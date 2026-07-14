namespace TiHiY.StreamControlCenter.Models;

public sealed record PerformanceSample(
    DateTime Timestamp,
    double Fps,
    double FrameMilliseconds,
    double CpuMilliseconds,
    double GpuMilliseconds);
