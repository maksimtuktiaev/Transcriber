using System.Text.Json;
using Whisper.net.LibraryLoader;

namespace Transcriber;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public enum ComputeDevice
{
    Gpu,
    Cpu
}

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppTheme Theme { get; set; } = AppTheme.System;
    public ComputeDevice ComputeDevice { get; set; } = ComputeDevice.Gpu;
    public string Model { get; set; } = "turbo";

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Transcriber", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { /* Повреждённые настройки заменяются значениями по умолчанию. */ }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static void ConfigureRuntime(ComputeDevice device)
    {
        RuntimeOptions.RuntimeLibraryOrder = device == ComputeDevice.Cpu
            ? [RuntimeLibrary.Cpu]
            : [RuntimeLibrary.Cuda, RuntimeLibrary.Cuda12];
    }
}
