using System.Diagnostics;
using System.Drawing;

namespace Transcriber;

public sealed class MainForm : Form
{
    private const string SupportedFilter =
        "Аудио и видео|*.mp3;*.wav;*.m4a;*.mp4;*.avi;*.mkv;*.mov;*.flac;*.ogg|" +
        "Все файлы|*.*";

    private readonly TranscriptionService _service = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private CancellationTokenSource? _cts;
    private ThemePalette _palette = null!;

    private string? _selectedFile;
    private string? _outputFile;
    private bool _isRunning;

    private readonly TextBox _fileBox = new();
    private readonly Button _selectButton = new();
    private readonly ComboBox _modelCombo = new();
    private readonly Button _settingsButton = new();
    private readonly Button _startButton = new();
    private readonly Label _statusLabel = new();
    private readonly Label _timeLabel = new();
    private readonly Label _etaLabel = new();
    private readonly Label _percentLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _resultLabel = new();
    private readonly FlowLayoutPanel _resultButtons = new();
    private readonly Button _openTxtButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Panel _dropPanel = new();
    private readonly List<Panel> _surfaces = [];
    private readonly List<Label> _mutedLabels = [];
    private readonly List<Button> _accentButtons = [];
    private readonly List<Button> _secondaryButtons = [];

    public MainForm()
    {
        Text = "Transcriber";
        ClientSize = new Size(820, 750);
        MinimumSize = new Size(740, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        AllowDrop = true;
        DragEnter += MainDragEnter;
        DragDrop += MainDragDrop;

        BuildInterface();
        ApplyTheme();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(34, 24, 34, 26),
            BackColor = Color.Transparent,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var brand = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        brand.Controls.Add(new Label
        {
            Text = "Transcriber",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold),
            Margin = new Padding(0),
            Tag = "primary",
        });
        var subtitle = new Label
        {
            Text = "Превратите запись в удобный текст",
            AutoSize = true,
            Margin = new Padding(1, 0, 0, 0),
            Tag = "muted",
        };
        _mutedLabels.Add(subtitle);
        brand.Controls.Add(subtitle);
        header.Controls.Add(brand, 0, 0);

        _settingsButton.Text = "⚙  Настройки";
        StyleButton(_settingsButton, false);
        _settingsButton.Click += (_, _) => OpenSettings();
        header.Controls.Add(_settingsButton, 1, 0);
        root.Controls.Add(header, 0, 0);

        _dropPanel.Dock = DockStyle.Fill;
        _dropPanel.Padding = new Padding(22);
        _dropPanel.Margin = new Padding(0, 8, 0, 8);
        _dropPanel.AllowDrop = true;
        _dropPanel.DragEnter += MainDragEnter;
        _dropPanel.DragDrop += MainDragDrop;
        _surfaces.Add(_dropPanel);
        root.Controls.Add(_dropPanel, 0, 1);

        var dropLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        dropLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dropLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        dropLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dropLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _dropPanel.Controls.Add(dropLayout);
        var fileTitle = new Label
        {
            Text = "ФАЙЛ ДЛЯ РАСШИФРОВКИ",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12),
            Tag = "muted",
        };
        _mutedLabels.Add(fileTitle);
        dropLayout.Controls.Add(fileTitle, 0, 0);

        _fileBox.ReadOnly = true;
        _fileBox.BorderStyle = BorderStyle.None;
        _fileBox.Dock = DockStyle.Fill;
        _fileBox.Font = new Font("Segoe UI", 11f);
        _fileBox.Text = "Перетащите аудио или видео сюда";
        _fileBox.Margin = new Padding(0, 2, 0, 10);
        dropLayout.Controls.Add(_fileBox, 0, 1);

