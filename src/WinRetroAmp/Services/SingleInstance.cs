using System.IO.Pipes;
using System.Text.Json;

namespace WinRetroAmp.Services;

/// <summary>
/// Keeps one running player per user. A second launch (e.g. double-clicking a song in Explorer)
/// forwards its command line to the first one through a named pipe and exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private static readonly string Name = "WinRetroAmp-" + Environment.UserName;
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Returns the instance guard, or null if another instance already runs (args were forwarded).</summary>
    public static SingleInstance? TryStart(string[] args)
    {
        var mutex = new Mutex(true, Name, out bool created);
        if (created) return new SingleInstance(mutex);
        mutex.Dispose();
        try
        {
            using var client = new NamedPipeClientStream(".", Name, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client);
            writer.WriteLine(JsonSerializer.Serialize(args));
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // The first instance is still starting or hung; nothing else to do.
        }
        return null;
    }

    /// <summary>Listens for forwarded command lines and hands them to <paramref name="onArgs"/> (on a background thread).</summary>
    public void Listen(Action<string[]> onArgs)
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(Name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cts.Token);
                    using var reader = new StreamReader(server);
                    string? line = await reader.ReadLineAsync(_cts.Token);
                    if (line is not null && JsonSerializer.Deserialize<string[]>(line) is { } args) onArgs(args);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    // Broken message: keep listening.
                }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
