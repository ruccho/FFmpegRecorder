using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Ruccho.FFmpegRecorder
{
    public class FFmpegRecorder : BaseTextureRecorder<FFmpegRecorderSettings>
    {
        private string Filename;
        protected override TextureFormat ReadbackTextureFormat => TextureFormat.RGBA32;

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
                Debug.LogError(string.Format("Movie recorder output directory \"{0}\" could not be created.", Settings.FileNameGenerator.BuildAbsolutePath(session)));
                return false;
            }

            var input = m_Inputs[0] as BaseRenderTextureInput;
            if (input == null)
            {
                Debug.LogError("MediaRecorder could not find input.");
                return false;
            }
            int width = input.OutputWidth;
            int height = input.OutputHeight;

            if (width <= 0 || height <= 0)
            {
                Debug.LogError(string.Format("MovieRecorder got invalid input resolution {0} x {1}.", width, height));
                return false;
            }

            var audioInput = (AudioInput)m_Inputs[1];

            bool mux = audioInput.audioSettings.PreserveAudio;

            Filename = Settings.FileNameGenerator.BuildAbsolutePath(session);
            CreateVideoProcess(width, height, RationalFromDouble(session.settings.FrameRate), Filename, !mux);

            if (mux)
            {
                CreateAudioProcess(Filename);
            }
            return true;

        }

        private FFmpegHost videoProcess;

        private void CreateVideoProcess(int width, int height, Rational frameRate, string outputPath, bool withoutMux = false)
        {
            videoProcess = new FFmpegHost(
                Settings.FFmpegExecutablePath,
                "-y -f rawvideo -vcodec rawvideo -pixel_format rgba"
                + " -video_size " + width + "x" + height
                + " -framerate " + frameRate.ToString()
                + " -loglevel error -i - " + "-pix_fmt yuv420p"
                + (Settings.VideoBitrate <= 0 ? "" : $" -b:v {Settings.VideoBitrate}")
                + (string.IsNullOrEmpty(Settings.VideoCodec) ? "" : $" -vcodec {Settings.VideoCodec}")
                + $" {Settings.VideoArguments}"
                //+ " -map 0:0 -f data"
                + (withoutMux ? $" \"{outputPath}\"" : $" \"{outputPath}_video.{Settings.FileExtension}\""));
        }

        private FFmpegHost audioProcess;

        private void CreateAudioProcess(string outputPath)
        {
            var audioInput = (AudioInput)m_Inputs[1];

            audioProcess = new FFmpegHost(
                Settings.FFmpegExecutablePath,
                $"-y -f f32le -ar {audioInput.sampleRate} -ac {audioInput.channelCount}"
                + " -loglevel error -i - "
                + (string.IsNullOrEmpty(Settings.AudioCodec) ? "" : $" -acodec {Settings.AudioCodec}")
                + $" -ar {audioInput.sampleRate} -ac {audioInput.channelCount}"
                + $" {Settings.AudioArguments}"
                //+ $" -map 0:0 -f data"
                + " \"" + outputPath + $"_audio.{Settings.FileExtension}\"");
        }

#if UNITY_2019_1_OR_NEWER

        private byte[] nativeArrayBuffer;
        protected override void WriteFrame(AsyncGPUReadbackRequest r)
        {
            var raw = r.GetData<byte>();
            if(nativeArrayBuffer == null || nativeArrayBuffer.Length != raw.Length)
            {
                nativeArrayBuffer = new byte[raw.Length];
            }
            raw.CopyTo(nativeArrayBuffer);

            var input = m_Inputs[0] as BaseRenderTextureInput;
            int width = input.OutputWidth;
            int height = input.OutputHeight;

            if (videoProcess != null && videoProcess.StdIn != null)
            {
                if (videoProcess.StdIn.BaseStream.CanWrite)
                {
                    videoProcess.StdIn.BaseStream.Write(nativeArrayBuffer, 0, nativeArrayBuffer.Length);
                    /*
                    for (int y = height - 1; y >= 0; y--)
                    {
                        videoProcess.StdIn.BaseStream.Write(nativeArrayBuffer, y * width * 4, width * 4);
                    }*/
                    videoProcess.StdIn.BaseStream.Flush();
                }
            }
        }
