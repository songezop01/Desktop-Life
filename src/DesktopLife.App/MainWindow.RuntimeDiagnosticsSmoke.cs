using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace DesktopLife.App;

public partial class MainWindow
{
    public async Task SmokeRuntimeDiagnostics(string outputRoot)
    {
        var profile = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(profile), Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DesktopLifeSmoke")), StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(profile), "N", out _))
            throw new InvalidOperationException("Runtime diagnostics smoke requires TEMP/GUID isolation.");
        FreezeRestartVerification(); Show();
        var section = DiagnosticsLogicalChildren<Expander>(this).Single(value => Equals(value.Header, "進階／診斷"));
        SetDiagnosticSectionExpanded(section, true); UpdateLayout();
        if (!RuntimeTestStatus.IsVisible || !Equals(RuntimeShortTestButton.Content,"短測 10 分鐘") || !Equals(RuntimeLongTestButton.Content,"長測 2 小時"))
            throw new InvalidOperationException("Runtime diagnostics controls were not visible or labelled correctly.");
        var root = Path.Combine(Path.GetTempPath(), "DesktopLifeRuntimeDiagnostics", Guid.NewGuid().ToString("N"));
        string stoppedId,passedId,failedId;
        var binaryVerified = false; var secondJobRejected = false;
        using (var first = new DiagnosticRunCoordinator(root))
        {
            await first.StartAsync(12, "Protocol", 1, true);
            var running = await WaitForDiagnosticStatus(first, status => status.State == DiagnosticRunState.Running && status.WorkerProcessId is not null);
            stoppedId = running.RunId;
            binaryVerified = DiagnosticRunCoordinator.HashFile(running.ExecutablePath) == running.ExecutableSha256
                && !string.Equals(Path.GetFullPath(running.ExecutablePath), Path.GetFullPath(Environment.ProcessPath!), StringComparison.OrdinalIgnoreCase);
            if (!binaryVerified || running.Scope != "ProcessProtocolOnly") throw new InvalidOperationException("Protocol binary identity or verification scope was not retained.");
            try { await first.StartAsync(2, "Protocol", 1, true); }
            catch (InvalidOperationException) { secondJobRejected = true; }
            if (!secondJobRejected) throw new InvalidOperationException("A second diagnostic was incorrectly accepted.");
        }
        // Recreate the manager while the same child remains active. The UI then
        // invokes its real Stop button against that reconnected process.
        using var reconnect = new DiagnosticRunCoordinator(root);
        reconnect.Refresh();
        if (reconnect.Current?.RunId != stoppedId || reconnect.Current.Terminal) throw new InvalidOperationException("Active diagnostic did not reconnect after control-panel service disposal.");
        runtimeDiagnostics = reconnect;
        reconnect.Changed += RuntimeDiagnosticsChanged;
        RefreshRuntimeDiagnosticsUi();
        RuntimeStopTestButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var stopped = await WaitForDiagnosticStatus(reconnect, status => status.RunId == stoppedId && status.State == DiagnosticRunState.Stopped);
        if (stopped.WorkloadSeconds >= stopped.RequestedSeconds) throw new InvalidOperationException("Cancellation did not stop the protocol run early.");
        await WaitForDiagnosticExit(stopped);
        RefreshRuntimeDiagnosticsUi();
        if (!RuntimeTestStatus.Text.Contains("已停止",StringComparison.Ordinal) || RuntimeStopTestButton.IsEnabled || !RuntimeViewReportButton.IsEnabled)
            throw new InvalidOperationException("Stop result was not reflected in the real diagnostics UI.");
        await reconnect.StartAsync(2,"Protocol",1,true,protocolExitDelaySeconds:2);
        var completing=await WaitForDiagnosticStatus(reconnect,status=>status.RunId!=stoppedId&&status.State==DiagnosticRunState.Completing);
        using(var stillAlive=DiagnosticRunCoordinator.FindOwnedProcess(completing)??throw new InvalidOperationException("Pending completion worker should still be alive."))
        {
            RefreshRuntimeDiagnosticsUi();
            if(RuntimeShortTestButton.IsEnabled||!runtimeDiagnosticSceneSuspended||RuntimeTestStatus.Text.Contains("通過",StringComparison.Ordinal)||reconnect.MarkCompletionNotified())
                throw new InvalidOperationException("Pending completion was announced or scene resumed before the owned worker exited.");
            using var completingReconnect=new DiagnosticRunCoordinator(root);completingReconnect.Refresh();
            if(completingReconnect.Current is not{State:DiagnosticRunState.Completing,Terminal:false})throw new InvalidOperationException("Completing live worker failed to reconnect.");
        }
        var passed = await WaitForDiagnosticStatus(reconnect,status=>status.RunId != stoppedId && status.State==DiagnosticRunState.Passed);
        passedId = passed.RunId; await WaitForDiagnosticExit(passed);
        await reconnect.StartAsync(12,"Protocol",1,true);
        var crash = await WaitForDiagnosticStatus(reconnect,status=>status.RunId != passedId && status.State==DiagnosticRunState.Running && status.WorkerProcessId is not null);
        failedId=crash.RunId;
        using(var owned=DiagnosticRunCoordinator.FindOwnedProcess(crash)??throw new InvalidOperationException("Crash protocol process identity was not confirmed."))
        { owned.Kill(); await owned.WaitForExitAsync(); }
        var failed=await WaitForDiagnosticStatus(reconnect,status=>status.RunId==failedId&&status.State is DiagnosticRunState.Failed or DiagnosticRunState.Interrupted);
        if(failed.State==DiagnosticRunState.Passed)throw new InvalidOperationException("Crashed protocol was incorrectly marked PASS.");
        reconnect.Changed -= RuntimeDiagnosticsChanged;
        runtimeDiagnostics = null; ResumeRuntimeDiagnosticScene();
        using var reopened = new DiagnosticRunCoordinator(root); reopened.Refresh();
        if(reopened.Current?.RunId!=failedId||!reopened.Current.Terminal)throw new InvalidOperationException("Completion report was not durable across service reopening.");
        File.WriteAllText(Path.Combine(profile,"runtime-diagnostics-protocol.json"),JsonSerializer.Serialize(new
        {
            Succeeded=true,Scope="Tiny process protocol and WPF controls only; no stress duration or release gate claim",VisibleControls=true,
            BinaryCopyHashVerified=binaryVerified,SecondJobRejected=secondJobRejected,ActiveReconnection=true,ParentOutputDisconnected=true,StopButtonRouted=true,
            StoppedRunId=stoppedId,StoppedState=stopped.State.ToString(),PassedProtocolRunId=passedId,CrashedProtocolRunId=failedId,CrashedState=failed.State.ToString(),
            CompletingWorkerStayedAlive=true,NoPrematurePassOrNotification=true,CompletingWorkerReconnected=true,
            ReopenedPersistentReport=true,IsolatedProfiles=true,ProductionProfileTouched=false,ReportRoot=root,CompletedUtc=DateTimeOffset.UtcNow
        },DiagnosticRunCoordinator.JsonOptions));
    }

    private static async Task<DiagnosticRunStatus> WaitForDiagnosticStatus(DiagnosticRunCoordinator coordinator,Func<DiagnosticRunStatus,bool> predicate)
    {
        var ready=new TaskCompletionSource<DiagnosticRunStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged()
        {
            if(coordinator.Current is not{}status)return;
            if(predicate(status))ready.TrySetResult(status);
            else if(status.State is DiagnosticRunState.Failed or DiagnosticRunState.Interrupted or DiagnosticRunState.TimedOut)
                ready.TrySetException(new InvalidOperationException($"Diagnostic protocol reached {status.State}: {status.FirstError}"));
        }
        coordinator.Changed+=OnChanged;
        try {coordinator.Refresh();OnChanged();return await ready.Task.WaitAsync(TimeSpan.FromSeconds(25));}
        finally {coordinator.Changed-=OnChanged;}
    }
    private static async Task WaitForDiagnosticExit(DiagnosticRunStatus status)
    {
        using var process=DiagnosticRunCoordinator.FindOwnedProcess(status);
        if(process is not null)await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
}
