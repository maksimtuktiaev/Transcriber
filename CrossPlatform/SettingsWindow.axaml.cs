using Avalonia.Controls;

namespace Transcriber;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ThemeBox.ItemsSource = new[] { "Системная", "Светлая", "Тёмная" };
        ThemeBox.SelectedIndex = (int)settings.Theme;

        string[] devices;
        if (OperatingSystem.IsMacOS())
        {
            devices = ["Apple Core ML", "Процессор"];
            DeviceBox.SelectedIndex = settings.ComputeDevice == ComputeDevice.Cpu ? 1 : 0;
            HintLabel.Text = "Core ML использует ускорение Apple Silicon и поддерживается на Mac с Intel. При недоступности ускорителя приложение перейдёт на процессор.";
        }
        else if (OperatingSystem.IsLinux())
        {
            devices = ["Видеокарта NVIDIA (CUDA)", "Процессор"];
            DeviceBox.SelectedIndex = settings.ComputeDevice == ComputeDevice.Cpu ? 1 : 0;
            HintLabel.Text = "Для CUDA требуется совместимая видеокарта NVIDIA и установленный драйвер. Без CUDA выберите процессор.";
        }
        else
        {
            devices = ["Видеокарта NVIDIA (CUDA)", "Процессор"];
            DeviceBox.SelectedIndex = settings.ComputeDevice == ComputeDevice.Cpu ? 1 : 0;
            HintLabel.Text = "GPU режим использует NVIDIA CUDA, CPU режим подойдёт для совместимых Windows и Linux систем.";
        }
        DeviceBox.ItemsSource = devices;
    }

    private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settings.Theme = (AppTheme)Math.Max(0, ThemeBox.SelectedIndex);
        bool cpu = DeviceBox.SelectedIndex == 1;
        _settings.ComputeDevice = cpu ? ComputeDevice.Cpu : OperatingSystem.IsMacOS() ? ComputeDevice.Apple : ComputeDevice.Gpu;
        _settings.Save();
        App.ApplyTheme(_settings.Theme);
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