#endif
        protected override void WriteFrame(Texture2D t)
        {
            byte[] cols = t.GetRawTextureData();
            var input = m_Inputs[0] as BaseRenderTextureInput;
            int width = input.OutputWidth;
            int height = input.OutputHeight;

            if (videoProcess != null && videoProcess.StdIn != null)
            {
                if (videoProcess.StdIn.BaseStream.CanWrite)
                {
                    for (int y = height - 1; y >= 0; y--)
                    {
                        videoProcess.StdIn.BaseStream.Write(cols, y * width * 4, width * 4);
                    }
                    videoProcess.StdIn.BaseStream.Flush();
                }
            }
        }


        protected override void RecordFrame(RecordingSession session)
        {
            base.RecordFrame(session);

            var audioInput = (AudioInput)m_Inputs[1];

            bool mux = audioInput.audioSettings.PreserveAudio;


            if (mux && (videoProcess == null || audioProcess == null || videoProcess.FFmpeg.HasExited || audioProcess.FFmpeg.HasExited)
                    || (!mux && (videoProcess == null || videoProcess.FFmpeg.HasExited)))
            {
                AbortRecording(session);
                return;
            }

            if (mux)
            {
                for (int i = 0; i < audioInput.mainBuffer.Length; i++)
                {
                    var bytes = BitConverter.GetBytes(audioInput.mainBuffer[i]);
                    audioProcess.StdIn.BaseStream.Write(bytes, 0, bytes.Length);
                }
                audioProcess.StdIn.BaseStream.Flush();
            }
        }

        private void AbortRecording(RecordingSession session)
        {
            Debug.LogError("FFmpeg has exited.");

            if (videoProcess != null)
            {
                if (videoProcess.StdIn != null)
                {
                    videoProcess.StdIn.Close();
                }
                videoProcess.FFmpeg.WaitForExit();
                var outputReader = videoProcess.FFmpeg.StandardError;
                var error = outputReader.ReadToEnd();
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Video encoding process: " + error);
                videoProcess = null;
            }

            if (audioProcess != null)
            {
                if (audioProcess.StdIn != null)
                {
                    audioProcess.StdIn.Close();
                }
                var outputReader = audioProcess.FFmpeg.StandardError;
                var error = outputReader.ReadToEnd();
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Audio encoding process: " + error);
                audioProcess = null;
            }

            session.Dispose();
        }

        protected override void DisposeEncoder()
        {
            base.DisposeEncoder();

            Debug.Log("End Encoder");

            var audioInput = (AudioInput)m_Inputs[1];

            bool mux = audioInput.audioSettings.PreserveAudio;

            if (audioProcess != null)
            {
                if (audioProcess.StdIn != null)
                {
                    audioProcess.StdIn.Close();
                }
                audioProcess.FFmpeg.WaitForExit();
                var outputReader = audioProcess.FFmpeg.StandardError;
                var error = outputReader.ReadToEnd();
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Audio encoding process: " + error);
            }

            if (videoProcess != null)
            {
                if (videoProcess.StdIn != null)
                {
                    videoProcess.StdIn.Close();
                }
                videoProcess.FFmpeg.WaitForExit();
                var outputReader = videoProcess.FFmpeg.StandardError;
                var error = outputReader.ReadToEnd();
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Video encoding process: " + error);
            }


            if (mux && audioProcess != null && videoProcess != null)
            {
                var muxProcess = new FFmpegHost(
                Settings.FFmpegExecutablePath,
                    $"-y -loglevel error" +
                    $" -i \"{Filename}_audio.{Settings.FileExtension}\" -i \"{Filename}_video.{Settings.FileExtension}\"" +
                    $" -c:v copy -c:a copy" +
                    $" \"{Filename}\""
                    , true);
                Debug.Log("Start muxing");

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

                        File.Delete($"{Filename}_audio.{Settings.FileExtension}");
                        File.Delete($"{Filename}_video.{Settings.FileExtension}");
                        Debug.Log("End muxing");
                    }, false);
                };

            }

            videoProcess = null;
            audioProcess = null;


        }

        static long GreatestCommonDivisor(long a, long b)
        {
            if (a == 0)
                return b;

            if (b == 0)
                return a;

            return (a < b) ? GreatestCommonDivisor(a, b % a) : GreatestCommonDivisor(b, a % b);
        }

        static Rational RationalFromDouble(double value)
        {
            var integral = Math.Floor(value);
            var frac = value - integral;

            const long precision = 10000000;

            var gcd = GreatestCommonDivisor((long)Math.Round(frac * precision), precision);
            var denom = precision / gcd;

            return new Rational()
            {
                numerator = (int)((long)integral * denom + ((long)Math.Round(frac * precision)) / gcd),
                denominator = (int)denom
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

    public abstract class BaseTextureRecorder<T> : GenericRecorder<T> where T : RecorderSettings
    {
        int m_OngoingAsyncGPURequestsCount;
        bool m_DelayedEncoderDispose;
        bool m_UseAsyncGPUReadback;
        Texture2D m_ReadbackTexture;

        protected abstract TextureFormat ReadbackTextureFormat { get; }

        protected override bool BeginRecording(RecordingSession session)
        {
            if (!base.BeginRecording(session))
                return false;

            m_UseAsyncGPUReadback = SystemInfo.supportsAsyncGPUReadback;
            m_OngoingAsyncGPURequestsCount = 0;
            m_DelayedEncoderDispose = false;
            return true;
        }

        protected override void RecordFrame(RecordingSession session)
        {
            var input = (BaseRenderTextureInput)m_Inputs[0];

            if (input.ReadbackTexture != null)
            {
                WriteFrame(input.ReadbackTexture);
                return;
            }

            var renderTexture = input.OutputRenderTexture;

            if (m_UseAsyncGPUReadback)
            {
                AsyncGPUReadback.Request(
                    renderTexture, 0, ReadbackTextureFormat, ReadbackDone);
                ++m_OngoingAsyncGPURequestsCount;
                return;
            }

            var width = renderTexture.width;
            var height = renderTexture.height;

            if (m_ReadbackTexture == null)
                m_ReadbackTexture = CreateReadbackTexture(width, height);

            var backupActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            m_ReadbackTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            m_ReadbackTexture.Apply();
            RenderTexture.active = backupActive;
            WriteFrame(m_ReadbackTexture);
        }

        private void ReadbackDone(AsyncGPUReadbackRequest r)
        {
            Profiler.BeginSample("BaseTextureRecorder.ReadbackDone");
            WriteFrame(r);
            Profiler.EndSample();
            --m_OngoingAsyncGPURequestsCount;
            if (m_OngoingAsyncGPURequestsCount == 0 && m_DelayedEncoderDispose)
                DisposeEncoder();
        }

        protected override void EndRecording(RecordingSession session)
        {
            base.EndRecording(session);
            if (m_OngoingAsyncGPURequestsCount > 0)
                m_DelayedEncoderDispose = true;
            else
                DisposeEncoder();
        }

        private Texture2D CreateReadbackTexture(int width, int height)
        {
            return new Texture2D(width, height, ReadbackTextureFormat, false);
        }

        protected virtual void WriteFrame(AsyncGPUReadbackRequest r)
        {
            if (m_ReadbackTexture == null)
                m_ReadbackTexture = CreateReadbackTexture(r.width, r.height);
            Profiler.BeginSample("BaseTextureRecorder.LoadRawTextureData");
            m_ReadbackTexture.LoadRawTextureData(r.GetData<byte>());
            Profiler.EndSample();
            WriteFrame(m_ReadbackTexture);
        }

        protected abstract void WriteFrame(Texture2D t);

        protected virtual void DisposeEncoder()
        {
            UnityHelpers.Destroy(m_ReadbackTexture);
        }
    }
}