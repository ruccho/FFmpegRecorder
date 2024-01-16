using System.Collections;
using System.Collections.Generic;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Ruccho.FFmpegRecorder
{
    [RecorderSettings(typeof(FFmpegRecorder), "FFmpeg")]
    public class FFmpegRecorderSettings : RecorderSettings
    {
        public ImageInputSelector ImageInputSelector => imageInputSelector;
        [SerializeField] ImageInputSelector imageInputSelector = new ImageInputSelector();

        public AudioInputSettings AudioInputSettings => audioInputSettings;
        [SerializeField] AudioInputSettings audioInputSettings = new AudioInputSettings();

        public string FFmpegExecutablePath => ffmpegExecutablePath;
        [SerializeField] private string ffmpegExecutablePath = default;
        public string OutputExtension => extension;
        public bool UsePreset = false;
        
        [SerializeField] FFmpegPreset preset;
        public FFmpegPreset Preset => preset;

        [SerializeField] private string extension = "mp4";
        protected override string Extension => extension;
        [SerializeField] private bool usePreset = false;
        public string VideoCodec => videoCodec;
        [SerializeField] private string videoCodec;
        public string AudioCodec => audioCodec;
        [SerializeField] private string audioCodec = "aac";
        public int VideoBitrate => videoBitrate;
        [SerializeField] private int videoBitrate = 0;
        public string VideoArguments => videoArguments;
        [SerializeField] private string videoArguments;
        public string AudioArguments => audioArguments;
        [SerializeField] private string audioArguments;
        private int[] presetOptions;

        public override IEnumerable<RecorderInputSettings> InputsSettings
        {
            get
            {
                yield return imageInputSelector.Selected;
                yield return audioInputSettings;
            }
        }
    }
}