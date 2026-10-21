using System.Diagnostics;
using NAudio.Wave;

namespace Transcriber;

/// <summary>
/// Whisper принимает только WAV 16 кГц, моно, 16 бит.
/// Python-версия делала это внутри faster-whisper (через PyAV), здесь — через ffmpeg
/// или, если его нет, через встроенные кодеки Windows (Media Foundation).
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

            // Запасной путь: Media Foundation (mp3, wav, m4a, mp4, wma, часто avi/flac)
            await Task.Run(() => ConvertWithMediaFoundation(inputFile, wavPath), ct);
            return (wavPath, GetDuration(wavPath));
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
                ? "\n\nУстановите ffmpeg (положите ffmpeg.exe рядом с Transcriber.exe) — он читает любые форматы."
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
        using var reader = new WaveFileReader(wavPath);
        var duration = reader.TotalTime;

        if (duration <= TimeSpan.Zero)
            throw new InvalidOperationException("Не удалось определить длительность аудио.");

        return duration;
    }

    private static void ConvertWithMediaFoundation(string input, string output)
    {
        using var reader = new MediaFoundationReader(input);
        var format = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(reader, format) { ResamplerQuality = 60 };
        WaveFileWriter.CreateWaveFile(output, resampler);
    }

    private static string? FindFfmpeg()
    {
        string local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(local))
            return local;

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path == null)
            return null;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim().Trim('"'), "ffmpeg.exe");
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
