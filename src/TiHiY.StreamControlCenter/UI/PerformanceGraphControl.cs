using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.UI;

internal sealed class PerformanceGraphControl : Control
{
    private readonly List<PerformanceSample> _samples = [];
    private const int MaximumSamples = 240;

    public PerformanceGraphControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(10, 13, 18);
        ForeColor = Theme.Text;
        MinimumSize = new Size(420, 260);
    }

    public void AddSample(PerformanceSample sample)
    {
        _samples.Add(sample);
        while (_samples.Count > MaximumSamples)
        {
            _samples.RemoveAt(0);
        }

        Invalidate();
    }

    public void ClearSamples()
    {
        _samples.Clear();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        var plot = new Rectangle(52, 28, Math.Max(20, ClientSize.Width - 70), Math.Max(20, ClientSize.Height - 72));
        using var borderPen = new Pen(Color.FromArgb(65, 78, 92));
        using var gridPen = new Pen(Color.FromArgb(35, 46, 58));
        using var framePen = new Pen(Color.FromArgb(255, 230, 40), 2f);
        using var cpuPen = new Pen(Color.FromArgb(55, 235, 105), 2f);
        using var gpuPen = new Pen(Color.FromArgb(35, 210, 255), 2f);
        using var labelBrush = new SolidBrush(Theme.MutedText);
        using var textBrush = new SolidBrush(Theme.Text);
        using var labelFont = new Font("Segoe UI", 8.5f);
        using var legendFont = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);

        graphics.DrawRectangle(borderPen, plot);

        var maximum = CalculateScaleMaximum();
        const int horizontalLines = 5;
        for (var i = 0; i <= horizontalLines; i++)
        {
            var y = plot.Top + (plot.Height * i / horizontalLines);
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            var value = maximum * (horizontalLines - i) / horizontalLines;
            graphics.DrawString($"{value:0} ms", labelFont, labelBrush, 4, y - 8);
        }

        DrawLegend(graphics, framePen, "FRAME", plot.Left, 5, legendFont, textBrush);
        DrawLegend(graphics, cpuPen, "CPU", plot.Left + 100, 5, legendFont, textBrush);
        DrawLegend(graphics, gpuPen, "GPU", plot.Left + 175, 5, legendFont, textBrush);

        if (_samples.Count < 2)
        {
            var message = "Запусти Star Citizen і натисни «ПОЧАТИ МОНІТОРИНГ»";
            var size = graphics.MeasureString(message, Font);
            graphics.DrawString(message, Font, labelBrush,
                plot.Left + (plot.Width - size.Width) / 2,
                plot.Top + (plot.Height - size.Height) / 2);
            return;
        }

        DrawSeries(graphics, plot, maximum, framePen, sample => sample.FrameMilliseconds);
        DrawSeries(graphics, plot, maximum, cpuPen, sample => sample.CpuMilliseconds);
        DrawSeries(graphics, plot, maximum, gpuPen, sample => sample.GpuMilliseconds);

        graphics.DrawString("останні 60 секунд", labelFont, labelBrush, plot.Right - 110, plot.Bottom + 10);
    }

    private double CalculateScaleMaximum()
    {
        if (_samples.Count == 0)
        {
            return 50;
        }

        var maximum = _samples.Max(sample =>
            Math.Max(sample.FrameMilliseconds, Math.Max(sample.CpuMilliseconds, sample.GpuMilliseconds)));

        maximum = Math.Clamp(maximum * 1.20, 20, 200);
        return Math.Ceiling(maximum / 10d) * 10d;
    }

    private void DrawSeries(
        Graphics graphics,
        Rectangle plot,
        double maximum,
        Pen pen,
        Func<PerformanceSample, double> selector)
    {
        var points = new PointF[_samples.Count];
        var denominator = Math.Max(1, MaximumSamples - 1);
        var emptySpace = MaximumSamples - _samples.Count;

        for (var index = 0; index < _samples.Count; index++)
        {
            var xIndex = emptySpace + index;
            var x = plot.Left + plot.Width * xIndex / denominator;
            var value = Math.Clamp(selector(_samples[index]), 0, maximum);
            var y = plot.Bottom - (float)(plot.Height * value / maximum);
            points[index] = new PointF(x, y);
        }

        if (points.Length > 1)
        {
            graphics.DrawLines(pen, points);
        }
    }

    private static void DrawLegend(
        Graphics graphics,
        Pen pen,
        string text,
        int x,
        int y,
        Font font,
        Brush brush)
    {
        graphics.DrawLine(pen, x, y + 8, x + 25, y + 8);
        graphics.DrawString(text, font, brush, x + 31, y);
    }
}
