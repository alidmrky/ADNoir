using System.Collections.Generic;

public sealed class SessionLoadResult
{
    public int Concurrency { get; set; }
    public int Requests { get; set; }
    public int Errors { get; set; }
    public int TransportFailures { get; set; }
    public double ElapsedSeconds { get; set; }
    public double RequestsPerSecond { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double MaxMs { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; }
}
