using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

// Standalone measurement helper loaded by PowerShell; no production dependencies.
public sealed class SessionLoadRunner : IDisposable
{
    private readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public SessionLoadRunner(string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<SessionLoadResult> RunAsync(string url, int concurrency, int seconds, int expectedStatus)
    {
        var samples = new ConcurrentBag<double>();
        var statuses = new ConcurrentBag<int>();
        var errors = new ConcurrentBag<bool>();
        var clock = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, concurrency).Select(async worker =>
        {
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    using (var response = await client.GetAsync(url))
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        statuses.Add((int)response.StatusCode);
                        using (var json = JsonDocument.Parse(body))
                        {
                            var root = json.RootElement;
                            string expectedEnvelope = expectedStatus == 404 ? "notFound" : "externalServiceError";
                            if ((int)response.StatusCode != expectedStatus || root.GetProperty("isSuccess").GetBoolean() ||
                                root.GetProperty("status").GetString() != expectedEnvelope ||
                                !root.TryGetProperty("correlationId", out _) || !root.TryGetProperty("traceId", out _))
                            {
                                errors.Add(true);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Never retain exception text, bodies or credentials in the measurement output.
                    errors.Add(true);
                }
                samples.Add(timer.Elapsed.TotalMilliseconds);
            }
        }));
        clock.Stop();
        var sorted = samples.OrderBy(value => value).ToArray();
        return new SessionLoadResult
        {
            Concurrency = concurrency, Requests = sorted.Length, Errors = errors.Count,
            TransportFailures = sorted.Length - statuses.Count,
            ElapsedSeconds = clock.Elapsed.TotalSeconds, RequestsPerSecond = sorted.Length / clock.Elapsed.TotalSeconds,
            P50Ms = Percentile(sorted, .50), P95Ms = Percentile(sorted, .95), P99Ms = Percentile(sorted, .99),
            MaxMs = sorted.Last(), StatusCounts = statuses.GroupBy(value => value).ToDictionary(group => group.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), group => group.Count())
        };
    }

    private static double Percentile(double[] sorted, double percentile) => sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    public void Dispose() => client.Dispose();
}
