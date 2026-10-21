namespace Transcriber;

/// <summary>
/// Скачивает и хранит модели Whisper (формат ggml) в %LocalAppData%\Transcriber\models.
/// </summary>
public static class ModelManager
{
    // Ключ в выпадающем списке -> имя файла в репозитории whisper.cpp
    private static readonly Dictionary<string, (string File, long MinBytes)> Models = new()
    {
        ["turbo"] = ("ggml-large-v3-turbo.bin", 1_400_000_000),
        ["small"] = ("ggml-small.bin", 400_000_000),
        ["base"]  = ("ggml-base.bin", 100_000_000),
    };

    private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static string ModelsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Transcriber",
        "models");

    public static string[] ModelKeys => Models.Keys.ToArray();

    /// <summary>Возвращает путь к модели; при необходимости скачивает её.</summary>
    public static async Task<string> EnsureModelAsync(
        string modelKey,
        IProgress<ProgressInfo> progress,
        CancellationToken ct)
    {
        if (!Models.TryGetValue(modelKey, out var info))
            throw new ArgumentException($"Неизвестная модель: {modelKey}");

        Directory.CreateDirectory(ModelsDirectory);

        string path = Path.Combine(ModelsDirectory, info.File);

        // Файл уже есть и выглядит целым
        if (File.Exists(path) && new FileInfo(path).Length >= info.MinBytes)
            return path;

        string partPath = path + ".part";

        progress.Report(new ProgressInfo(0, $"Скачивание модели {modelKey}...", TimeSpan.Zero, TimeSpan.Zero, ""));

        using var response = await Http.GetAsync(BaseUrl + info.File, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? 0;

        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var target = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, true))
        {
            var buffer = new byte[1 << 20];
            long done = 0;
            int lastPercent = -1;
            int read;

            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;

                int percent = total > 0 ? (int)(done * 100 / total) : 0;
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report(new ProgressInfo(
                        percent,
                        $"Скачивание модели {modelKey}: {done / 1048576} / {total / 1048576} МБ",
                        TimeSpan.Zero, TimeSpan.Zero, "Это нужно только один раз"));
                }
            }
        }

        if (File.Exists(path))
            File.Delete(path);
        File.Move(partPath, path);

        return path;
    }
}
