using System.Text.Json;
using DesktopLife.App;
using Xunit;

namespace DesktopLife.Tests;

public sealed class DiagnosticProtocolTests
{
    [Theory]
    [InlineData(DiagnosticRunState.Stopped)]
    [InlineData(DiagnosticRunState.Failed)]
    [InlineData(DiagnosticRunState.Passed)]
    public void FinishedResultAndSingleNotificationSurviveServiceReopen(DiagnosticRunState state)
    {
        using var fixture=new Fixture();
        var status=fixture.Status with{State=state,FinishedUtc=DateTimeOffset.UtcNow};
        fixture.Publish(status);
        using(var first=new DiagnosticRunCoordinator(fixture.Root))
        {
            first.Refresh();Assert.Equal(state,first.Current!.State);
            Assert.True(first.MarkCompletionNotified());Assert.False(first.MarkCompletionNotified());
        }
        using var reopened=new DiagnosticRunCoordinator(fixture.Root);reopened.Refresh();
        Assert.Equal(state,reopened.Current!.State);Assert.True(reopened.Current.Terminal);
        Assert.False(reopened.MarkCompletionNotified());
    }

    [Fact]
    public async Task ActivePreparationRejectsASecondJobBeforeAnyBinaryCopy()
    {
        using var fixture=new Fixture();fixture.Publish(fixture.Status);
        using var coordinator=new DiagnosticRunCoordinator(fixture.Root);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>coordinator.StartAsync(false,1));
        Assert.Single(Directory.GetDirectories(Path.Combine(fixture.Root,"runs")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Directory,"binary")));
    }

    [Fact]
    public async Task CompletedReportAwaitingWorkerExitCannotNotifyOrStartAnotherJob()
    {
        using var fixture=new Fixture();fixture.Publish(fixture.Status with{State=DiagnosticRunState.Completing,PendingResult=DiagnosticRunState.Passed});
        using var coordinator=new DiagnosticRunCoordinator(fixture.Root);coordinator.Refresh();
        Assert.False(coordinator.Current!.Terminal);Assert.False(coordinator.MarkCompletionNotified());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>coordinator.StartAsync(false,1));
        using var reopened=new DiagnosticRunCoordinator(fixture.Root);reopened.Refresh();
        Assert.Equal(DiagnosticRunState.Completing,reopened.Current!.State);Assert.False(reopened.Current.Terminal);
    }

    [Fact]
    public void LiveCapturedProcessCannotFinalizeEvenWhenItsModuleIdentityIsUnavailable()
    {
        using var fixture=new Fixture();using var process=System.Diagnostics.Process.GetCurrentProcess();
        fixture.Publish(fixture.Status with{State=DiagnosticRunState.Completing,PendingResult=DiagnosticRunState.Passed,WorkerExitCode=0,Scope="ProcessProtocolOnly",
            WorkerProcessId=process.Id,WorkerStartedUtc=new DateTimeOffset(process.StartTime.ToUniversalTime()),ExecutablePath=Path.Combine(fixture.Directory,"unavailable.exe")});
        using var coordinator=new DiagnosticRunCoordinator(fixture.Root);coordinator.Refresh();
        Assert.Equal(DiagnosticRunState.Completing,coordinator.Current!.State);Assert.False(coordinator.MarkCompletionNotified());
    }

    [Fact]
    public void StalePreparationIsInterruptedAndNeverPass()
    {
        using var fixture=new Fixture();fixture.Publish(fixture.Status with{StartedUtc=DateTimeOffset.UtcNow.AddMinutes(-3)});
        using var coordinator=new DiagnosticRunCoordinator(fixture.Root);coordinator.Refresh();
        Assert.Equal(DiagnosticRunState.Interrupted,coordinator.Current!.State);
        Assert.NotNull(coordinator.Current.FirstError);Assert.True(coordinator.Current.Terminal);
    }

    [Fact]
    public async Task AtomicStatusReplacementToleratesATemporaryWindowsReader()
    {
        using var fixture=new Fixture();var file=Path.Combine(fixture.Directory,"run-status.json");
        fixture.Publish(fixture.Status);
        var reader=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
        var finished=fixture.Status with{State=DiagnosticRunState.Stopped,FinishedUtc=DateTimeOffset.UtcNow};
        var write=Task.Run(()=>DiagnosticRunCoordinator.WriteStatus(fixture.Directory,finished));
        await Task.Delay(80);reader.Dispose();await write;
        Assert.Equal(DiagnosticRunState.Stopped,DiagnosticRunCoordinator.ReadStatus(fixture.Directory)!.State);
    }

    [Fact]
    public async Task ConcurrentProgressReadsCannotCorruptTheTerminalResult()
    {
        using var fixture=new Fixture();fixture.Publish(fixture.Status);
        using var coordinator=new DiagnosticRunCoordinator(fixture.Root);
        var write=Task.Run(()=>
        {
            for(var second=0;second<80;second++)DiagnosticRunCoordinator.WriteStatus(fixture.Directory,fixture.Status with{State=DiagnosticRunState.Running,WorkloadSeconds=second,LastUpdatedUtc=DateTimeOffset.UtcNow});
            DiagnosticRunCoordinator.WriteStatus(fixture.Directory,fixture.Status with{State=DiagnosticRunState.Passed,WorkloadSeconds=600,FinishedUtc=DateTimeOffset.UtcNow,LastUpdatedUtc=DateTimeOffset.UtcNow});
        });
        var read=Task.Run(()=>{for(var index=0;index<200;index++)coordinator.Refresh();});
        await Task.WhenAll(write,read);coordinator.Refresh();
        Assert.Equal(DiagnosticRunState.Passed,coordinator.Current!.State);
        using var reopened=new DiagnosticRunCoordinator(fixture.Root);reopened.Refresh();Assert.Equal(DiagnosticRunState.Passed,reopened.Current!.State);
    }

    [Fact]
    public void WorkerRejectsProductionProfileAndInvalidJobPathsBeforeOpeningAnyData()
    {
        var profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopLife");
        Assert.Throws<ArgumentException>(()=>DiagnosticWorkerSession.FromArguments(["--diagnostic-worker","--diagnostic-run-directory="+profile]));
        using var fixture=new Fixture();
        Assert.Throws<ArgumentException>(()=>DiagnosticWorkerSession.FromArguments(["--diagnostic-worker","--diagnostic-run-directory="+fixture.Directory]));
        Assert.False(File.Exists(Path.Combine(fixture.Directory,"run-status.json")));
    }

    [Theory]
    [InlineData("--diagnostic-protocol-only")]
    [InlineData("--diagnostic-run-directory=invalid")]
    public void IncompleteDiagnosticCommandCannotFallThroughToThePersonProfile(string argument)
        =>Assert.Throws<ArgumentException>(()=>DiagnosticWorkerSession.FromArguments([argument]));

    private sealed class Fixture:IDisposable
    {
        public string Root {get;}=Path.Combine(Path.GetTempPath(),"DesktopLifeRuntimeDiagnostics",Guid.NewGuid().ToString("N"));
        public string Directory {get;}
        public DiagnosticRunStatus Status {get;}
        public Fixture()
        {
            var run=Guid.NewGuid().ToString("N");Directory=Path.Combine(Root,"runs",run);System.IO.Directory.CreateDirectory(Directory);
            Status=new(){RunId=run,State=DiagnosticRunState.Preparing,RequestedSeconds=600,Floors=1,StartedUtc=DateTimeOffset.UtcNow,LastUpdatedUtc=DateTimeOffset.UtcNow,Kind="Short",Version="fixture"};
        }
        public void Publish(DiagnosticRunStatus status)
        {
            DiagnosticRunCoordinator.WriteStatus(Directory,status);
            DiagnosticRunCoordinator.AtomicWrite(Path.Combine(Root,"latest.json"),JsonSerializer.Serialize(new{Status.RunId}));
        }
        public void Dispose()
        {
            var resolved=Path.GetFullPath(Root);var parent=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopLifeRuntimeDiagnostics"));
            if(!string.Equals(Path.GetDirectoryName(resolved),parent,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(resolved),"N",out _))throw new InvalidOperationException("Refusing cleanup outside the owned test root.");
            System.IO.Directory.Delete(resolved,true);
        }
    }
}
