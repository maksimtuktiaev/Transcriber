using System.Drawing;

namespace Transcriber;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly ThemePalette _palette;
    private readonly ComboBox _theme = new();
    private readonly ComboBox _device = new();
    private readonly Label _deviceHint = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        _palette = ThemePalette.For(settings.Theme);

        Text = "Настройки Transcriber";
        ClientSize = new Size(460, 430);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = _palette.Window;
        ForeColor = _palette.Text;
        Font = new Font("Segoe UI", 10f);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(28, 22, 28, 20),
            BackColor = _palette.Window,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        Add(layout, new Label
        {
            Text = "Настройки",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
            ForeColor = _palette.Text,
            Margin = new Padding(0, 0, 0, 3),
        });
        Add(layout, new Label
        {
            Text = "Внешний вид и производительность",
            AutoSize = true,
            ForeColor = _palette.Muted,
            Margin = new Padding(0, 0, 0, 18),
        });

        AddField(layout, "Тема приложения", _theme, ["Как в Windows", "Светлая", "Тёмная"],
            settings.Theme switch { AppTheme.Light => 1, AppTheme.Dark => 2, _ => 0 });

        AddField(layout, "Устройство распознавания", _device,
            ["Видеокарта (CUDA)", "Процессор (CPU)"],
            settings.ComputeDevice == ComputeDevice.Cpu ? 1 : 0);

        _deviceHint.Text = "GPU быстрее; CPU подойдёт, если видеокарта недоступна.";
        _deviceHint.AutoSize = true;
        _deviceHint.ForeColor = _palette.Muted;
        _deviceHint.Margin = new Padding(0, 0, 0, 16);
        Add(layout, _deviceHint);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = _palette.Window,
            Margin = new Padding(0, 8, 0, 0),
        };
        var save = MakeButton("Сохранить", _palette.Accent, _palette.AccentText);
        save.Click += (_, _) =>
        {
            _settings.Theme = _theme.SelectedIndex switch
            {
                1 => AppTheme.Light,
                2 => AppTheme.Dark,
                _ => AppTheme.System,
            };
            _settings.ComputeDevice = _device.SelectedIndex == 1 ? ComputeDevice.Cpu : ComputeDevice.Gpu;
            DialogResult = DialogResult.OK;
            Close();
        };
        var cancel = MakeButton("Отмена", _palette.SurfaceAlt, _palette.Text);
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void AddField(TableLayoutPanel layout, string title, ComboBox combo, string[] items, int selected)
    {
        var label = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            ForeColor = _palette.Text,
            Margin = new Padding(0, 4, 0, 7),
        };
        Add(layout, label);

        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.AddRange(items);
        combo.SelectedIndex = selected;
        combo.Dock = DockStyle.Top;
        combo.Height = 36;
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = _palette.Input;
        combo.ForeColor = _palette.Text;
        combo.Margin = new Padding(0, 0, 0, 15);
        Add(layout, combo);
    }

    private static void Add(TableLayoutPanel layout, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(control);
    }

    private static Button MakeButton(string text, Color back, Color fore) => new()
    {
        Text = text,
        AutoSize = true,
        FlatStyle = FlatStyle.Flat,
        BackColor = back,
        ForeColor = fore,
        Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
        Padding = new Padding(14, 7, 14, 7),
        Margin = new Padding(8, 0, 0, 0),
        Cursor = Cursors.Hand,
    };
}
