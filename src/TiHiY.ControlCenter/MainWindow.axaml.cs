using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TiHiY.ControlCenter;

public partial class MainWindow : Window
{
    private string? _loadedPath;
    private XDocument? _document;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OpenXml_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Star Citizen layout XML",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Star Citizen XML") { Patterns = new[] { "*.xml" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count == 0) return;

        _loadedPath = files[0].Path.LocalPath;
        try
        {
            _document = XDocument.Load(_loadedPath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            BackupButton.IsEnabled = true;
            ApplyDefaultsButton.IsEnabled = true;
            SaveAsButton.IsEnabled = true;
            StatusText.Text = $"Loaded: {_loadedPath}";
            RefreshSummary();
            AddLog("XML loaded successfully.");
        }
        catch (Exception ex)
        {
            AddLog("ERROR: " + ex.Message);
            StatusText.Text = "Failed to load XML.";
        }
    }

    private void Backup_Click(object? sender, RoutedEventArgs e)
    {
        if (_loadedPath is null || !File.Exists(_loadedPath)) return;
        var dir = Path.GetDirectoryName(_loadedPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(_loadedPath);
        var backup = Path.Combine(dir, $"{name}.backup-{DateTime.Now:yyyyMMdd-HHmmss}.xml");
        File.Copy(_loadedPath, backup, overwrite: false);
        AddLog("Backup created: " + backup);
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Star Citizen XML",
            SuggestedFileName = "TiHiY_Universal_HOSAM.xml",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Star Citizen XML") { Patterns = new[] { "*.xml" } }
            }
        });

        if (file is null) return;
        _document.Save(file.Path.LocalPath);
        AddLog("Saved: " + file.Path.LocalPath);
    }

    private void ApplyDefaults_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null) return;

        var deadzone = ParseDouble(DeadzoneBox.Text, 0.018);
        var saturation = ParseDouble(SaturationBox.Text, 0.95);
        var twistDeadzone = ParseDouble(TwistDeadzoneBox.Text, 0.03);
        var sliderDeadzone = ParseDouble(SliderDeadzoneBox.Text, 0.02);

        var joystickOptions = _document.Descendants("deviceoptions")
            .FirstOrDefault(x => ((string?)x.Attribute("name"))?.Contains("T.16000M", StringComparison.OrdinalIgnoreCase) == true);

        if (joystickOptions is null)
        {
            AddLog("T.16000M deviceoptions block not found.");
            return;
        }

        SetOption(joystickOptions, "x", "deadzone", deadzone);
        SetOption(joystickOptions, "y", "deadzone", deadzone);
        SetOption(joystickOptions, "x", "saturation", saturation);
        SetOption(joystickOptions, "y", "saturation", saturation);
        SetOption(joystickOptions, "rotz", "deadzone", twistDeadzone);
        SetOption(joystickOptions, "rotz", "saturation", 0.99);
        SetOption(joystickOptions, "slider1", "deadzone", sliderDeadzone);
        SetOption(joystickOptions, "slider1", "saturation", 1.0);

        AddLog("TiHiY defaults applied to XML in memory. Use Save As XML to export.");
        RefreshSummary();
    }

    private static double ParseDouble(string? value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)) return result;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result)) return result;
        return fallback;
    }

    private static void SetOption(XElement parent, string input, string attribute, double value)
    {
        var option = parent.Elements("option")
            .FirstOrDefault(x => (string?)x.Attribute("input") == input && x.Attribute(attribute) is not null);

        if (option is null)
        {
            option = new XElement("option", new XAttribute("input", input));
            parent.Add(option);
        }

        option.SetAttributeValue(attribute, value.ToString("0.########", CultureInfo.InvariantCulture));
    }

    private void RefreshSummary()
    {
        if (_document is null) return;

        var actions = _document.Descendants("action").ToList();
        var rebinds = _document.Descendants("rebind").ToList();
        var emptyJoystick = rebinds.Count(r => ((string?)r.Attribute("input")) == "js1_ ");
        var joystickRebinds = rebinds.Count(r => ((string?)r.Attribute("input"))?.StartsWith("js1_", StringComparison.OrdinalIgnoreCase) == true);

        SummaryText.Text = $"Actions: {actions.Count} | Rebinds: {rebinds.Count} | Joystick rebinds: {joystickRebinds} | Empty js1_ binds: {emptyJoystick}";

        var sb = new StringBuilder();
        sb.AppendLine("DEVICE OPTIONS");
        foreach (var device in _document.Descendants("deviceoptions"))
        {
            sb.AppendLine("- " + (string?)device.Attribute("name"));
            foreach (var option in device.Elements("option"))
            {
                sb.AppendLine("  " + option.ToString(SaveOptions.DisableFormatting));
            }
        }

        sb.AppendLine();
        sb.AppendLine("ACTION BINDS");
        foreach (var map in _document.Descendants("actionmap"))
        {
            sb.AppendLine("[" + (string?)map.Attribute("name") + "]");
            foreach (var action in map.Elements("action"))
            {
                var name = (string?)action.Attribute("name");
                var inputs = action.Elements("rebind").Select(r => (string?)r.Attribute("input") ?? "");
                sb.AppendLine($"  {name}: {string.Join(", ", inputs)}");
            }
        }

        BindingsText.Text = sb.ToString();
    }

    private void AddLog(string message)
    {
        LogText.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
    }
}
