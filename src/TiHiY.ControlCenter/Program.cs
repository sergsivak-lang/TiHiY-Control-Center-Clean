using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace TiHiY.ControlCenter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private string? _xmlPath;
    private XDocument? _document;

    private readonly NumericUpDown _xDeadzone = NumberBox();
    private readonly NumericUpDown _yDeadzone = NumberBox();
    private readonly NumericUpDown _twistDeadzone = NumberBox();
    private readonly NumericUpDown _sliderDeadzone = NumberBox();
    private readonly NumericUpDown _xSaturation = NumberBox(1);
    private readonly NumericUpDown _ySaturation = NumberBox(1);
    private readonly NumericUpDown _twistSaturation = NumberBox(1);
    private readonly NumericUpDown _sliderSaturation = NumberBox(1);

    private readonly TextBox _log = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly Label _fileLabel = new() { AutoSize = true, Text = "XML: не відкрито" };

    public MainForm()
    {
        Text = "TiHiY Control Center v0.2 - XML Editor";
        Width = 1180;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        var openButton = Button("Open XML", OpenXml);
        var backupButton = Button("Backup", BackupXml);
        var defaultsButton = Button("Apply TiHiY Defaults", ApplyDefaults);
        var saveButton = Button("Save As", SaveAs);
        var refreshButton = Button("Refresh values", RefreshFromXml);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(8), AutoSize = false };
        top.Controls.AddRange([openButton, backupButton, defaultsButton, saveButton, refreshButton, _fileLabel]);

        var settings = new TableLayoutPanel { Dock = DockStyle.Top, Height = 105, ColumnCount = 8, RowCount = 2, Padding = new Padding(8) };
        for (int i = 0; i < 8; i++) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
        settings.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        settings.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        AddSetting(settings, 0, "X deadzone", _xDeadzone);
        AddSetting(settings, 1, "Y deadzone", _yDeadzone);
        AddSetting(settings, 2, "Twist deadzone", _twistDeadzone);
        AddSetting(settings, 3, "Slider deadzone", _sliderDeadzone);
        AddSetting(settings, 4, "X saturation", _xSaturation);
        AddSetting(settings, 5, "Y saturation", _ySaturation);
        AddSetting(settings, 6, "Twist saturation", _twistSaturation);
        AddSetting(settings, 7, "Slider saturation", _sliderSaturation);

        _grid.Columns.Add("Map", "Action map");
        _grid.Columns.Add("Action", "Action");
        _grid.Columns.Add("Input", "Input");
        _grid.Columns.Add("Mode", "Mode");

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var bindsTab = new TabPage("Binds") { Controls = { _grid } };
        var logTab = new TabPage("Log") { Controls = { _log } };
        tabs.TabPages.Add(bindsTab);
        tabs.TabPages.Add(logTab);

        Controls.Add(tabs);
        Controls.Add(settings);
        Controls.Add(top);

        Log("Готово. Натисни Open XML і вибери layout_300126_exported.xml");
    }

    private static NumericUpDown NumberBox(decimal value = 0)
    {
        return new NumericUpDown
        {
            DecimalPlaces = 4,
            Increment = 0.001M,
            Minimum = 0,
            Maximum = 2,
            Value = value,
            Width = 110
        };
    }

    private static Button Button(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Margin = new Padding(4) };
        b.Click += click;
        return b;
    }

    private static void AddSetting(TableLayoutPanel panel, int col, string label, Control control)
    {
        var box = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = false };
        box.Controls.Add(new Label { Text = label, AutoSize = true });
        box.Controls.Add(control);
        panel.Controls.Add(box, col, 0);
        panel.SetRowSpan(box, 2);
    }

    private void OpenXml(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Open Star Citizen XML",
            Filter = "Star Citizen XML (*.xml)|*.xml|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            _xmlPath = dlg.FileName;
            _document = XDocument.Load(_xmlPath, LoadOptions.PreserveWhitespace);
            _fileLabel.Text = "XML: " + Path.GetFileName(_xmlPath);
            RefreshFromXml(sender, e);
            Log("Відкрито XML: " + _xmlPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Open XML error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Помилка відкриття XML: " + ex.Message);
        }
    }

    private void RefreshFromXml(object? sender, EventArgs e)
    {
        if (_document is null)
        {
            Log("Спочатку відкрий XML.");
            return;
        }

        _xDeadzone.Value = Clamp(GetOption("x", "deadzone") ?? 0);
        _yDeadzone.Value = Clamp(GetOption("y", "deadzone") ?? 0);
        _twistDeadzone.Value = Clamp(GetOption("rotz", "deadzone") ?? 0);
        _sliderDeadzone.Value = Clamp(GetOption("slider1", "deadzone") ?? 0);
        _xSaturation.Value = Clamp(GetOption("x", "saturation") ?? 0);
        _ySaturation.Value = Clamp(GetOption("y", "saturation") ?? 0);
        _twistSaturation.Value = Clamp(GetOption("rotz", "saturation") ?? 0);
        _sliderSaturation.Value = Clamp(GetOption("slider1", "saturation") ?? 0);

        FillBindsGrid();
        Log("Значення з XML прочитано. Порожніх js1_ біндів: " + CountEmptyJoystickBinds());
    }

    private void ApplyDefaults(object? sender, EventArgs e)
    {
        _xDeadzone.Value = 0.018M;
        _yDeadzone.Value = 0.018M;
        _twistDeadzone.Value = 0.030M;
        _sliderDeadzone.Value = 0.020M;
        _xSaturation.Value = 0.950M;
        _ySaturation.Value = 0.950M;
        _twistSaturation.Value = 0.990M;
        _sliderSaturation.Value = 1.000M;
        Log("TiHiY HOSAM defaults застосовано в полях. Натисни Save As, щоб записати XML.");
    }

    private void BackupXml(object? sender, EventArgs e)
    {
        if (_xmlPath is null || !File.Exists(_xmlPath))
        {
            Log("Спочатку відкрий XML.");
            return;
        }

        var dir = Path.Combine(Path.GetDirectoryName(_xmlPath)!, "Backup");
        Directory.CreateDirectory(dir);
        var backup = Path.Combine(dir, Path.GetFileNameWithoutExtension(_xmlPath) + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml");
        File.Copy(_xmlPath, backup, overwrite: false);
        Log("Backup створено: " + backup);
    }

    private void SaveAs(object? sender, EventArgs e)
    {
        if (_document is null)
        {
            Log("Спочатку відкрий XML.");
            return;
        }

        try
        {
            ApplyNumbersToXml();

            using var dlg = new SaveFileDialog
            {
                Title = "Save Star Citizen XML",
                Filter = "Star Citizen XML (*.xml)|*.xml|All files (*.*)|*.*",
                FileName = "layout_TiHiY_HOSAM_v0_2.xml"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _document.Save(dlg.FileName, SaveOptions.DisableFormatting);
            Log("Збережено XML: " + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save XML error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("Помилка збереження XML: " + ex.Message);
        }
    }

    private XElement? JoystickDeviceOptions()
    {
        return _document?.Root?
            .Elements("deviceoptions")
            .FirstOrDefault(e => ((string?)e.Attribute("name") ?? string.Empty).Contains("T.16000M", StringComparison.OrdinalIgnoreCase));
    }

    private decimal? GetOption(string input, string attribute)
    {
        var device = JoystickDeviceOptions();
        var option = device?.Elements("option")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("input"), input, StringComparison.OrdinalIgnoreCase)
                              && e.Attribute(attribute) is not null);
        var raw = (string?)option?.Attribute(attribute);
        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private void SetOption(string input, string attribute, decimal value)
    {
        var device = JoystickDeviceOptions();
        if (device is null)
        {
            device = new XElement("deviceoptions", new XAttribute("name", "T.16000M"));
            _document!.Root!.AddFirst(device);
        }

        var option = device.Elements("option")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("input"), input, StringComparison.OrdinalIgnoreCase)
                              && e.Attribute(attribute) is not null);

        if (option is null)
        {
            option = new XElement("option", new XAttribute("input", input));
            device.Add(option);
        }

        option.SetAttributeValue(attribute, value.ToString("0.####", CultureInfo.InvariantCulture));
    }

    private void ApplyNumbersToXml()
    {
        SetOption("x", "deadzone", _xDeadzone.Value);
        SetOption("y", "deadzone", _yDeadzone.Value);
        SetOption("rotz", "deadzone", _twistDeadzone.Value);
        SetOption("slider1", "deadzone", _sliderDeadzone.Value);
        SetOption("x", "saturation", _xSaturation.Value);
        SetOption("y", "saturation", _ySaturation.Value);
        SetOption("rotz", "saturation", _twistSaturation.Value);
        SetOption("slider1", "saturation", _sliderSaturation.Value);
    }

    private void FillBindsGrid()
    {
        _grid.Rows.Clear();
        if (_document?.Root is null) return;

        foreach (var map in _document.Root.Elements("actionmap"))
        {
            var mapName = (string?)map.Attribute("name") ?? "";
            foreach (var action in map.Elements("action"))
            {
                var actionName = (string?)action.Attribute("name") ?? "";
                foreach (var rebind in action.Elements("rebind"))
                {
                    var input = (string?)rebind.Attribute("input") ?? "";
                    var mode = (string?)rebind.Attribute("activationMode") ?? (string?)rebind.Attribute("multiTap") ?? "";
                    _grid.Rows.Add(mapName, actionName, input, mode);
                }
            }
        }
    }

    private int CountEmptyJoystickBinds()
    {
        return _document?.Descendants("rebind").Count(r => ((string?)r.Attribute("input") ?? "").Trim() == "js1_") ?? 0;
    }

    private static decimal Clamp(decimal value)
    {
        if (value < 0) return 0;
        if (value > 2) return 2;
        return value;
    }

    private void Log(string text)
    {
        _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
    }
}
