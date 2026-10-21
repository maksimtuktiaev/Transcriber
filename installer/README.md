# Windows installer

The x64 installer is built with Inno Setup from `Transcriber.iss`.

1. Publish the self-contained x64 application into `installer/payload`.
2. Place FFmpeg's `ffmpeg.exe`, its required DLLs, and notices in `installer/payload`.
3. Compile `Transcriber.iss` with Inno Setup. The output is `installer/release/TranscriberSetup-x64.exe`.

The setup is per-user and does not require administrator rights. It includes the .NET runtime, FFmpeg and its shared libraries, and the Whisper CPU/CUDA native runtimes. The GPU mode requires a compatible NVIDIA driver and CUDA installation; CPU mode does not.

Whisper model files are downloaded on first use into `%LocalAppData%\Transcriber\models`; bundling the default turbo model would add about 1.6 GB to the setup. The first model download therefore needs internet access and disk space.

The setup installs third-party notices under `{app}\licenses`. Keep the corresponding source links and licenses with every public release.
