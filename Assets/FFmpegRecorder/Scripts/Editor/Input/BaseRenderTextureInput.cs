using System.Collections;
using System.Collections.Generic;
using UnityEditor.Recorder;
using UnityEngine;

namespace Ruccho.FFmpegRecorder
{

    public abstract class BaseRenderTextureInput : RecorderInput
    {

        public RenderTexture OutputRenderTexture { get; set; }
        
        public Texture2D ReadbackTexture { get; set; }
        
        public int OutputWidth { get; protected set; }

        public int OutputHeight { get; protected set; }
        
        protected void ReleaseBuffer()
        {
            if (OutputRenderTexture != null)
            {
                if (OutputRenderTexture == RenderTexture.active)
                    RenderTexture.active = null;

                OutputRenderTexture.Release();
                OutputRenderTexture = null;
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                ReleaseBuffer();
        }
    }
}