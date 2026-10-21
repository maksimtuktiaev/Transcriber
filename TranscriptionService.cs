using System.Diagnostics;
using System.Text;
using Whisper.net;

namespace Transcriber;

public sealed record ProgressInfo(
    int Percent,
    string Status,
    TimeSpan Current,
    TimeSpan Total,
    string Eta);

/// <summary>
/// Вся логика транскрибации (аналог transcription_worker из Python-версии).
/// Модель кэшируется между запусками, как self.model в оригинале.
/// </summary>
public sealed class TranscriptionService : IDisposable
{
    private WhisperFactory? _factory;
    private string? _currentModel;

    public async Task<string> RunAsync(
        string inputFile,
        string modelKey,
        ComputeDevice device,
        IProgress<ProgressInfo> progress,
        CancellationToken ct)
    {
        // 1. Модель (скачивание при первом запуске)
        string modelPath = await ModelManager.EnsureModelAsync(modelKey, progress, ct);

        Report(progress, 0, $"Загрузка Whisper: {modelKey}...");
        string factoryKey = $"{modelKey}:{device}";
        if (_factory == null || _currentModel != factoryKey)
        {
            _factory?.Dispose();
            _factory = null;

            try
            {
                AppSettings.ConfigureRuntime(device);
                _factory = await Task.Run(() => WhisperFactory.FromPath(modelPath), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    (device == ComputeDevice.Apple
                        ? "Не удалось запустить Whisper с ускорением Apple Core ML. Попробуйте выбрать процессор в настройках.\n\n"
                        : device == ComputeDevice.Gpu
                        ? "Не удалось запустить Whisper на видеокарте. Проверьте драйвер и установку CUDA; " +
                          "для модели turbo нужно около 4–6 ГБ видеопамяти, попробуйте small.\n\n"
                        : "Не удалось запустить Whisper на процессоре. Попробуйте выбрать видеокарту в настройках.\n\n") +
                    "Техническая ошибка: " + ex.Message, ex);
            }
            _currentModel = factoryKey;
        }

        // 2. Аудио -> WAV 16 кГц
        Report(progress, 0, "Подготовка аудио...");
        var (wavPath, duration) = await AudioConverter.ToWhisperWavAsync(inputFile, ct);

        try
        {
            // 3. Распознавание
            Report(progress, 0, "Транскрибация...");

            var parts = new List<string>();
            var stopwatch = Stopwatch.StartNew();
            long lastUpdateMs = -1000;
            double total = duration.TotalSeconds;

            await using var processor = _factory
                .CreateBuilder()
                .WithLanguage("ru")
                .WithThreads(Math.Max(1, Environment.ProcessorCount - 1))
                .Build();

            await using var wavStream = File.OpenRead(wavPath);

            await foreach (var segment in processor.ProcessAsync(wavStream, ct))
            {
                string text = segment.Text.Trim();

                if (text.Length > 0 && !TextProcessor.IsNonSpeech(text))
                    parts.Add(text);

                double current = Math.Min(segment.End.TotalSeconds, total);
                double percent = Math.Clamp(current / total * 100, 0, 100);

                long now = stopwatch.ElapsedMilliseconds;
                if (now - lastUpdateMs < 200 && percent < 100)
                    continue;
                lastUpdateMs = now;

                string eta;
                if (current > 1)
                {
                    double speed = current / stopwatch.Elapsed.TotalSeconds;
                    double remaining = (total - current) / speed;
                    eta = "Осталось примерно: " + TextProcessor.FormatDuration(remaining);
                }
                else
                {
                    eta = "Рассчитываем время...";
                }

                progress.Report(new ProgressInfo(
                    (int)percent,
                    $"Транскрибация: {TextProcessor.FormatDuration(current)} / {TextProcessor.FormatDuration(total)}",
                    TimeSpan.FromSeconds(current),
                    duration,
                    eta));
            }

            progress.Report(new ProgressInfo(100, "Транскрибация завершена.", duration, duration, "Обработка текста..."));

            // 4. Очистка текста (пунктуацию Whisper ставит сам)
            string finalText = TextProcessor.FormatTranscript(parts);

            // 5. Сохранение рядом с исходным файлом
            Report(progress, 100, "Сохранение TXT...");
            string txtPath = Path.ChangeExtension(inputFile, ".txt");
            await File.WriteAllTextAsync(txtPath, finalText, new UTF8Encoding(false), ct);

            return txtPath;
        }
        finally
        {
            AudioConverter.TryDelete(wavPath);
        }
    }

    private static void Report(IProgress<ProgressInfo> progress, int percent, string status) =>
        progress.Report(new ProgressInfo(percent, status, TimeSpan.Zero, TimeSpan.Zero, ""));

    public void Dispose()
    {
        _factory?.Dispose();
        _factory = null;
    }
}
