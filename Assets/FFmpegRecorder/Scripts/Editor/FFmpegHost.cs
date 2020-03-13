using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Recorder;
using UnityEngine;

namespace Ruccho.FFmpegRecorder
{
    public class FFmpegHost
    {
        public System.Diagnostics.Process FFmpeg { get; private set; }
        public StreamWriter StdIn => FFmpeg?.StandardInput;
        public StreamReader StdOut => FFmpeg?.StandardOutput;
        public StreamReader StdErr => FFmpeg?.StandardError;
        

        public FFmpegHost(string executable, string arguments, bool redirect = true)
        {
            var psi = new System.Diagnostics.ProcessStartInfo()
            {
                Arguments = arguments,
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = redirect,
                RedirectStandardOutput = redirect,
                RedirectStandardError = redirect
                
            };

            FFmpeg = System.Diagnostics.Process.Start(psi);
        }
    }
}