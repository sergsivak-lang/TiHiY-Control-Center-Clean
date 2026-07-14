using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.UI;

public sealed class PerformanceModule : IAsyncDisposable
{
    private static readonly ConditionalWeakTable<Form, PerformanceModule> AttachedModules = new();

    private readonly Form _mainForm;
    private readonly PresentMonService _presentMon = new();
    private readonly List<PerformanceSample> _samples = [];
    private readonly PerformanceGraphControl _graph = new() { Dock = DockStyle.Fill };
    private readonly TextBox _presentMonPath = new();
    private readonly TextBox _processName = new();
    private readonly Label _status = new();
    private readonly Label _fps = CreateMetricValue("— FPS");
    private readonly Label _frame = CreateMetricValue("— ms");
    private readonly Label _cpu = CreateMetricValue("— ms");
    private readonly Label _gpu = CreateMetricValue("— ms");
    private readonly Button _startButton = Theme.CreateButton("ПОЧАТИ МОНІТОРИНГ");
    private readonly Button _stopButton = Theme.CreateButton("ЗУПИНИТИ", true);
    private bool _disposed;

    private PerformanceModule(Form mainForm)
    {
        _mainForm = mainForm;
        _presentMon.SampleReceived += PresentMonOnSampleReceived;
        _presentMon.StatusChanged += PresentMonOnStatusChanged;
        _mainForm.FormClosed += MainFormOnFormClosed;

        var tabControl = FindControl<TabControl>(_mainForm)
            ?? throw new InvalidOperationException("Не знайдено головний TabControl.");

        tabControl.TabPages.Add(BuildTab());
    }

    public static void Attach(Form mainForm)
    {
        ArgumentNullException.ThrowIfNull(mainForm);
        if (!AttachedModules.TryGetValue(mainForm, out _))
        {
            AttachedModules.Add(mainForm, new PerformanceModule(mainForm));
        }
    }

    private TabPage BuildTab()
    {
        var page = new TabPage("ПРОДУКТИВНІСТЬ")
        {
            BackColor = Theme.Window,
            ForeColor = Theme.Text,
            Padding = new Padding(4)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14),
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        page.Controls.Add(root);

        root.Controls.Add(BuildMetricsPanel(), 0, 0);
        root.Controls.Add(_graph, 0, 1);
        root.Controls.Add(BuildControlsPanel(), 0, 2);
        return page;
    }

    private Control BuildMetricsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 2,
            Padding = new Padding(12),
            BackColor = Theme.Panel
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        AddMetric(panel, "FPS", _fps, 0);
        AddMetric(panel, "FRAME", _frame, 1);
        AddMetric(panel, "CPU FRAME", _cpu, 2);
        AddMetric(panel, "GPU FRAME", _gpu, 3);

        var statePanel = new Panel { Dock = DockStyle.Fill };
        var heading = Theme.CreateLabel("СТАН", true);
        heading.Location = new Point(4, 4);
        _status.Text = "Готово до запуску";
        _status.ForeColor = Theme.MutedText;
        _status.AutoSize = false;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _status.Location = new Point(6, 29);
        _status.Size = new Size(250, 46);
        statePanel.Controls.Add(heading);
        statePanel.Controls.Add(_status);
        panel.Controls.Add(statePanel, 4, 0);
        panel.SetRowSpan(statePanel, 2);

