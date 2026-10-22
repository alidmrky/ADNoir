using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AdNoir.Notification.IntegrationChecks;

// Protocol fixture only: accepts mail locally and never forwards it to an external server.
internal sealed class LoopbackSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<string, int> _requests = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<Task> _sessions = [];
    private readonly Task _accepting;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    private int _acceptedCount;
    public int AcceptedCount => Volatile.Read(ref _acceptedCount);

    public LoopbackSmtpServer()
    {
        _listener.Start();
        _accepting = AcceptAsync();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_shutdown.Token);
                _sessions.Add(HandleAsync(client));
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new(stream, Encoding.ASCII))
        using (StreamWriter writer = new(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" })
        {
            string recipient = string.Empty;
            bool data = false;
            await writer.WriteLineAsync("220 localhost integration fixture");
            while (await reader.ReadLineAsync(_shutdown.Token) is string line)
            {
                if (data)
                {
                    if (line == ".")
                    {
                        data = false;
                        if (recipient.Contains("uncertain", StringComparison.Ordinal)) { return; }
                        Interlocked.Increment(ref _acceptedCount);
                        await writer.WriteLineAsync("250 accepted");
                    }
                    continue;
                }
                if (line.StartsWith("EHLO", StringComparison.Ordinal) || line.StartsWith("HELO", StringComparison.Ordinal))
                {
                    await writer.WriteLineAsync("250 localhost");
                }
                else if (line.StartsWith("RCPT TO:", StringComparison.Ordinal))
                {
                    recipient = line;
                    int count = _requests.AddOrUpdate(recipient, 1, (_, old) => old + 1);
                    if (recipient.Contains("reject", StringComparison.Ordinal))
                    {
                        await writer.WriteLineAsync("550 rejected");
                    }
                    else if (recipient.Contains("retry", StringComparison.Ordinal) && count == 1)
                    {
                        await writer.WriteLineAsync("451 temporary rejection");
                    }
                    else { await writer.WriteLineAsync("250 recipient accepted"); }
                }
                else if (line == "DATA")
                {
                    data = true;
                    await writer.WriteLineAsync("354 send data");
                }
                else if (line == "QUIT")
                {
                    await writer.WriteLineAsync("221 bye");
                    return;
                }
                else { await writer.WriteLineAsync("250 OK"); }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        _listener.Stop();
        await _accepting;
        try { await Task.WhenAll(_sessions); }
        catch (OperationCanceledException) { }
        _shutdown.Dispose();
    }
}
