﻿using System;
using UnityEngine;
using System.Reflection;
using Unity.Collections;
using UnityEditor.Recorder;
using System.ComponentModel;
using System.Collections.Generic;

namespace Ruccho.FFmpegRecorder
{
    static class AudioRenderer
    {
        static readonly MethodInfo s_StartMethod;
        static readonly MethodInfo s_StopMethod;
        static readonly MethodInfo s_GetSampleCountForCaptureFrameMethod;
        static readonly MethodInfo s_RenderMethod;

        static int s_StartCount = 0;

        static AudioRenderer()
        {
            const string className = "UnityEngine.AudioRenderer";
            const string dllName = "UnityEngine";
            var audioRecorderType = Type.GetType(className + ", " + dllName);
            if (audioRecorderType == null)
            {
                Debug.Log("AudioInput could not find " + className + " type in " + dllName);
                return;
            }
            s_StartMethod = audioRecorderType.GetMethod("Start");
            s_StopMethod = audioRecorderType.GetMethod("Stop");
            s_GetSampleCountForCaptureFrameMethod =
                audioRecorderType.GetMethod("GetSampleCountForCaptureFrame");
            s_RenderMethod = audioRecorderType.GetMethod("Render");
            s_StartCount = 0;
        }

        public static void Start()
        {
            if (s_StartCount == 0)
                s_StartMethod.Invoke(null, null);

            ++s_StartCount;
        }

        public static void Stop()
        {
            --s_StartCount;

            if (s_StartCount <= 0)
                s_StopMethod.Invoke(null, null);
        }

        public static uint GetSampleCountForCaptureFrame()
        {
            var count = (int)s_GetSampleCountForCaptureFrameMethod.Invoke(null, null);
            return (uint)count;
        }

        public static void Render(NativeArray<float> buffer)
        {
            s_RenderMethod.Invoke(null, new object[] { buffer });
        }
    }

    class AudioInput : RecorderInput
    {
        class BufferManager : IDisposable
        {
            readonly NativeArray<float>[] m_Buffers;

            public BufferManager(ushort bufferCount, uint sampleFrameCount, ushort channelCount)
            {
                m_Buffers = new NativeArray<float>[bufferCount];
                for (int i = 0; i < m_Buffers.Length; ++i)
                    m_Buffers[i] = new NativeArray<float>((int)sampleFrameCount * channelCount, Allocator.Persistent);
            }

            public NativeArray<float> GetBuffer(int index)
            {
                return m_Buffers[index];
            }

            public void Dispose()
            {
                foreach (var a in m_Buffers)
                    a.Dispose();
            }
        }

        ushort m_ChannelCount;

        public ushort channelCount
        {
            get { return m_ChannelCount; }
        }

        public int sampleRate
        {
            get { return AudioSettings.outputSampleRate; }
        }

        public NativeArray<float> mainBuffer
        {
            get { return s_BufferManager.GetBuffer(0); }
        }

        static AudioInput s_Handler;
        static BufferManager s_BufferManager;

        public AudioInputSettings audioSettings
        {
            get { return (AudioInputSettings)settings; }
        }

        protected override void BeginRecording(RecordingSession session)
        {
            m_ChannelCount = new Func<ushort>(() => {
                switch (AudioSettings.speakerMode)
                {
                    case AudioSpeakerMode.Mono: return 1;
                    case AudioSpeakerMode.Stereo: return 2;
                    case AudioSpeakerMode.Quad: return 4;
                    case AudioSpeakerMode.Surround: return 5;
                    case AudioSpeakerMode.Mode5point1: return 6;
                    case AudioSpeakerMode.Mode7point1: return 7;
                    case AudioSpeakerMode.Prologic: return 2;
                    default: return 1;
                }
            })();

            if (RecorderOptions.VerboseMode)
                Debug.Log(string.Format("AudioInput.BeginRecording for capture frame rate {0}", Time.captureFramerate));

            if (audioSettings.PreserveAudio)
                AudioRenderer.Start();
        }

        protected override void NewFrameReady(RecordingSession session)
        {
            if (!audioSettings.PreserveAudio)
                return;

            if (s_Handler == null)
                s_Handler = this;

            if (s_Handler == this)
            {
                var sampleFrameCount = AudioRenderer.GetSampleCountForCaptureFrame();
                if (RecorderOptions.VerboseMode)
                    Debug.Log(string.Format("AudioInput.NewFrameReady {0} audio sample frames @ {1} ch",
                        sampleFrameCount, m_ChannelCount));

                const ushort bufferCount = 1;

                if (s_BufferManager != null)
                    s_BufferManager.Dispose();

                s_BufferManager = new BufferManager(bufferCount, sampleFrameCount, m_ChannelCount);

                AudioRenderer.Render(mainBuffer);
            }
        }

        protected override void FrameDone(RecordingSession session)
        {

        }

        protected override void EndRecording(RecordingSession session)
        {
            if (s_BufferManager != null)
            {
                s_BufferManager.Dispose();
                s_BufferManager = null;
            }

            s_Handler = null;

            if (audioSettings.PreserveAudio)
                AudioRenderer.Stop();
        }
    }
    
    [DisplayName("Audio")]
    [Serializable]
    public class AudioInputSettings : RecorderInputSettings
    {
        public bool PreserveAudio
        {
            get { return preserveAudio; }
            set { preserveAudio = value; }
        }

        [SerializeField] private bool preserveAudio = true;

        protected override Type InputType
        {
            get { return typeof(AudioInput); }
        }
        
        protected override bool ValidityCheck(List<string> errors)
        {
            return true;
        }
    }
}