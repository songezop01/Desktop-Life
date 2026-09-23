using DesktopLife.App;
using System.IO.Pipes;
using Xunit;

namespace DesktopLife.Tests;

public class AppInstanceChannelTests
{
    private sealed class NonPumpingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) { }
    }
    private static string Root() => Path.Combine(Path.GetTempPath(), "DesktopLifeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DuplicateLaunchActivatesExistingInstance()
    {
        var root = Root();
        var received = new TaskCompletionSource<AppInstanceChannel.Command>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new AppInstanceChannel(root, command => received.TrySetResult(command));
        Assert.True(await AppInstanceChannel.SendAsync(root, AppInstanceChannel.Command.Activate));
        Assert.Equal(AppInstanceChannel.Command.Activate, await received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ShutdownWaitsUntilSaveOwnerReleasesLock()
    {
        var root = Root();
        Directory.CreateDirectory(root);
        using var instance = new FileStream(Path.Combine(root, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var received = new TaskCompletionSource<AppInstanceChannel.Command>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new AppInstanceChannel(root, command => received.TrySetResult(command));
        var shuttingDown = AppInstanceChannel.ShutdownAsync(root);
        Assert.Equal(AppInstanceChannel.Command.Shutdown, await received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        await Task.Delay(200);
        Assert.False(shuttingDown.IsCompleted);
        instance.Dispose();
        Assert.True(await shuttingDown.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task InvalidCommandDoesNotReachAppAndNextClientStillWorks()
    {
        var root = Root();
        var commands = new System.Collections.Concurrent.ConcurrentQueue<AppInstanceChannel.Command>();
        using var server = new AppInstanceChannel(root, commands.Enqueue);
        using (var invalid = new NamedPipeClientStream(".", AppInstanceChannel.PipeName(root), PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await invalid.ConnectAsync(1500);
            await invalid.WriteAsync(new byte[] { 255 });
            Assert.Equal(0, await invalid.ReadAsync(new byte[1]));
        }
        Assert.Empty(commands);
        Assert.True(await AppInstanceChannel.SendAsync(root, AppInstanceChannel.Command.Activate));
    }

    [Fact]
    public async Task MissingServerReturnsFalseAndDoesNotCreatePetData()
    {
        var root = Root();
        Assert.False(await AppInstanceChannel.SendAsync(root, AppInstanceChannel.Command.Activate, 100));
        Assert.True(await AppInstanceChannel.ShutdownAsync(root));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void StartupCommandsCompleteWhenDispatcherIsSynchronouslyWaiting()
    {
        var root = Root();
        Directory.CreateDirectory(root);
        using var instance = new FileStream(Path.Combine(root, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var server = new AppInstanceChannel(root, command =>
        {
            if (command == AppInstanceChannel.Command.Shutdown)
                _ = Task.Run(async () => { await Task.Delay(100); instance.Dispose(); });
        });
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingContext());
            try
            {
                Assert.True(AppInstanceChannel.SendAsync(root, AppInstanceChannel.Command.Activate).GetAwaiter().GetResult());
                Assert.True(AppInstanceChannel.ShutdownAsync(root).GetAwaiter().GetResult());
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(3)), "Startup client captured the blocked dispatcher context.");
        Assert.Null(failure);
    }
}
