using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Threading;
using Unity.Collections;
using UnityEditor.Media;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.Rendering;


namespace Ruccho.FFmpegRecorder
{
    public class FFmpegRecorder : BaseTextureRecorder<FFmpegRecorderSettings>
    {
        protected override TextureFormat ReadbackTextureFormat => TextureFormat.RGBA32;

        private Func<FFmpegRecorder, NativeArray<float>> AudioInputMainBufferGet { get; set; }
        private string AbsoluteFilename { get; set; }

        public RecorderInput audioInput => m_Inputs[1];

        protected override bool BeginRecording(RecordingSession session)
        {
            if (!base.BeginRecording(session))
                return false;

            try
            {
                Settings.FileNameGenerator.CreateDirectory(session);
            }
            catch (Exception)
            {
                Debug.LogError(string.Format("FFmpeg recorder output directory \"{0}\" could not be created.",
                    Settings.FileNameGenerator.BuildAbsolutePath(session)));
                return false;
            }

            var input = m_Inputs[0] as BaseRenderTextureInput;
            if (input == null)
            {
                Debug.LogError("FFmpegRecorder could not find input.");
                return false;
            }

            int width = input.OutputWidth;
            int height = input.OutputHeight;

            if (width <= 0 || height <= 0)
            {
                Debug.LogError(string.Format("FFmpegRecorder got invalid input resolution {0} x {1}.", width, height));
                return false;
            }
            
            var audioAttrsList = new List<AudioTrackAttributes>();

            var audioInputType = typeof(Recorder).Assembly.GetType("UnityEditor.Recorder.Input.AudioInput");
            if (audioInputType == null)
            {
                Debug.LogError("AudioInput Type was not found.");
                return false;
            }

            var audioInputSampleRate = (int) audioInputType.GetProperty("sampleRate").GetValue(audioInput);
            var audioInputChannelCount = (ushort) audioInputType.GetProperty("channelCount").GetValue(audioInput);

            //var audioInputParamEx = Expression.Parameter(audioInputType, "audioInput");
            var ffmpegRecorderParamEx = Expression.Parameter(typeof(FFmpegRecorder), "ffmpegRecorder");
            
            var audioInputAsRecorderInputEx =
                Expression.MakeMemberAccess(ffmpegRecorderParamEx, typeof(FFmpegRecorder).GetProperty("audioInput"));

            var audioInputEx = Expression.TypeAs(audioInputAsRecorderInputEx, audioInputType);
            
            var audioInputMainBufferEx =
                Expression.MakeMemberAccess(audioInputEx, audioInputType.GetProperty("mainBuffer"));
            
            var audioInputMainBufferGetEx = Expression.Lambda<Func<FFmpegRecorder, NativeArray<float>>>(
                audioInputMainBufferEx, ffmpegRecorderParamEx);
            
            AudioInputMainBufferGet = audioInputMainBufferGetEx.Compile();

            //Debug.Log(AudioInputMainBufferGet(this));


#if UNITY_EDITOR_OSX
            if (Settings.AudioInputSettings.PreserveAudio)
            {
                // Special case with WebM and audio on older Apple computers: deactivate async GPU readback because there
                // is a risk of not respecting the WebM standard and receiving audio frames out of sync (see "monotonically
                // increasing timestamps"). This happens only with Target Cameras.
                if (m_Inputs[0].settings is CameraInputSettings && Settings.OutputFormat == VideoRecorderOutputFormat.WebM)
                {
                    UseAsyncGPUReadback = false;
                }
            }
#endif

            try
            {
                AbsoluteFilename = Settings.FileNameGenerator.BuildAbsolutePath(session);
                bool mux = Settings.AudioInputSettings.PreserveAudio;

                CreateVideoProcess(width, height, RationalFromDouble(session.settings.FrameRate), AbsoluteFilename, !mux);

                if (mux)
                {
                    CreateAudioProcess(AbsoluteFilename, audioInputSampleRate, audioInputChannelCount);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("MovieRecorder unable to create MovieEncoder. " + ex.Message);
                return false;
            }
        }

        private FFmpegHost videoProcess;

        private void CreateVideoProcess(int width, int height, Rational frameRate, string outputPath,
            bool withoutMux = false)
        {
            videoProcess?.Dispose();
            videoProcess = new FFmpegHost(
                Settings.FFmpegExecutablePath,
                "-y -f rawvideo -vcodec rawvideo -pixel_format rgba"
                + " -video_size " + width + "x" + height
                + " -framerate " + frameRate.ToString()
                + " -loglevel error -i - " + "-pix_fmt yuv420p"
                + (Settings.VideoBitrate <= 0 ? "" : $" -b:v {Settings.VideoBitrate}")
                + (string.IsNullOrEmpty(Settings.VideoCodec) ? "" : $" -vcodec {Settings.VideoCodec}")
                + $" {Settings.VideoArguments}"
                + (withoutMux ? $" \"{outputPath}\"" : $" \"{outputPath}_video.{Settings.OutputExtension}\""));
        }

        private FFmpegHost audioProcess;

        private void CreateAudioProcess(string outputPath, int sampleRate, int channelCount)
        {
            audioProcess?.Dispose();
            audioProcess = new FFmpegHost(
                Settings.FFmpegExecutablePath,
                $"-y -f f32le -ar {sampleRate} -ac {channelCount}"
                + " -loglevel error -i - "
                + (string.IsNullOrEmpty(Settings.AudioCodec) ? "" : $" -acodec {Settings.AudioCodec}")
                + $" -ar {sampleRate} -ac {channelCount}"
                + $" {Settings.AudioArguments}"
                //+ $" -map 0:0 -f data"
                + $" \"{outputPath}_audio.{Settings.OutputExtension}\"");
        }

        private byte[] nativeArrayBuffer;

        protected override void WriteFrame(Texture2D t)
        {
            NativeArray<byte> cols = t.GetRawTextureData<byte>();
            InternalWriteFrame(cols);
        }

#if UNITY_2019_1_OR_NEWER
        protected override void WriteFrame(AsyncGPUReadbackRequest r)
        {
            var raw = r.GetData<byte>();
            InternalWriteFrame(raw);
        }

#endif

        private void InternalWriteFrame(NativeArray<byte> data)
        {
            if (nativeArrayBuffer == null || nativeArrayBuffer.Length != data.Length)
            {
                nativeArrayBuffer = new byte[data.Length];
            }

            data.CopyTo(nativeArrayBuffer);

            var input = m_Inputs[0] as BaseRenderTextureInput;
            int width = input.OutputWidth;
            int height = input.OutputHeight;

            if (videoProcess != null && videoProcess.StdIn != null)
            {
                if (videoProcess.StdIn.BaseStream.CanWrite)
                {
                    videoProcess.StdIn.BaseStream.Write(nativeArrayBuffer, 0, nativeArrayBuffer.Length);
                    videoProcess.StdIn.BaseStream.Flush();
                }
            }
        }

        //private float[] audioBuffer;
        protected override void RecordFrame(RecordingSession session)
        {
            base.RecordFrame(session);
            

            bool mux = Settings.AudioInputSettings.PreserveAudio;


            if (mux && (videoProcess == null || audioProcess == null || videoProcess.FFmpeg.HasExited ||
                        audioProcess.FFmpeg.HasExited)
                || (!mux && (videoProcess == null || videoProcess.FFmpeg.HasExited)))
            {
                AbortRecording(session);
                return;
            }

            if (mux)
            {
                var mainBuffer = AudioInputMainBufferGet(this);
                
                for (int i = 0; i < mainBuffer.Length; i++)
                {
                    var bytes = BitConverter.GetBytes(mainBuffer[i]);
                    audioProcess.StdIn.BaseStream.Write(bytes, 0, bytes.Length);
                }
                
                audioProcess.StdIn.BaseStream.Flush();
            }
        }

        private string CloseProcess(FFmpegHost process)
        {
            if (process != null)
            {
                if (process.StdIn != null)
                {
                    process.StdIn.Close();
                }

                process.FFmpeg.WaitForExit();
                var outputReader = process.FFmpeg.StandardError;
                var error = outputReader.ReadToEnd();
                return error;
            }

            return null;
        }
        private void AbortRecording(RecordingSession session)
        {
            Debug.LogError("FFmpeg has exited.");

            var videoError = CloseProcess(videoProcess);
            if (!string.IsNullOrEmpty(videoError)) Debug.LogError("Video encoding process: " + videoError);
            
            videoProcess?.Dispose();
            videoProcess = null;
            
            var audioError = CloseProcess(videoProcess);
            if (!string.IsNullOrEmpty(videoError)) Debug.LogError("Video encoding process: " + audioError);
            
            audioProcess?.Dispose();
            audioProcess = null;
            
            session.Dispose();
        }

        protected override void DisposeEncoder()
        {
            base.DisposeEncoder();

            Debug.Log("FFmpeg Recorder: End Encoder");

            bool mux = Settings.AudioInputSettings.PreserveAudio;

            if (audioProcess != null && videoProcess != null)
            {
                var videoError = CloseProcess(videoProcess);
                if (!string.IsNullOrEmpty(videoError)) Debug.LogError("Video encoding process: " + videoError);
                var audioError = CloseProcess(audioProcess);
                if (!string.IsNullOrEmpty(videoError)) Debug.LogError("Audio encoding process: " + audioError);

                if (mux)
                {
                    var muxProcess = new FFmpegHost(
                        Settings.FFmpegExecutablePath,
                        $"-y -loglevel error" +
                        $" -i \"{AbsoluteFilename}_audio.{Settings.OutputExtension}\" -i \"{AbsoluteFilename}_video.{Settings.OutputExtension}\"" +
                        $" -c:v copy -c:a copy" +
                        $" \"{AbsoluteFilename}\""
                        , true);
                    Debug.Log("FFmpeg Recorder: Start muxing");

                    muxProcess.FFmpeg.EnableRaisingEvents = true;
                    SynchronizationContext c = SynchronizationContext.Current;
                    muxProcess.FFmpeg.Exited += (obj, e) =>
                    {
                        c.Post((_) =>
                        {
                            muxProcess.FFmpeg.WaitForExit();
                            var outputReader = muxProcess.FFmpeg.StandardError;
                            var error = outputReader.ReadToEnd();
                            if (!string.IsNullOrEmpty(error)) Debug.LogError("Muxing process: " + error);
                            else
                            {
                                File.Delete($"{AbsoluteFilename}_audio.{Settings.OutputExtension}");
                                File.Delete($"{AbsoluteFilename}_video.{Settings.OutputExtension}");
                            }

                            Debug.Log("FFmpeg Recorder: End muxing");
                        }, false);
                    };
                }
                
                videoProcess?.Dispose();
                audioProcess?.Dispose();
                videoProcess = null;
                audioProcess = null;
            }
        }

        static long GreatestCommonDivisor(long a, long b)
        {
            if (a == 0)
                return b;

            if (b == 0)
                return a;

            return (a < b) ? GreatestCommonDivisor(a, b % a) : GreatestCommonDivisor(b, a % b);
        }

        private static Rational RationalFromDouble(double value)
        {
            var integral = Math.Floor(value);
            var frac = value - integral;

            const long precision = 10000000;

            var gcd = GreatestCommonDivisor((long) Math.Round(frac * precision), precision);
            var denom = precision / gcd;

            return new Rational()
            {
                numerator = (int) ((long) integral * denom + ((long) Math.Round(frac * precision)) / gcd),
                denominator = (int) denom
            };
        }

        public struct Rational
        {
            public int numerator;
            public int denominator;

            public override string ToString()
            {
                return $"{numerator}/{denominator}";
            }
        }
    }
}