        return panel;
    }

    private Control BuildControlsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(12),
            BackColor = Theme.PanelAlt
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _presentMonPath.Dock = DockStyle.Fill;
        _presentMonPath.Text = FindPresentMonExecutable() ?? string.Empty;
        _processName.Dock = DockStyle.Fill;
        _processName.Text = "StarCitizen.exe";

        var browseButton = Theme.CreateButton("ОБРАТИ EXE");
        browseButton.Dock = DockStyle.Fill;
        browseButton.Click += BrowseButtonOnClick;

        var downloadButton = Theme.CreateButton("СТОРІНКА PRESENTMON");
        downloadButton.Dock = DockStyle.Fill;
        downloadButton.Click += (_, _) => OpenUrl("https://github.com/GameTechDev/PresentMon/releases/latest");

        panel.Controls.Add(Theme.CreateLabel("PresentMon.exe"), 0, 0);
        panel.Controls.Add(_presentMonPath, 1, 0);
        panel.Controls.Add(browseButton, 2, 0);
        panel.Controls.Add(downloadButton, 3, 0);

        panel.Controls.Add(Theme.CreateLabel("Процес гри"), 0, 1);
        panel.Controls.Add(_processName, 1, 1);

        _startButton.Dock = DockStyle.Fill;
        _stopButton.Dock = DockStyle.Fill;
        _stopButton.Enabled = false;
        _startButton.Click += StartButtonOnClick;
        _stopButton.Click += StopButtonOnClick;
        panel.Controls.Add(_startButton, 2, 1);
        panel.Controls.Add(_stopButton, 3, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };

        var clearButton = Theme.CreateButton("Очистити графік");
        clearButton.Click += (_, _) =>
        {
            _samples.Clear();
            _graph.ClearSamples();
            ResetMetricLabels();
        };

        var exportButton = Theme.CreateButton("Експорт CSV");
        exportButton.Click += ExportButtonOnClick;

        var hint = Theme.CreateLabel(
            "FPS і час кадру беруться з ETW через PresentMon. Дані можуть трохи відрізнятися від внутрішнього r_displayFrameGraph Star Citizen.",
            true);
        hint.MaximumSize = new Size(700, 0);
        hint.Margin = new Padding(20, 10, 6, 6);

        actions.Controls.Add(clearButton);
        actions.Controls.Add(exportButton);
        actions.Controls.Add(hint);
        panel.Controls.Add(actions, 0, 2);
        panel.SetColumnSpan(actions, 4);

        return panel;
    }

    private static void AddMetric(TableLayoutPanel panel, string title, Label value, int column)
    {
        var titleLabel = Theme.CreateLabel(title, true);
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.TextAlign = ContentAlignment.BottomLeft;
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.TopLeft;
        panel.Controls.Add(titleLabel, column, 0);
        panel.Controls.Add(value, column, 1);
    }

    private static Label CreateMetricValue(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            ForeColor = Theme.Text,
            Font = new Font("Consolas", 17f, FontStyle.Bold),
            Margin = new Padding(6)
        };
    }

    private async void StartButtonOnClick(object? sender, EventArgs e)
    {
        var executable = _presentMonPath.Text.Trim();
        var processName = Path.GetFileName(_processName.Text.Trim());

        if (!File.Exists(executable))
        {
            SetStatus("Спочатку обери PresentMon.exe.", Theme.Danger);
            return;
        }

        if (string.IsNullOrWhiteSpace(processName))
        {
            SetStatus("Вкажи назву процесу гри.", Theme.Danger);
            return;
        }

        var processBaseName = Path.GetFileNameWithoutExtension(processName);
        if (Process.GetProcessesByName(processBaseName).Length == 0)
        {
            SetStatus($"Процес {processName} не запущений.", Theme.Danger);
            return;
        }

        try
        {
            SetButtons(running: true);
            await _presentMon.StartAsync(executable, processName);
        }
        catch (Exception ex)
        {
            SetButtons(running: false);
            SetStatus(ex.Message, Theme.Danger);
        }
    }

    private async void StopButtonOnClick(object? sender, EventArgs e)
    {
        await StopMonitoringAsync();
    }

    private void BrowseButtonOnClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Оберіть консольний PresentMon",
            Filter = "PresentMon (*.exe)|PresentMon*.exe|Виконувані файли (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(_mainForm) == DialogResult.OK)
        {
            _presentMonPath.Text = dialog.FileName;
        }
    }

    private void ExportButtonOnClick(object? sender, EventArgs e)
    {
        if (_samples.Count == 0)
        {
            SetStatus("Немає даних для експорту.", Theme.MutedText);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Зберегти статистику",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"StarCitizen-performance-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog(_mainForm) != DialogResult.OK)
        {
            return;
        }

        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,FPS,FrameMilliseconds,CpuMilliseconds,GpuMilliseconds");
        foreach (var sample in _samples)
        {
            csv.Append(sample.Timestamp.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.Fps.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.FrameMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.CpuMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.GpuMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).AppendLine();
        }

        File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        SetStatus($"CSV збережено: {Path.GetFileName(dialog.FileName)}", Theme.Accent);
    }

    private void PresentMonOnSampleReceived(object? sender, PerformanceSample sample)
    {
        RunOnUiThread(() =>
        {
            _samples.Add(sample);
            while (_samples.Count > 20_000)
            {
                _samples.RemoveAt(0);
            }

            _fps.Text = $"{sample.Fps:0} FPS";
            _frame.Text = $"{sample.FrameMilliseconds:0.0} ms";
            _cpu.Text = $"{sample.CpuMilliseconds:0.0} ms";
            _gpu.Text = $"{sample.GpuMilliseconds:0.0} ms";
            _fps.ForeColor = sample.Fps >= 50 ? Theme.Accent : sample.Fps >= 30 ? Color.Gold : Theme.Danger;
            _frame.ForeColor = Color.Gold;
            _cpu.ForeColor = Color.FromArgb(55, 235, 105);
            _gpu.ForeColor = Color.FromArgb(35, 210, 255);
            _graph.AddSample(sample);
            SetStatus("● ОТРИМУЮ ДАНІ", Theme.Accent);
        });
    }

    private void PresentMonOnStatusChanged(object? sender, string message)
    {
        RunOnUiThread(() =>
        {
            SetStatus(message, message.Contains("помил", StringComparison.OrdinalIgnoreCase) ||
                               message.Contains("немає", StringComparison.OrdinalIgnoreCase)
                ? Theme.Danger
                : Theme.MutedText);

            if (!_presentMon.IsRunning)
            {
                SetButtons(running: false);
            }
        });
    }

    private async void MainFormOnFormClosed(object? sender, FormClosedEventArgs e)
    {
        await DisposeAsync();
    }

    private async Task StopMonitoringAsync()
    {
        SetStatus("Зупиняю PresentMon...", Theme.MutedText);
        await _presentMon.StopAsync();
        SetButtons(running: false);
        SetStatus("Моніторинг зупинено.", Theme.MutedText);
    }

    private void SetButtons(bool running)
    {
        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _presentMonPath.Enabled = !running;
        _processName.Enabled = !running;
    }

    private void ResetMetricLabels()
    {
        _fps.Text = "— FPS";
        _frame.Text = "— ms";
        _cpu.Text = "— ms";
        _gpu.Text = "— ms";
        _fps.ForeColor = Theme.Text;
        _frame.ForeColor = Theme.Text;
        _cpu.ForeColor = Theme.Text;
        _gpu.ForeColor = Theme.Text;
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
    }

    private void RunOnUiThread(Action action)
    {
        if (_disposed || _mainForm.IsDisposed)
        {
            return;
        }

        if (_mainForm.InvokeRequired)
        {
            try
            {
                _mainForm.BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // Вікно закривається.
            }
        }
        else
        {
            action();
        }
    }

    private static string? FindPresentMonExecutable()
    {
        var candidates = Directory
            .EnumerateFiles(AppContext.BaseDirectory, "PresentMon*.exe", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        return candidates.FirstOrDefault();
    }

    private static T? FindControl<T>(Control parent) where T : Control
    {
        foreach (Control control in parent.Controls)
        {
            if (control is T match)
            {
                return match;
            }

            var nested = FindControl<T>(control);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // Браузер може бути заблокований політикою Windows.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _presentMon.SampleReceived -= PresentMonOnSampleReceived;
        _presentMon.StatusChanged -= PresentMonOnStatusChanged;
        await _presentMon.DisposeAsync();
        AttachedModules.Remove(_mainForm);
    }
}
