using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Ruccho.FFmpegRecorder
{
    public class GameViewInput : BaseRenderTextureInput
    {
        bool m_ModifiedResolution;
        RenderTexture m_CaptureTexture;

        GameViewInputSettings scSettings
        {
            get { return (GameViewInputSettings)settings; }
        }

        protected override void NewFrameReady(RecordingSession session)
        {
#if UNITY_2019_1_OR_NEWER
            ScreenCapture.CaptureScreenshotIntoRenderTexture(m_CaptureTexture);
            //m_VFlipper?.Flip(m_CaptureTexture);
#else
            ReadbackTexture = ScreenCapture.CaptureScreenshotAsTexture();
#endif
        }

        protected override void BeginRecording(RecordingSession session)
        {
            int w;
            int h;
            GameViewSizeHelper.GetGameRenderSize(out w, out h);
            OutputWidth = w;
            OutputHeight = h;

#if !UNITY_2019_1_OR_NEWER
            return;
#else
            m_CaptureTexture = new RenderTexture(OutputWidth, OutputHeight, 0, RenderTextureFormat.ARGB32)
            {
                wrapMode = TextureWrapMode.Repeat
            };
            m_CaptureTexture.Create();
            
            OutputRenderTexture = m_CaptureTexture;
#endif
        }

        protected override void FrameDone(RecordingSession session)
        {
            UnityHelpers.Destroy(ReadbackTexture);
            ReadbackTexture = null;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    public class GameViewInputSettings : StandardImageInputSettings
    {
        protected override Type InputType => typeof(GameViewInput);
    }

}