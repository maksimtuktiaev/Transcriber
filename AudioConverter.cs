using System.Diagnostics;
#if WINDOWS
using NAudio.Wave;
#endif

namespace Transcriber;

/// <summary>
/// Whisper принимает только WAV 16 кГц, моно, 16 бит.
/// Python-версия делала это внутри faster-whisper (через PyAV), здесь — через ffmpeg
/// через установленный ffmpeg (встроенный Media Foundation используется только в Windows).
/// </summary>
public static class AudioConverter
{
    public static async Task<(string WavPath, TimeSpan Duration)> ToWhisperWavAsync(
        string inputFile,
        CancellationToken ct)
    {
        string wavPath = Path.Combine(Path.GetTempPath(), $"transcriber_{Guid.NewGuid():N}.wav");

        string? ffmpeg = FindFfmpeg();
        string? ffmpegError = null;

        try
        {
            if (ffmpeg != null)
            {
                ffmpegError = await RunFfmpegAsync(ffmpeg, inputFile, wavPath, ct);
                if (ffmpegError == null)
                    return (wavPath, GetDuration(wavPath));
            }

            // Запасной путь Windows: Media Foundation.
#if WINDOWS
            await Task.Run(() => ConvertWithMediaFoundation(inputFile, wavPath), ct);
            return (wavPath, GetDuration(wavPath));
#else
            throw new InvalidOperationException(ffmpegError ?? "ffmpeg не найден в PATH.");
#endif
        }
        catch (OperationCanceledException)
        {
            TryDelete(wavPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(wavPath);

            string hint = ffmpeg == null
#if WINDOWS
                ? "\n\nУстановите ffmpeg (положите ffmpeg.exe рядом с Transcriber.exe) — он читает любые форматы."
#else
                ? "\n\nУстановите ffmpeg через менеджер пакетов вашей системы (например, apt или Homebrew)."
#endif
                : $"\n\nОшибка ffmpeg:\n{ffmpegError}";

            throw new InvalidOperationException(
                "Не удалось прочитать аудио из файла: " + ex.Message + hint, ex);
        }
    }

    public static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* не критично */ }
    }

    private static TimeSpan GetDuration(string wavPath)
    {
#if WINDOWS
        using var reader = new WaveFileReader(wavPath);
        var duration = reader.TotalTime;
#else
        // WAV заголовок содержит среднюю скорость потока и размер аудиоданных.
        using var stream = File.OpenRead(wavPath);
        using var reader = new BinaryReader(stream);
        if (new string(reader.ReadChars(4)) != "RIFF")
            throw new InvalidOperationException("Некорректный WAV файл.");
        reader.ReadUInt32();
        if (new string(reader.ReadChars(4)) != "WAVE")
            throw new InvalidOperationException("Некорректный WAV файл.");
        uint bytesPerSecond = 0;
        uint dataLength = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            string chunk = new(reader.ReadChars(4));
            uint size = reader.ReadUInt32();
            long next = stream.Position + size + (size & 1);
            if (chunk == "fmt " && size >= 12)
            {
                stream.Position += 8;
                bytesPerSecond = reader.ReadUInt32();
            }
            else if (chunk == "data")
                dataLength = size;
            stream.Position = Math.Min(next, stream.Length);
        }
        var duration = bytesPerSecond > 0 ? TimeSpan.FromSeconds((double)dataLength / bytesPerSecond) : TimeSpan.Zero;
#endif

        if (duration <= TimeSpan.Zero)
            throw new InvalidOperationException("Не удалось определить длительность аудио.");

        return duration;
    }

#if WINDOWS
    private static void ConvertWithMediaFoundation(string input, string output)
    {
        using var reader = new MediaFoundationReader(input);
        var format = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(reader, format) { ResamplerQuality = 60 };
        WaveFileWriter.CreateWaveFile(output, resampler);
    }
#endif

    private static string? FindFfmpeg()
    {
        string executable = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        string local = Path.Combine(AppContext.BaseDirectory, executable);
        if (File.Exists(local))
            return local;

        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (OperatingSystem.IsMacOS())
            path += Path.PathSeparator + "/opt/homebrew/bin" + Path.PathSeparator + "/usr/local/bin";

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim().Trim('"'), executable);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch { /* некорректный путь в PATH */ }
        }

        return null;
    }

    /// <summary>Возвращает null при успехе или текст ошибки.</summary>
    private static async Task<string?> RunFfmpegAsync(
        string ffmpeg, string input, string output, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        foreach (var arg in new[]
                 {
                     "-y", "-hide_banner", "-loglevel", "error",
                     "-i", input,
                     "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le",
                     output
                 })
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var registration = ct.Register(() =>
        {
            try { process.Kill(true); } catch { /* уже завершён */ }
        });

        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync(CancellationToken.None);
        ct.ThrowIfCancellationRequested();

        string stderr = await stderrTask;
        await stdoutTask;

        if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length < 100)
            return string.IsNullOrWhiteSpace(stderr) ? $"код выхода {process.ExitCode}" : stderr.Trim();

        return null;
    }
}
