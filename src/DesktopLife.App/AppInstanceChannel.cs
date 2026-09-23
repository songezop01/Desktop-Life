using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace DesktopLife.App;

/// <summary>Local, current-user-only commands; the application remains responsible for saving before exit.</summary>
internal sealed class AppInstanceChannel : IDisposable
{
    internal enum Command : byte { Activate = 1, Shutdown = 2 }
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task listener;

    public AppInstanceChannel(string dataRoot, Action<Command> receive)
    {
        listener = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(PipeName(dataRoot), PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(cancellation.Token);
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                    request.CancelAfter(TimeSpan.FromSeconds(2));
                    var bytes = new byte[1];
                    await pipe.ReadExactlyAsync(bytes, request.Token);
                    if (bytes[0] is not ((byte)Command.Activate) and not ((byte)Command.Shutdown)) continue;
                    await pipe.WriteAsync(new byte[] { 1 }, request.Token);
                    await pipe.FlushAsync(request.Token);
                    receive((Command)bytes[0]);
                }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }
        });
    }

    internal static string PipeName(string dataRoot)
    {
        var identity = Path.GetFullPath(dataRoot).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        return "DesktopLife-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
    }

    internal static async Task<bool> SendAsync(string dataRoot, Command command, int timeoutMilliseconds = 1500)
    {
        using var timeout = new CancellationTokenSource(timeoutMilliseconds);
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName(dataRoot), PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await pipe.WriteAsync(new[] { (byte)command }, timeout.Token).ConfigureAwait(false);
            await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
            var response = new byte[1];
            await pipe.ReadExactlyAsync(response, timeout.Token).ConfigureAwait(false);
            return response[0] == 1;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
        { return false; }
    }

    internal static bool IsRunning(string dataRoot)
    {
        try
        {
            if (!Directory.Exists(dataRoot)) return false;
            using var probe = new FileStream(Path.Combine(dataRoot, "instance.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException) { return true; }
    }

    internal static async Task<bool> ShutdownAsync(string dataRoot)
    {
        if (!IsRunning(dataRoot)) return true;
        if (!await SendAsync(dataRoot, Command.Shutdown).ConfigureAwait(false)) return false;
        // Acknowledging the command is not proof of shutdown: saving or icon recovery may cancel/delay closing.
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (!IsRunning(dataRoot)) return true;
            await Task.Delay(100).ConfigureAwait(false);
        }
        return false;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        // Never block the WPF dispatcher during shutdown.
        _ = listener.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
    }
}