        var fileButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        _selectButton.Text = "Выбрать файл";
        StyleButton(_selectButton, true);
        _selectButton.Click += (_, _) => SelectFile();
        fileButtons.Controls.Add(_selectButton);
        var formats = new Label
        {
            Text = "MP3 · WAV · M4A · MP4 · MKV · MOV · FLAC · OGG",
            AutoSize = true,
            Margin = new Padding(13, 9, 0, 0),
            Tag = "muted",
        };
        _mutedLabels.Add(formats);
        fileButtons.Controls.Add(formats);
        dropLayout.Controls.Add(fileButtons, 0, 2);

        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 12, 0, 0),
        };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        options.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        options.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        var modelLabel = new Label
        {
            Text = "Модель",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Margin = new Padding(0, 9, 12, 0),
            Tag = "primary",
        };
        options.Controls.Add(modelLabel, 0, 0);
        _modelCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _modelCombo.Items.AddRange(ModelManager.ModelKeys.Cast<object>().ToArray());
        _modelCombo.SelectedItem = ModelManager.ModelKeys.Contains(_settings.Model) ? _settings.Model : "turbo";
        _modelCombo.Width = 130;
        _modelCombo.FlatStyle = FlatStyle.Flat;
        _modelCombo.SelectedIndexChanged += (_, _) => SaveModelPreference();
        options.Controls.Add(_modelCombo, 1, 0);

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        _startButton.Text = "Начать расшифровку";
        StyleButton(_startButton, true);
        _startButton.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        _startButton.Padding = new Padding(20, 10, 20, 10);
        _startButton.Click += async (_, _) => await StartTranscriptionAsync();
        actionRow.Controls.Add(_startButton);

        var chosen = new Label
        {
            AutoSize = true,
            Margin = new Padding(14, 14, 0, 0),
            Tag = "muted",
        };
        chosen.Name = "deviceSummary";
        _mutedLabels.Add(chosen);
        actionRow.Controls.Add(chosen);
        options.Controls.Add(actionRow, 0, 1);
        options.SetColumnSpan(actionRow, 2);
        root.Controls.Add(options, 0, 2);

        var progressCard = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 16, 20, 14),
            Margin = new Padding(0, 8, 0, 8),
        };
        _surfaces.Add(progressCard);
        root.Controls.Add(progressCard, 0, 3);
        var progressLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        progressLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        progressLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        progressLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        progressLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        progressCard.Controls.Add(progressLayout);

        _statusLabel.Text = "Готово к работе";
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Margin = new Padding(0, 0, 0, 7);
        progressLayout.Controls.Add(_statusLabel, 0, 0);
        _percentLabel.Text = "0%";
        _percentLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _percentLabel.TextAlign = ContentAlignment.MiddleRight;
        _percentLabel.Dock = DockStyle.Fill;
        progressLayout.Controls.Add(_percentLabel, 1, 0);
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(0, 0, 0, 8);
        progressLayout.Controls.Add(_progress, 0, 1);
        progressLayout.SetColumnSpan(_progress, 2);

        var infoRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        _timeLabel.Text = "0:00 / 0:00";
        _timeLabel.AutoSize = true;
        _timeLabel.Margin = new Padding(0, 0, 14, 0);
        _timeLabel.Tag = "muted";
        _mutedLabels.Add(_timeLabel);
        infoRow.Controls.Add(_timeLabel);
        _etaLabel.AutoSize = true;
        _etaLabel.Margin = new Padding(0);
        _etaLabel.Tag = "muted";
        _mutedLabels.Add(_etaLabel);
        infoRow.Controls.Add(_etaLabel);
        progressLayout.Controls.Add(infoRow, 0, 2);
        progressLayout.SetColumnSpan(infoRow, 2);

        _resultCard.Dock = DockStyle.Fill;
        _resultCard.Padding = new Padding(20, 16, 20, 12);
        _resultCard.Margin = new Padding(0);
        _resultCard.Visible = false;
        root.Controls.Add(_resultCard, 0, 4);
        _surfaces.Add(_resultCard);
        var resultLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        _resultCard.Controls.Add(resultLayout);
        _resultLabel.AutoSize = true;
        _resultLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        resultLayout.Controls.Add(_resultLabel);
        _resultButtons.AutoSize = true;
        _resultButtons.WrapContents = false;
        _resultButtons.FlowDirection = FlowDirection.LeftToRight;
        _resultButtons.BackColor = Color.Transparent;
        _resultButtons.Margin = new Padding(0, 12, 0, 0);
        StyleButton(_openTxtButton, true);
        _openTxtButton.Text = "Открыть текст";
        _openTxtButton.Click += (_, _) => OpenPath(_outputFile);
        StyleButton(_openFolderButton, false);
        _openFolderButton.Text = "Показать в папке";
        _openFolderButton.Click += (_, _) => OpenPath(Path.GetDirectoryName(_outputFile ?? ""));
        _resultButtons.Controls.Add(_openTxtButton);
        _resultButtons.Controls.Add(_openFolderButton);
        resultLayout.Controls.Add(_resultButtons);
    }

    private readonly Panel _resultCard = new();

    private void StyleButton(Button button, bool accent)
    {
        button.AutoSize = true;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        button.Padding = new Padding(14, 7, 14, 7);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(0, 0, 8, 0);
        if (accent) _accentButtons.Add(button); else _secondaryButtons.Add(button);
    }

    private void ApplyTheme()
    {
        _palette = ThemePalette.For(_settings.Theme);
        BackColor = _palette.Window;
        ForeColor = _palette.Text;
        foreach (var panel in _surfaces)
        {
            panel.BackColor = _palette.Surface;
            panel.Paint -= DrawCardBorder;
            panel.Paint += DrawCardBorder;
        }
        _fileBox.BackColor = _palette.Surface;
        _fileBox.ForeColor = _selectedFile == null ? _palette.Muted : _palette.Text;
        _modelCombo.BackColor = _palette.Input;
        _modelCombo.ForeColor = _palette.Text;
        foreach (var label in _mutedLabels) label.ForeColor = _palette.Muted;
        foreach (var label in ControlsOfType<Label>(this))
            if (Equals(label.Tag, "primary")) label.ForeColor = _palette.Text;
        foreach (var button in _accentButtons)
        {
            button.BackColor = _palette.Accent;
            button.ForeColor = _palette.AccentText;
        }
        foreach (var button in _secondaryButtons)
        {
            button.BackColor = _palette.SurfaceAlt;
            button.ForeColor = _palette.Text;
        }
        _resultLabel.ForeColor = _palette.Accent;
        _statusLabel.ForeColor = _palette.Text;
        _percentLabel.ForeColor = _palette.Text;
        foreach (var control in ControlsOfType<Control>(this))
            if (control is Panel or TableLayoutPanel or FlowLayoutPanel)
                control.BackColor = Color.Transparent;
        foreach (var panel in _surfaces) panel.BackColor = _palette.Surface;
        foreach (var label in _mutedLabels) label.ForeColor = _palette.Muted;
        UpdateDeviceSummary();
    }

    private void DrawCardBorder(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel) return;
        using var pen = new Pen(_palette.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
    }

    private static IEnumerable<T> ControlsOfType<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T match) yield return match;
            foreach (var nested in ControlsOfType<T>(child)) yield return nested;
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _settings.Save();
        ApplyTheme();
    }

    private void SaveModelPreference()
    {
        if (_modelCombo.SelectedItem is string model)
        {
            _settings.Model = model;
            try { _settings.Save(); } catch { }
        }
    }

    private void UpdateDeviceSummary()
    {
        var label = ControlsOfType<Label>(this).FirstOrDefault(x => x.Name == "deviceSummary");
        if (label != null)
        {
            label.Text = _settings.ComputeDevice == ComputeDevice.Gpu ? "• Видеокарта" : "• Процессор";
            label.ForeColor = _palette.Muted;
        }
    }

    private void MainDragEnter(object? sender, DragEventArgs e)
    {
        if (!_isRunning && e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void MainDragDrop(object? sender, DragEventArgs e)
    {
        if (_isRunning || e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        SetSelectedFile(files[0]);
    }

    private void SelectFile()
    {
        if (_isRunning) return;
        using var dialog = new OpenFileDialog { Title = "Выберите аудио или видео", Filter = SupportedFilter };
        if (dialog.ShowDialog(this) == DialogResult.OK) SetSelectedFile(dialog.FileName);
    }

    private void SetSelectedFile(string path)
    {
        if (!File.Exists(path)) return;
        _selectedFile = path;
        _outputFile = null;
        _fileBox.Text = Path.GetFileName(path);
        _fileBox.ForeColor = _palette.Text;
        _resultCard.Visible = false;
        ResetProgress();
        _statusLabel.Text = "Файл готов к расшифровке";
    }

    private void ResetProgress()
    {
        _progress.Value = 0;
        _percentLabel.Text = "0%";
        _timeLabel.Text = "0:00 / 0:00";
        _etaLabel.Text = "";
    }

    private async Task StartTranscriptionAsync()
    {
        if (_isRunning) return;
        if (_selectedFile == null)
        {
            _statusLabel.Text = "Сначала выберите аудио или видеофайл";
            return;
        }

        SetRunning(true);
        _resultCard.Visible = false;
        ResetProgress();
        _statusLabel.Text = "Подготовка Whisper…";
        _cts = new CancellationTokenSource();
        var progress = new Progress<ProgressInfo>(ApplyProgress);

        try
        {
            string model = (string)_modelCombo.SelectedItem!;
            string file = _selectedFile;
            ComputeDevice device = _settings.ComputeDevice;
            _outputFile = await Task.Run(() => _service.RunAsync(file, model, device, progress, _cts.Token));
            OnFinished();
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed) SetRunning(false);
        }
        catch (Exception ex)
        {
            OnError(ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void SetRunning(bool running)
    {
        _isRunning = running;
        _startButton.Enabled = !running;
        _selectButton.Enabled = !running;
        _modelCombo.Enabled = !running;
        _settingsButton.Enabled = !running;
        if (running) _startButton.Text = "Идёт расшифровка…";
        else _startButton.Text = "Начать расшифровку";
    }

    private void ApplyProgress(ProgressInfo info)
    {
        if (IsDisposed) return;
        _progress.Value = Math.Clamp(info.Percent, 0, 100);
        _percentLabel.Text = $"{_progress.Value}%";
        _statusLabel.Text = info.Status;
        if (info.Total > TimeSpan.Zero)
            _timeLabel.Text = $"{TextProcessor.FormatDuration(info.Current.TotalSeconds)} / {TextProcessor.FormatDuration(info.Total.TotalSeconds)}";
        if (!string.IsNullOrEmpty(info.Eta)) _etaLabel.Text = info.Eta;
    }

    private void OnFinished()
    {
        SetRunning(false);
        _progress.Value = 100;
        _percentLabel.Text = "100%";
        _statusLabel.Text = "Расшифровка завершена";
        _etaLabel.Text = "Готово";
        _resultLabel.Text = "✓  Текст сохранён: " + Path.GetFileName(_outputFile);
        _resultCard.Visible = true;
    }

    private void OnError(string message)
    {
        SetRunning(false);
        _statusLabel.Text = "Не удалось обработать файл";
        MessageBox.Show(this, message, "Ошибка расшифровки", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !(File.Exists(path) || Directory.Exists(path))) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _service.Dispose();
        base.Dispose(disposing);
    }
}
