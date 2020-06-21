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

        [SerializeField] private GameViewInputSettings gameViewInputSettings = new GameViewInputSettings();
        [SerializeField] AudioInputSettings m_AudioInputSettings = new AudioInputSettings();

        public override IEnumerable<RecorderInputSettings> InputsSettings
        {
            get
            {
                yield return gameViewInputSettings;
                yield return m_AudioInputSettings;
            }
        }
        [SerializeField]
        private string extension = "mp4";
        protected override string Extension => extension;
        public string FileExtension => extension;

        [SerializeField]
        private string ffmpegExecutablePath;
        public string FFmpegExecutablePath => ffmpegExecutablePath;

        public string VideoCodec => videoCodec;
        [SerializeField] private string videoCodec;
        public string AudioCodec => audioCodec;
        [SerializeField] private string audioCodec;
        public int VideoBitrate => videoBitrate;
        [SerializeField] private int videoBitrate = 12000000;
        public string VideoArguments => videoArguments;
        [SerializeField] private string videoArguments;
        public string AudioArguments => audioArguments;
        [SerializeField] private string audioArguments;

    }

}