using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Transcriber;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly TranscriptionService _service = new();
    private string? _inputPath;
    private string? _outputPath;
    private CancellationTokenSource? _cancellation;

    public MainWindow()
    {
        InitializeComponent();
        App.ApplyTheme(_settings.Theme);
        ModelBox.SelectedItem = ModelBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => (string?)item.Content == _settings.Model) ?? ModelBox.Items[0];
        DeviceLabel.Text = "Устройство: " + DeviceName(_settings.ComputeDevice);
        Closed += (_, _) => _service.Dispose();
    }

    private async void ChooseFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите аудио- или видеофайл",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Медиафайлы")
            {
                Patterns = ["*.mp3", "*.wav", "*.m4a", "*.mp4", "*.mkv", "*.mov", "*.flac", "*.ogg", "*.wma", "*.aac"]
            }, FilePickerFileTypes.All]
        });

        if (files.Count == 0) return;
        _inputPath = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(_inputPath))
        {
            ShowError("Не удалось получить путь к выбранному файлу.");
            return;
        }
        FileLabel.Text = Path.GetFileName(_inputPath);
        ErrorLabel.IsVisible = false;
        ResultCard.IsVisible = false;
    }

    private async void Start_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_inputPath == null)
        {
            ShowError("Сначала выберите аудио- или видеофайл.");
            return;
        }

        _settings.Model = (ModelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "turbo";
        _settings.Save();
        _cancellation = new CancellationTokenSource();
        SetBusy(true);
        ResultCard.IsVisible = false;
        ErrorLabel.IsVisible = false;
        Progress.Value = 0;

        var progress = new Progress<ProgressInfo>(info =>
        {
            Progress.Value = info.Percent;
            PercentLabel.Text = info.Percent > 0 ? $"{info.Percent}%" : string.Empty;
            StatusLabel.Text = info.Status;
            EtaLabel.Text = info.Eta;
        });

        try
        {
            _outputPath = await _service.RunAsync(_inputPath, _settings.Model, _settings.ComputeDevice, progress, _cancellation.Token);
            Progress.Value = 100;
            PercentLabel.Text = "100%";
            StatusLabel.Text = "Расшифровка завершена";
            EtaLabel.Text = "Готово";
            SavedLabel.Text = "✓  Текст сохранён: " + Path.GetFileName(_outputPath);
            ResultCard.IsVisible = true;
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Расшифровка отменена";
            EtaLabel.Text = "Можно запустить снова";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Не удалось завершить расшифровку";
            ShowError(ex.Message);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            SetBusy(false);
        }
    }

    private void Settings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings);
        dialog.ShowDialog(this);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _cancellation?.Cancel();

    private void OpenText_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_outputPath != null) OpenWithDefaultApp(_outputPath);
    }

    private void OpenFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_outputPath != null) OpenWithDefaultApp(Path.GetDirectoryName(_outputPath)!);
    }

    private void SetBusy(bool busy)
    {
        CancelButton.IsVisible = busy;
        ModelBox.IsEnabled = !busy;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private static string DeviceName(ComputeDevice device) => device switch
    {
        ComputeDevice.Cpu => "Процессор",
        ComputeDevice.Apple => "Apple Core ML",
        _ => "NVIDIA CUDA"
    };

    private static void OpenWithDefaultApp(string path)
    {
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsMacOS() ? "open" : OperatingSystem.IsLinux() ? "xdg-open" : path,
            UseShellExecute = !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()
        };
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()) start.ArgumentList.Add(path);
        Process.Start(start);
    }
}
