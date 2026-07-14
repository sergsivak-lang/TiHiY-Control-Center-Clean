using System.Diagnostics;
using System.Globalization;
using System.Text;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Services;

public sealed class PresentMonService : IAsyncDisposable
{
    private readonly object _sampleLock = new();
    private readonly Stopwatch _publishTimer = Stopwatch.StartNew();

    private Process? _process;
    private CancellationTokenSource? _cancellation;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private Dictionary<string, int>? _columns;
    private double _frameTotal;
    private double _cpuTotal;
    private double _gpuTotal;
    private int _sampleCount;

    public event EventHandler<PerformanceSample>? SampleReceived;
    public event EventHandler<string>? StatusChanged;

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(string executablePath, string processName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("Не знайдено PresentMon.exe.", executablePath);
        }

        processName = Path.GetFileName(processName.Trim());
        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new ArgumentException("Вкажіть назву процесу гри.", nameof(processName));
        }

        await StopAsync().ConfigureAwait(false);

        _columns = null;
        ResetAccumulator();
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--process_name");
        startInfo.ArgumentList.Add(processName);
        startInfo.ArgumentList.Add("--output_stdout");
        startInfo.ArgumentList.Add("--no_console_stats");
        startInfo.ArgumentList.Add("--exclude_dropped");
        startInfo.ArgumentList.Add("--terminate_on_proc_exit");
        startInfo.ArgumentList.Add("--stop_existing_session");
        startInfo.ArgumentList.Add("--session_name");
        startInfo.ArgumentList.Add("TiHiYStreamControlCenter");

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) => PublishStatus("PresentMon зупинено.");

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Не вдалося запустити PresentMon.");
        }

        _process = process;
        PublishStatus($"Моніторинг запущено для {processName}.");

        _stdoutTask = ReadStandardOutputAsync(process, _cancellation.Token);
        _stderrTask = ReadStandardErrorAsync(process, _cancellation.Token);
    }

    public async Task StopAsync()
    {
        var cancellation = Interlocked.Exchange(ref _cancellation, null);
        cancellation?.Cancel();

        var process = Interlocked.Exchange(ref _process, null);
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Процес уже завершився.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // PresentMon міг завершитися між перевіркою та Kill().
            }
        }

        var tasks = new[] { _stdoutTask, _stderrTask }.Where(task => task is not null).Cast<Task>().ToArray();
        if (tasks.Length > 0)
        {
            try
            {
                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(1500)).ConfigureAwait(false);
            }
            catch
            {
                // Помилки читання потоку не повинні блокувати закриття програми.
            }
        }

        _stdoutTask = null;
        _stderrTask = null;
        process?.Dispose();
        cancellation?.Dispose();
        ResetAccumulator();
    }

    private async Task ReadStandardOutputAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                ProcessCsvLine(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальна зупинка.
        }
        catch (ObjectDisposedException)
        {
            // Нормальна зупинка під час закриття програми.
        }
        catch (Exception ex)
        {
            PublishStatus($"Помилка читання PresentMon: {ex.Message}");
        }
    }

    private async Task ReadStandardErrorAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (line.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                {
                    PublishStatus("Немає доступу до ETW. Запусти Control Center від адміністратора або додай користувача до Performance Log Users.");
                }
                else if (line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("failed", StringComparison.OrdinalIgnoreCase))
                {
                    PublishStatus(line.Trim());
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальна зупинка.
        }
        catch (ObjectDisposedException)
        {
            // Нормальна зупинка.
        }
    }

    private void ProcessCsvLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var values = ParseCsvLine(line);
        if (values.Count == 0)
        {
            return;
        }

        if (_columns is null)
        {
            if (!values.Any(value => value.Equals("Application", StringComparison.OrdinalIgnoreCase)) ||
                !values.Any(value => value.Equals("ProcessID", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _columns = values
                .Select((name, index) => new { Name = name.Trim().TrimStart('\uFEFF'), Index = index })
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

            PublishStatus("PresentMon підключено. Очікую кадри гри...");
            return;
        }

        var frameMs = ReadMetric(values, "MsBetweenPresents", "msBetweenPresents");
        var cpuMs = ReadMetric(values, "MsCPUBusy", "msCPUBusy", "CPUFrameTime");
        var gpuMs = ReadMetric(values, "MsGPUTime", "MsGPUBusy", "msGPUActive", "GPUFrameTime");

        if (frameMs is null || frameMs <= 0 || frameMs > 1000)
        {
            return;
        }

        PerformanceSample? sample = null;
        lock (_sampleLock)
        {
            _frameTotal += frameMs.Value;
            _cpuTotal += Math.Max(0, cpuMs ?? 0);
            _gpuTotal += Math.Max(0, gpuMs ?? 0);
            _sampleCount++;

            if (_publishTimer.ElapsedMilliseconds >= 250 && _sampleCount > 0)
            {
                var averageFrame = _frameTotal / _sampleCount;
                var averageCpu = _cpuTotal / _sampleCount;
                var averageGpu = _gpuTotal / _sampleCount;
                sample = new PerformanceSample(
                    DateTime.Now,
                    1000d / averageFrame,
                    averageFrame,
                    averageCpu,
                    averageGpu);

                ResetAccumulatorUnsafe();
            }
        }

        if (sample is not null)
        {
            SampleReceived?.Invoke(this, sample);
        }
    }

    private double? ReadMetric(IReadOnlyList<string> values, params string[] columnNames)
    {
        if (_columns is null)
        {
            return null;
        }

        foreach (var columnName in columnNames)
        {
            if (!_columns.TryGetValue(columnName, out var index) || index < 0 || index >= values.Count)
            {
                continue;
            }

            var raw = values[index].Trim();
            if (raw.Equals("NA", StringComparison.OrdinalIgnoreCase) || raw.Length == 0)
            {
                return null;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                result.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        result.Add(field.ToString());
        return result;
    }

    private void ResetAccumulator()
    {
        lock (_sampleLock)
        {
            ResetAccumulatorUnsafe();
        }
    }

    private void ResetAccumulatorUnsafe()
    {
        _frameTotal = 0;
        _cpuTotal = 0;
        _gpuTotal = 0;
        _sampleCount = 0;
        _publishTimer.Restart();
    }

    private void PublishStatus(string message) => StatusChanged?.Invoke(this, message);

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
