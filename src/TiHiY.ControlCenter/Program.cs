using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace TiHiY.ControlCenter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private readonly TextBox _log = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, ReadOnly = true };
    private readonly NumericUpDown _deadzoneX = new() { DecimalPlaces = 4, Increment = 0.001M, Maximum = 1, Width = 90 };
    private readonly NumericUpDown _deadzoneY = new() { DecimalPlaces = 4, Increment = 0.001M, Maximum = 1, Width = 90 };
    private readonly NumericUpDown _deadzoneTwist = new() { DecimalPlaces = 4, Increment = 0.001M, Maximum = 1, Width = 90 };
    private readonly NumericUpDown _satX = new() { DecimalPlaces = 4, Increment = 0.001M, Maximum = 1, Width = 90 };
    private readonly NumericUpDown _satY = new() { DecimalPlaces = 4, Increment = 0.001M, Maximum = 1, Width = 90 };
    private readonly NumericUpDown _satSlider = new() { DecimalPlaces = 4, Increment = 0.001M, Maximum = 1, Width = 90 };
    private XDocument? _doc;
    private string? _path;

    public MainForm()
    {
        Text = "TiHiY Control Center v0.1";
        Width = 1000;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
        top.Controls.Add(Button("Open XML", OpenXml));
        top.Controls.Add(Button("Backup", BackupXml));
        top.Controls.Add(Button("Apply TiHiY Defaults", ApplyDefaults));
        top.Controls.Add(Button("Save As", SaveAs));

        var settings = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 95, Padding = new Padding(8) };
        AddSetting(settings, "X deadzone", _deadzoneX);
        AddSetting(settings, "Y deadzone", _deadzoneY);
        AddSetting(settings, "Twist deadzone", _deadzoneTwist);
        AddSetting(settings, "X saturation", _satX);
        AddSetting(settings, "Y saturation", _satY);
        AddSetting(settings, "Slider saturation", _satSlider);

        Controls.Add(_log);
        Controls.Add(settings);
        Controls.Add(top);
        Log("Готово. Натисни Open XML і вибери layout_300126_exported.xml");
    }

    private static Button Button(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(4) };
        b.Click += click;
        return b;
    }

    private static void AddSetting(FlowLayoutPanel panel, string label, NumericUpDown value)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
        panel.Controls.Add(value);
    }

    private void OpenXml(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "Star Citizen XML (*.xml)|*.xml|All files (*.*)|*.*" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _path = dlg.FileName;
        _doc = XDocument.Load(_path, LoadOptions.PreserveWhitespace);
        ReadCurrentValues();
        Analyze();
    }

    private void BackupXml(object? sender, EventArgs e)
    {
        if (_path is null || !File.Exists(_path)) { Log("Спочатку відкрий XML."); return; }
        var backup = Path.Combine(Path.GetDirectoryName(_path)!, Path.GetFileNameWithoutExtension(_path) + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml");
        File.Copy(_path, backup);
        Log("Backup створено: " + backup);
    }

    private void ApplyDefaults(object? sender, EventArgs e)
    {
        if (_doc is null) { Log("Спочатку відкрий XML."); return; }
        _deadzoneX.Value = 0.018M;
        _deadzoneY.Value = 0.018M;
        _deadzoneTwist.Value = 0.030M;
        _satX.Value = 0.950M;
        _satY.Value = 0.950M;
        _satSlider.Value = 1.000M;
        WriteOption("x", "deadzone", _deadzoneX.Value);
        WriteOption("y", "deadzone", _deadzoneY.Value);
        WriteOption("rotz", "deadzone", _deadzoneTwist.Value);
        WriteOption("x", "saturation", _satX.Value);
        WriteOption("y", "saturation", _satY.Value);
        WriteOption("slider1", "saturation", _satSlider.Value);
        Log("TiHiY defaults застосовано в пам'яті. Натисни Save As.");
    }

    private void SaveAs(object? sender, EventArgs e)
    {
        if (_doc is null) { Log("Спочатку відкрий XML."); return; }
        using var dlg = new SaveFileDialog { Filter = "Star Citizen XML (*.xml)|*.xml", FileName = "TiHiY_Universal_HOSAM.xml" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _doc.Save(dlg.FileName);
        Log("Збережено: " + dlg.FileName);
    }

    private void ReadCurrentValues()
    {
        _deadzoneX.Value = ReadDecimal("x", "deadzone", 0);
        _deadzoneY.Value = ReadDecimal("y", "deadzone", 0);
        _deadzoneTwist.Value = ReadDecimal("rotz", "deadzone", 0);
        _satX.Value = ReadDecimal("x", "saturation", 1);
        _satY.Value = ReadDecimal("y", "saturation", 1);
        _satSlider.Value = ReadDecimal("slider1", "saturation", 1);
    }

    private decimal ReadDecimal(string input, string attr, decimal fallback)
    {
        var value = _doc?.Descendants("option").FirstOrDefault(x => (string?)x.Attribute("input") == input && x.Attribute(attr) != null)?.Attribute(attr)?.Value;
        return decimal.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : fallback;
    }

    private void WriteOption(string input, string attr, decimal value)
    {
        if (_doc?.Root is null) return;
        var option = _doc.Descendants("option").FirstOrDefault(x => (string?)x.Attribute("input") == input && x.Attribute(attr) != null);
        if (option is null)
        {
            var deviceOptions = _doc.Descendants("deviceoptions").FirstOrDefault(x => ((string?)x.Attribute("name"))?.Contains("T.16000M") == true);
            if (deviceOptions is null) return;
            option = new XElement("option", new XAttribute("input", input));
            deviceOptions.Add(option);
        }
        option.SetAttributeValue(attr, value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void Analyze()
    {
        if (_doc is null) return;
        var rebinds = _doc.Descendants("rebind").ToList();
        var empty = rebinds.Count(x => ((string?)x.Attribute("input")) == "js1_ ");
        var buttons = rebinds.Select(x => (string?)x.Attribute("input")).Where(x => x?.StartsWith("js1_button") == true).Distinct().OrderBy(x => x).ToList();
        var hats = rebinds.Select(x => (string?)x.Attribute("input")).Where(x => x?.StartsWith("js1_hat") == true).Distinct().OrderBy(x => x).ToList();
        Log($"XML відкрито: {_path}");
        Log($"Rebinds: {rebinds.Count}");
        Log($"Порожні js1_: {empty}");
        Log("Кнопки: " + string.Join(", ", buttons));
        Log("HAT: " + string.Join(", ", hats));
    }

    private void Log(string text) => _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
}
