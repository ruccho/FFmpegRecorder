using System.Collections;
using System.Collections.Generic;
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
        private SerializedProperty videoCodec;
        private SerializedProperty videoBitrate;
        private SerializedProperty videoArguments;
        private SerializedProperty audioCodec;
        private SerializedProperty audioArguments;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            if (target != null)
            {
                ffmpegExecutablePath = serializedObject.FindProperty("ffmpegExecutablePath");
                extension = serializedObject.FindProperty("extension");
                videoCodec = serializedObject.FindProperty("videoCodec");
                videoBitrate = serializedObject.FindProperty("videoBitrate");
                videoArguments = serializedObject.FindProperty("videoArguments");
                audioCodec = serializedObject.FindProperty("audioCodec");
                audioArguments = serializedObject.FindProperty("audioArguments");
            }
        }
        
        protected override void FileTypeAndFormatGUI()
        {
            EditorGUILayout.PropertyField(ffmpegExecutablePath);
            EditorGUILayout.PropertyField(extension);
            EditorGUILayout.PropertyField(videoCodec);
            EditorGUILayout.PropertyField(videoBitrate);
            EditorGUILayout.PropertyField(videoArguments);
            EditorGUILayout.PropertyField(audioCodec);
            EditorGUILayout.PropertyField(audioArguments);
        }
        
    }
}