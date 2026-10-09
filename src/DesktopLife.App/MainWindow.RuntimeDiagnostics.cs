using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopLife.App;

public partial class MainWindow
{
    private DiagnosticRunCoordinator? runtimeDiagnostics;
    private readonly DispatcherTimer runtimeDiagnosticClock = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool runtimeDiagnosticSceneSuspended;
    private bool runtimeDiagnosticStarting;
    private bool runtimeDiagnosticPreviousPause, runtimeDiagnosticLifeWasRunning, runtimeDiagnosticLearningWasRunning,
        runtimeDiagnosticVisibilityWasRunning, runtimeDiagnosticIconsWereRunning, runtimeDiagnosticClockWasRunning;

    private void InitializeRuntimeDiagnosticsUi(object sender, RoutedEventArgs e)
    {
        if (runtimeDiagnostics is not null || Environment.GetCommandLineArgs().Any(argument => argument is "--diagnostic-worker" or "--smoke-test" or "--house-test" or "--stress-test" or "--performance-test" or "--restart-verify-test" or "--runtime-diagnostics-test" or "--food-test")) return;
        try
        {
            runtimeDiagnostics = new DiagnosticRunCoordinator();
            runtimeDiagnostics.Changed += RuntimeDiagnosticsChanged;
            runtimeDiagnosticClock.Tick += (_, _) => RefreshRuntimeDiagnosticsUi();
            runtimeDiagnosticClock.Start();
            Closed += (_, _) => { runtimeDiagnosticClock.Stop(); runtimeDiagnostics.Changed -= RuntimeDiagnosticsChanged; runtimeDiagnostics.Dispose(); };
            // File and process reconciliation stays off the dispatcher.
            _ = Task.Run(runtimeDiagnostics.Refresh);
            RefreshRuntimeDiagnosticsUi();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { RuntimeTestStatus.Text = "無法建立診斷紀錄：" + ex.Message; }
    }

    private void RuntimeDiagnosticsChanged()
    {
        if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke(new Action(RefreshRuntimeDiagnosticsUi));
    }

    private async void StartRuntimeShortTest(object sender, RoutedEventArgs e) => await StartRuntimeTestAsync(false);
    private async void StartRuntimeLongTest(object sender, RoutedEventArgs e) => await StartRuntimeTestAsync(true);

    private async Task StartRuntimeTestAsync(bool longRun)
    {
        if (runtimeDiagnostics is null || runtimeDiagnosticStarting || saveClosing) return;
        runtimeDiagnosticStarting = true;
        RuntimeShortTestButton.IsEnabled = RuntimeLongTestButton.IsEnabled = false;
        try
        {
            if (!await SavePetStateAsync()) { RuntimeTestStatus.Text = "請先排除正式資料保存問題，再執行診斷。"; return; }
            SuspendRuntimeDiagnosticScene();
            RuntimeTestStatus.Text = "正在建立隔離程式及測試資料……";
            await runtimeDiagnostics.StartAsync(longRun, Pet.House?.FloorCount ?? 1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { RuntimeTestStatus.Text = "診斷無法開始：" + ex.Message; }
        finally
        {
            runtimeDiagnosticStarting = false;
            if (runtimeDiagnostics.Current is not { Terminal: false }) ResumeRuntimeDiagnosticScene();
            RefreshRuntimeDiagnosticsUi();
        }
    }

    private void StopRuntimeTest(object sender, RoutedEventArgs e)
    {
        try
        {
            runtimeDiagnostics?.Stop(); RuntimeStopTestButton.IsEnabled = false;
            RuntimeTestStatus.Text = "正在停止，測試程序會保存最後樣本與報告。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { RuntimeTestStatus.Text = "無法送出停止要求：" + ex.Message; }
    }

    private void ViewRuntimeTestReport(object sender, RoutedEventArgs e)
    {
        if (runtimeDiagnostics?.CurrentDirectory is not { } directory || !Directory.Exists(directory)) return;
        try { Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { RuntimeTestStatus.Text = "無法開啟診斷報告：" + ex.Message; }
    }

    private void RefreshRuntimeDiagnosticsUi()
    {
        if (saveClosing || runtimeDiagnostics is null) return;
        var status = runtimeDiagnostics.Current;
        var active = status is { Terminal: false };
        RuntimeShortTestButton.IsEnabled = RuntimeLongTestButton.IsEnabled = !active && !runtimeDiagnosticStarting;
        RuntimeStopTestButton.IsEnabled = active && status!.State is not (DiagnosticRunState.Stopping or DiagnosticRunState.Completing);
        RuntimeViewReportButton.IsEnabled = status is not null;
        if (status is null) return;
        if (active) SuspendRuntimeDiagnosticScene(); else if (!runtimeDiagnosticStarting) ResumeRuntimeDiagnosticScene();
        var title = status.Kind == "Long" ? "程式內長測" : "程式內短測";
        var outcome = status.State switch
        {
            DiagnosticRunState.Passed => "通過", DiagnosticRunState.Failed => "失敗", DiagnosticRunState.Stopped => "已停止（未完成）",
            DiagnosticRunState.Interrupted => "已中斷（未完成）", DiagnosticRunState.TimedOut => "逾時（未完成）",
            DiagnosticRunState.Stopping => "停止中", DiagnosticRunState.Completing => "正在結束測試程序", DiagnosticRunState.Preparing => "準備中", _ => "執行中"
        };
        RuntimeTestStatus.Text = $"{title} · {outcome} · {status.Phase}";
        RuntimeTestProgress.Maximum = status.RequestedSeconds;
        RuntimeTestProgress.Value = Math.Min(status.RequestedSeconds, status.WorkloadSeconds);
        var elapsed = TimeSpan.FromSeconds(Math.Max(0, status.WorkloadSeconds));
        var expected = (status.WorkloadStartedUtc??status.StartedUtc).AddSeconds(status.RequestedSeconds);
        RuntimeTestDetails.Text = $"測試編號 {status.RunId}\n主場景 {elapsed:hh\\:mm\\:ss}／{TimeSpan.FromSeconds(status.RequestedSeconds):hh\\:mm\\:ss} · 預計場景完成 {expected.LocalDateTime:MM/dd HH:mm:ss}\n最後更新 {status.LastUpdatedUtc.LocalDateTime:HH:mm:ss} · {status.Floors} 層樓\n{status.Characters}\n版本 {status.Version} · 程式雜湊 {status.ExecutableSha256}";
        if (status.FirstError is { } error) RuntimeTestDetails.Text += "\n首個問題：" + error;
        if (status.Terminal && tray is not null && runtimeDiagnostics.MarkCompletionNotified())
            tray.ShowBalloonTip(5000, "Desktop Life 診斷完成", $"{title}：{outcome}。可在進階／診斷查看保留的報告。", status.State == DiagnosticRunState.Passed ? Forms.ToolTipIcon.Info : Forms.ToolTipIcon.Warning);
    }

    private void SuspendRuntimeDiagnosticScene()
    {
        if (runtimeDiagnosticSceneSuspended || saveClosing) return;
        runtimeDiagnosticPreviousPause = aiPaused;
        runtimeDiagnosticLifeWasRunning = lifeTimer.IsEnabled; runtimeDiagnosticLearningWasRunning = learningTimer.IsEnabled;
        runtimeDiagnosticVisibilityWasRunning = visibilityTimer.IsEnabled; runtimeDiagnosticIconsWereRunning = iconTimer.IsEnabled;
        runtimeDiagnosticClockWasRunning = lifeClock.IsRunning;
        TickLife(); SetPaused(true); runtimeDiagnosticSceneSuspended = true;
        lifeTimer.Stop(); learningTimer.Stop(); visibilityTimer.Stop(); iconTimer.Stop(); lifeClock.Stop(); audio?.Stop();
        foreach (var surface in Pet.Surfaces.Concat(characterWindows).Distinct()) if (surface.IsVisible) SuspendCharacterPresence(surface);
        foreach (var window in characterWindows) window.SetSimulationEnabled(false);
        HitStatus.Text = "診斷期間正式場景暫停；完成或停止後自動恢復。";
    }

    private void ResumeRuntimeDiagnosticScene()
    {
        if (!runtimeDiagnosticSceneSuspended || saveClosing) return;
        runtimeDiagnosticSceneSuspended = false;
        lastLifeTick = lifeClock.Elapsed; lastLearningTick = lifeClock.Elapsed.TotalSeconds;
        if (runtimeDiagnosticClockWasRunning) lifeClock.Start();
        foreach (var window in characterWindows) window.SetSimulationEnabled(true);
        SetPaused(runtimeDiagnosticPreviousPause);
        if (runtimeDiagnosticLifeWasRunning) lifeTimer.Start(); if (runtimeDiagnosticLearningWasRunning) learningTimer.Start();
        if (runtimeDiagnosticVisibilityWasRunning) visibilityTimer.Start(); if (runtimeDiagnosticIconsWereRunning && !iconClosing) iconTimer.Start();
        UpdatePetVisibility();
    }
}
