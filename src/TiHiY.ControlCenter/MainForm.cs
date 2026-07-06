using System.Data;
using System.Xml.Linq;

namespace TiHiY.ControlCenter;

public sealed class MainForm : Form
{
    private readonly Label _status = new() { AutoSize = true, Text = "Open Star Citizen layout_*.xml", Dock = DockStyle.Top };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private XDocument? _doc;
    private string? _xmlPath;

    public MainForm()
    {
        Text = "TiHiY Control Center v0.1";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
        buttons.Controls.Add(MakeButton("Open XML", OpenXml));
        buttons.Controls.Add(MakeButton("Backup", BackupXml));
        buttons.Controls.Add(MakeButton("Apply TiHiY Defaults", ApplyDefaults));
        buttons.Controls.Add(MakeButton("Export XML", ExportXml));

        Controls.Add(_grid);
        Controls.Add(_status);
        Controls.Add(buttons);
    }

    private static Button MakeButton(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(4) };
        b.Click += click;
        return b;
    }

    private void OpenXml(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Star Citizen XML (*.xml)|*.xml|All files (*.*)|*.*",
            Title = "Open Star Citizen control profile"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _xmlPath = dialog.FileName;
        _doc = XDocument.Load(_xmlPath, LoadOptions.PreserveWhitespace);
        LoadBindsToGrid();
    }

    private void BackupXml(object? sender, EventArgs e)
    {
        if (_xmlPath is null)
        {
            MessageBox.Show(this, "Open XML first.");
            return;
        }

        var backupDir = Path.Combine(Path.GetDirectoryName(_xmlPath)!, "TiHiY_Backup");
        Directory.CreateDirectory(backupDir);
        var backupPath = Path.Combine(backupDir, Path.GetFileNameWithoutExtension(_xmlPath) + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml");
        File.Copy(_xmlPath, backupPath, overwrite: false);
        MessageBox.Show(this, "Backup created:\n" + backupPath);
    }

    private void ApplyDefaults(object? sender, EventArgs e)
    {
        if (_doc is null)
        {
            MessageBox.Show(this, "Open XML first.");
            return;
        }

        var device = _doc.Descendants("deviceoptions")
            .FirstOrDefault(x => ((string?)x.Attribute("name"))?.Contains("T.16000M", StringComparison.OrdinalIgnoreCase) == true);

        if (device is null)
        {
            MessageBox.Show(this, "T.16000M deviceoptions not found.");
            return;
        }

        SetOption(device, "x", "deadzone", "0.018");
        SetOption(device, "y", "deadzone", "0.018");
        SetOption(device, "rotz", "deadzone", "0.030");
        SetOption(device, "slider1", "deadzone", "0.010");
        SetOption(device, "x", "saturation", "0.950");
        SetOption(device, "y", "saturation", "0.950");
        SetOption(device, "rotz", "saturation", "0.920");
        SetOption(device, "slider1", "saturation", "1.000");

        LoadBindsToGrid();
        MessageBox.Show(this, "TiHiY HOSAM axis defaults applied in memory. Use Export XML to save.");
    }

    private static void SetOption(XElement device, string input, string attr, string value)
    {
        var opt = device.Elements("option")
            .FirstOrDefault(x => (string?)x.Attribute("input") == input && x.Attribute(attr) is not null);

        if (opt is null)
        {
            opt = new XElement("option", new XAttribute("input", input));
            device.Add(opt);
        }

        opt.SetAttributeValue(attr, value);
    }

    private void ExportXml(object? sender, EventArgs e)
    {
        if (_doc is null)
        {
            MessageBox.Show(this, "Open XML first.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Star Citizen XML (*.xml)|*.xml",
            FileName = "TiHiY_Universal_HOSAM_v3.xml",
            Title = "Export Star Citizen control profile"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _doc.Save(dialog.FileName);
        MessageBox.Show(this, "Exported:\n" + dialog.FileName);
    }

    private void LoadBindsToGrid()
    {
        if (_doc is null) return;

        var table = new DataTable();
        table.Columns.Add("ActionMap");
        table.Columns.Add("Action");
        table.Columns.Add("Input");
        table.Columns.Add("Activation");

        int emptyJoystickBinds = 0;
        foreach (var map in _doc.Descendants("actionmap"))
        {
            var mapName = (string?)map.Attribute("name") ?? "";
            foreach (var action in map.Elements("action"))
            {
                var actionName = (string?)action.Attribute("name") ?? "";
                foreach (var rebind in action.Elements("rebind"))
                {
                    var input = (string?)rebind.Attribute("input") ?? "";
                    if (input.Trim() == "js1_") emptyJoystickBinds++;
                    table.Rows.Add(mapName, actionName, input, (string?)rebind.Attribute("activationMode") ?? "");
                }
            }
        }

        _grid.DataSource = table;
        _status.Text = $"Loaded: {Path.GetFileName(_xmlPath)} | Binds: {table.Rows.Count} | Empty joystick binds: {emptyJoystickBinds}";
    }
}
