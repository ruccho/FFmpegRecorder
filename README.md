# FFmpegRecorder

Extension for Unity Recorder that provides output through FFmpeg.

![image](https://pbs.twimg.com/media/ETFD78_UEAAdi7l?format=jpg&name=4096x4096)

## Requirements

- Unity 2018.4+
- Tested with Unity Recoeder 2.4.0
- Tested on Windows

## Installation

This package is provided through Git dependencies feature of Unity Package Manager.

1. Preapare FFmpeg executable on your computer.
2. Open your project.

(In Unity 2019.3+)

3. Open Package Manager Window and click "+" button and "Add package from git URL..."
4. Enter "https://github.com/ruccho/FFmpegRecorder.git?path=/Packages/io.github.ruccho.ffmpegrecorder"

(In Unity ~2019.2)

3. Open Packages/manifest.json
4. Add dependencies entry:
```json:manifest.json
{
  "dependencies": {
      ...
      "io.github.ruccho.ffmpegrecorder": "https://github.com/ruccho/FFmpegRecorder.git?path=/Packages/io.github.ruccho.ffmpegrecorder"
      ...
  }
}
```



## Usage

1. Open **Recorder Window**.
2. Click **Add New Recorders** and select **FFmpeg**.
3. Specify **Ffmpeg Executable Path** and **Extension**.
4. (optional) Specify codecs, bitrate, and custom arguments. Keep them empty (or 0) if you don't want to specify them.