using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEngine;

namespace Ruccho.FFmpegRecorder
{
    [CustomEditor(typeof(FFmpegRecorderSettings))]
    public class FFmpegRecorderEditor : RecorderEditor
    {
        private SerializedProperty ffmpegExecutablePath;
        private SerializedProperty extension;
        private SerializedProperty usePreset;
        private SerializedProperty preset;
        private SerializedProperty videoCodec;
        private SerializedProperty videoBitrate;
        private SerializedProperty videoArguments;
        private SerializedProperty audioCodec;
        private SerializedProperty audioArguments;
        private GUIContent[] presetLabels;
        private int[] presetOptions;
        private static int padval = 30;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (target != null)
            {
                //Initialize presets
                var presets = FFmpegPreset.GetValues(typeof(FFmpegPreset));
                presetLabels = presets.Cast<FFmpegPreset>().Select(p => new GUIContent(p.GetDisplayName())).ToArray();
                presetOptions = presets.Cast<int>().ToArray();

                ffmpegExecutablePath = serializedObject.FindProperty("ffmpegExecutablePath");
                extension = serializedObject.FindProperty("extension");
                usePreset = serializedObject.FindProperty("usePreset");
                preset = serializedObject.FindProperty("preset");
                videoCodec = serializedObject.FindProperty("videoCodec");
                videoBitrate = serializedObject.FindProperty("videoBitrate");
                videoArguments = serializedObject.FindProperty("videoArguments");
                audioCodec = serializedObject.FindProperty("audioCodec");
                audioArguments = serializedObject.FindProperty("audioArguments");
            }
        }

        protected override void ImageRenderOptionsGUI()
        {
            base.ImageRenderOptionsGUI();
            //Check project audioManager setting is disable(e.g. some project using other audio system it will be disable)
            var audioManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0];
            var serializedManager = new SerializedObject(audioManager);
            var disableAudioProp = serializedManager.FindProperty("m_DisableAudio");
            if (disableAudioProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Audio setting is disabled in project settings .\n If your project using other audio system current is not support recode",
                    MessageType.Warning);
            }
        }

        protected override void FileTypeAndFormatGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(ffmpegExecutablePath);
            var oldPath = Application.dataPath;
            if (GUILayout.Button(new GUIContent("...", " Select ffmpeg path"), GUILayout.Width(30)))
            {
                if (!string.IsNullOrEmpty(ffmpegExecutablePath.stringValue))
                    oldPath = ffmpegExecutablePath.stringValue;
                var newPath = EditorUtility.OpenFilePanel("Select ffmpeg.exe path", oldPath, "exe");
                if (!string.IsNullOrEmpty(newPath))
                {
                    ffmpegExecutablePath.stringValue = newPath;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Preview ffmpeg path");
            EditorGUILayout.LabelField(ffmpegExecutablePath.stringValue);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(usePreset);
            if (!usePreset.boolValue)
            {
                EditorGUILayout.PropertyField(extension);
                EditorGUILayout.PropertyField(videoCodec);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.IntPopup(preset, presetLabels, presetOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    var ffmpegPreset = (FFmpegPreset)preset.enumValueIndex;
                    extension.stringValue = ffmpegPreset.GetSuffix();
                    videoCodec.stringValue = ffmpegPreset.GetVideoCodec();
                    videoArguments.stringValue = ffmpegPreset.GetAdditionalFormatVideoArguments();
                }
            }

            EditorGUILayout.PropertyField(videoBitrate, new GUIContent("VideoBitrate(Kb/s)"));
            EditorGUILayout.PropertyField(audioCodec);
            EditorGUILayout.PropertyField(videoArguments);
            EditorGUILayout.PropertyField(audioArguments);
        }
    }
}