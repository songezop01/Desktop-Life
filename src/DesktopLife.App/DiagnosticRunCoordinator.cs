using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopLife.App;

public enum DiagnosticRunState { Preparing, Running, Stopping, Completing, Passed, Failed, Stopped, Interrupted, TimedOut }

public sealed record DiagnosticRunStatus
{
    public int Schema { get; init; } = 1;
    public string RunId { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Scope { get; init; } = "InstalledRuntimeOnly";
    public string Version { get; init; } = "";
    public string ExecutableSha256 { get; init; } = "";
    public string AssemblySha256 { get; init; } = "";
    public string BinaryManifestSha256 { get; init; } = "";
    public string ExecutablePath { get; init; } = "";
    public int RequestedSeconds { get; init; }
    public int Floors { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset LastUpdatedUtc { get; init; }
    public DateTimeOffset? FinishedUtc { get; init; }
    public DateTimeOffset? WorkerStartedUtc { get; init; }
    public int? WorkerProcessId { get; init; }
    public DiagnosticRunState State { get; init; }
    public DiagnosticRunState? PendingResult { get; init; }
    public int? WorkerExitCode { get; init; }
    public string Phase { get; init; } = "準備隔離測試";
    public double WorkloadSeconds { get; init; }
    public DateTimeOffset? WorkloadStartedUtc { get; init; }
    public string Characters { get; init; } = "橘貓、女孩、邊牧犬";
    public string? FirstError { get; init; }
    public bool Terminal => State is DiagnosticRunState.Passed or DiagnosticRunState.Failed or DiagnosticRunState.Stopped or DiagnosticRunState.Interrupted or DiagnosticRunState.TimedOut;
}

public sealed record DiagnosticBinaryFile(string Name, string Sha256);

// This service never loads the person's organism or settings. It manages only
// binaries, synthetic profiles and reports under its dedicated diagnostics area.
public sealed class DiagnosticRunCoordinator : IDisposable
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopLife", "diagnostics");
    private readonly string root;
    private readonly SemaphoreSlim startGate = new(1, 1);
    private readonly object refreshGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer reconciliation;
    private readonly HashSet<string> monitoredRuns = new(StringComparer.Ordinal);
    private readonly HashSet<StreamReader> outputReaders=[];
    private bool disposed;
    public event Action? Changed;
    public DiagnosticRunStatus? Current { get; private set; }
    public string? CurrentDirectory => Current is null ? null : Path.Combine(root, "runs", Current.RunId);

    public DiagnosticRunCoordinator(string? storageRoot = null)
    {
        root = Path.GetFullPath(storageRoot ?? DefaultRoot);
        Directory.CreateDirectory(Path.Combine(root, "runs"));
        watcher = new FileSystemWatcher(root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime, Filter = "*", EnableRaisingEvents = true };
        watcher.Changed += OnFileChanged; watcher.Created += OnFileChanged; watcher.Renamed += OnFileChanged; watcher.Deleted += OnFileChanged;
        // ReplaceFile notifications may be coalesced on Windows. This bounded
        // in-process fallback keeps a reconnected UI current without asking an
        // external agent to poll or making a missed event lose completion.
        reconciliation=new Timer(_=>{if(Current is{Terminal:false})Refresh();},null,TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1));
    }

    private void OnFileChanged(object? sender, FileSystemEventArgs e)
    {
        if (Path.GetFileName(e.FullPath) is "run-status.json" or "latest.json") Refresh();
    }

    public void Refresh()
    {
        if (disposed) return;
        lock (refreshGate) RefreshLocked();
    }

    private void RefreshLocked()
    {
        if (disposed) return;
        try
        {
            using var latest = JsonDocument.Parse(ReadText(Path.Combine(root, "latest.json")));
            var runId = latest.RootElement.GetProperty("RunId").GetString();
            if (!Guid.TryParseExact(runId, "N", out _)) return;
            var runDirectory = Path.Combine(root, "runs", runId!);
            var status = ReadStatus(runDirectory);
            if (status is null) return;
            if (!status.Terminal && status.WorkerProcessId is not null)
            {
                var process = FindOwnedProcess(status);
                if (process is null)
                {
                    // An unreadable module is not proof of exit. Keep waiting
                    // while the captured PID/start-time still belongs to a live
                    // process, even if its recorded OnExit code is already set.
                    if(CapturedProcessStillAlive(status)){Current=status;Changed?.Invoke();return;}
                    // The child can write its terminal report between the first
                    // read and process exit. Never replace that newer completion.
                    var afterExit = ReadStatus(runDirectory);
                    if (afterExit is null) return;
                    status = afterExit;
                    if (!status.Terminal)
                    {
                        status = status.PendingResult is not null&&status.WorkerExitCode is{} recordedExit
                            ?FinalizeExitedRun(runDirectory,status,recordedExit)
                            :status with { State = DiagnosticRunState.Interrupted, PendingResult=null, Phase = "測試程序已中斷", FirstError = status.FirstError ?? "測試程序未正常交回完成結果。", FinishedUtc = DateTimeOffset.UtcNow, LastUpdatedUtc = DateTimeOffset.UtcNow };
                        WriteStatus(runDirectory, status);
                    }
                }
                else
                {
                    bool add;
                    lock (monitoredRuns) add = monitoredRuns.Add(status.RunId);
                    if (add) _ = MonitorProcessAsync(process, runDirectory, status); else process.Dispose();
                }
            }
            else if (!status.Terminal && status.WorkerProcessId is null && DateTimeOffset.UtcNow - status.StartedUtc > TimeSpan.FromMinutes(2))
            {
                status = status with { State = DiagnosticRunState.Interrupted, Phase = "準備工作已中斷", FirstError = status.FirstError ?? "測試尚未啟動便中斷。", FinishedUtc = DateTimeOffset.UtcNow, LastUpdatedUtc = DateTimeOffset.UtcNow };
                WriteStatus(runDirectory, status);
            }
            Current = status;
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException) { /* Atomic replacement may briefly be unreadable. Keep the last known state. */ }
    }

    public Task StartAsync(bool longRun, int floors) => StartAsync(longRun ? 7200 : 600, longRun ? "Long" : "Short", floors, false);

    internal async Task StartAsync(int seconds, string kind, int floors, bool protocolOnly,int protocolExitDelaySeconds=0)
    {
        if (floors is < 1 or > 3 || seconds <= 0 || (!protocolOnly && seconds is not (600 or 7200))) throw new ArgumentOutOfRangeException(nameof(seconds));
        await startGate.WaitAsync();
        try
        {
            // A short cross-process lease covers preparation and handoff. The
            // persisted live process identity protects a run after UI restart.
            await using var lease = new FileStream(Path.Combine(root, "start.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Refresh();
            using var stillExiting = Current is null ? null : FindOwnedProcess(Current);
            if (Current is { Terminal: false } || stillExiting is not null) throw new InvalidOperationException("已有一項診斷正在執行，請等候完成或先停止。");
            var runId = Guid.NewGuid().ToString("N");
            var runDirectory = Path.Combine(root, "runs", runId);
            Directory.CreateDirectory(runDirectory);
            var binaryDirectory = Path.Combine(runDirectory, "binary");
            var sourceExecutable = Environment.ProcessPath ?? throw new InvalidOperationException("找不到目前程式。");
            var now = DateTimeOffset.UtcNow;
            var status = new DiagnosticRunStatus { RunId = runId, Kind = kind, RequestedSeconds = seconds, Floors = floors,
                Scope = protocolOnly ? "ProcessProtocolOnly" : "InstalledRuntimeOnly", Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown", StartedUtc = now, LastUpdatedUtc = now, State = DiagnosticRunState.Preparing };
            WriteStatus(runDirectory, status);
            AtomicWrite(Path.Combine(root, "latest.json"), JsonSerializer.Serialize(new { RunId = runId }, JsonOptions));
            lock(refreshGate)Current = status; Changed?.Invoke();
            try
            {
                var manifest = await Task.Run(() => SnapshotBinaries(AppContext.BaseDirectory, binaryDirectory));
                var executable = Path.Combine(binaryDirectory, Path.GetFileName(sourceExecutable));
                if (!File.Exists(executable)) throw new IOException("找不到診斷副本的啟動程式。");
                var manifestPath = Path.Combine(runDirectory, "binary-manifest.json");
                AtomicWrite(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
                var assemblyPath = Path.Combine(binaryDirectory, Assembly.GetExecutingAssembly().GetName().Name+".dll");
                status = status with { ExecutablePath = executable, ExecutableSha256 = HashFile(executable),
                    AssemblySha256 = File.Exists(assemblyPath) ? HashFile(assemblyPath) : "", BinaryManifestSha256 = HashFile(manifestPath), LastUpdatedUtc = DateTimeOffset.UtcNow };
                WriteStatus(runDirectory, status);
                var start = new ProcessStartInfo(executable) { WorkingDirectory = binaryDirectory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("--diagnostic-worker"); start.ArgumentList.Add("--diagnostic-run-directory=" + runDirectory);
                if (protocolOnly) {start.ArgumentList.Add("--diagnostic-protocol-only");if(protocolExitDelaySeconds>0)start.ArgumentList.Add("--diagnostic-protocol-exit-delay="+Math.Clamp(protocolExitDelaySeconds,1,5));}
                else { start.ArgumentList.Add("--stress-test"); start.ArgumentList.Add("--stress-seconds=" + seconds); start.ArgumentList.Add("--stress-presence=All"); start.ArgumentList.Add("--stress-floors=" + floors); }
                var process = Process.Start(start) ?? throw new IOException("無法啟動隔離診斷。");
                // Do not overwrite the worker's status after launching: the worker
                // exclusively owns its state until it has exited.
                lock (monitoredRuns) monitoredRuns.Add(runId);
                lock(outputReaders){outputReaders.Add(process.StandardOutput);outputReaders.Add(process.StandardError);}
                _ = DrainOutputAsync(process.StandardOutput, Path.Combine(runDirectory, "worker-output.log"));
                _ = DrainOutputAsync(process.StandardError, Path.Combine(runDirectory, "worker-error.log"));
                _ = MonitorProcessAsync(process, runDirectory, status);
            }
            catch (Exception ex)
            {
                status = status with { State = DiagnosticRunState.Failed, Phase = "無法準備診斷", FirstError = ex.Message, FinishedUtc = DateTimeOffset.UtcNow, LastUpdatedUtc = DateTimeOffset.UtcNow };
                WriteStatus(runDirectory, status); lock(refreshGate)Current = status; Changed?.Invoke();
                throw;
            }
        }
        finally { startGate.Release(); }
    }

    public void Stop()
    {
        Refresh();
        if (Current is not { Terminal: false } || Current.State==DiagnosticRunState.Completing || CurrentDirectory is not { } directory) return;
        AtomicWrite(Path.Combine(directory, "cancel-request.json"), JsonSerializer.Serialize(new { Current.RunId, RequestedUtc = DateTimeOffset.UtcNow }, JsonOptions));
    }

    public bool MarkCompletionNotified()
    {
        if (Current is not { Terminal: true } || CurrentDirectory is not { } directory) return false;
        try { using var marker = new FileStream(Path.Combine(directory, "completion-notified.marker"), FileMode.CreateNew, FileAccess.Write, FileShare.Read); return true; }
        catch (IOException) { return false; }
    }

    private async Task MonitorProcessAsync(Process process, string directory, DiagnosticRunStatus initial)
    {
        try
        {
            var timeout = initial.StartedUtc.AddSeconds(initial.RequestedSeconds * 1.5 + 120) - DateTimeOffset.UtcNow;
            var exit = process.WaitForExitAsync();
            if (await Task.WhenAny(exit, Task.Delay(timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(1))) != exit)
            {
                AtomicWrite(Path.Combine(directory, "cancel-request.json"), "{\"Reason\":\"Timeout\"}");
                if (await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(30))) != exit)
                {
                    // Terminate only the exact captured child if cooperation failed.
                    var latest = ReadStatus(directory);
                    using var owned = latest is null ? null : FindOwnedProcess(latest);
                    if (owned is not null) { owned.Kill(); await owned.WaitForExitAsync(); }
                }
                var timed = ReadStatus(directory) ?? initial;
                WriteStatus(directory, timed with { State = DiagnosticRunState.TimedOut, PendingResult=null, Phase = "診斷逾時", FirstError = timed.FirstError ?? "隔離測試超過允許時間。", FinishedUtc = DateTimeOffset.UtcNow, LastUpdatedUtc = DateTimeOffset.UtcNow });
            }
            else
            {
                await exit;
                var final = ReadStatus(directory) ?? initial;
                WriteStatus(directory,FinalizeExitedRun(directory,final,process.ExitCode));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            var latest = ReadStatus(directory) ?? initial;
            if (!latest.Terminal) WriteStatus(directory, latest with { State = DiagnosticRunState.Interrupted, Phase = "診斷連線中斷", FirstError = ex.Message, FinishedUtc = DateTimeOffset.UtcNow, LastUpdatedUtc = DateTimeOffset.UtcNow });
        }
        finally { process.Dispose(); lock(monitoredRuns)monitoredRuns.Remove(initial.RunId); Refresh(); }
    }

    private static DiagnosticRunStatus FinalizeExitedRun(string directory,DiagnosticRunStatus status,int exitCode)
    {
        if(status.Terminal&&!(exitCode!=0&&status.State==DiagnosticRunState.Passed))return status;
        var outcome=status.PendingResult??DiagnosticRunState.Failed;
        var error=status.FirstError;
        if(exitCode!=0&&outcome is DiagnosticRunState.Passed or DiagnosticRunState.Stopped)
        {outcome=DiagnosticRunState.Failed;error??=$"測試程序結束碼 {exitCode}，完成結果無法確認。";}
        if(status.PendingResult is null){outcome=DiagnosticRunState.Failed;error??=$"程序結束碼 {exitCode}，未交回有效完成結果。";}
        if(outcome is DiagnosticRunState.Passed or DiagnosticRunState.Stopped&&status.Scope=="InstalledRuntimeOnly")
        {
            try
            {
                using var report=JsonDocument.Parse(ReadText(Path.Combine(directory,"evidence","stress-report.json")));
                var native=report.RootElement;
                if(native.GetProperty("RequestedSeconds").GetInt32()!=status.RequestedSeconds||native.GetProperty("Floors").GetInt32()!=status.Floors||native.GetProperty("Presence").GetString()!="All")throw new InvalidDataException("測試報告與請求場景不符。");
                if(outcome==DiagnosticRunState.Passed&&(status.WorkloadSeconds<status.RequestedSeconds||!native.GetProperty("CompletedWorkload").GetBoolean()||!native.GetProperty("DurableReloadVerification").GetProperty("Succeeded").GetBoolean()))throw new InvalidDataException("主場景或保存核對尚未完成，不能標示通過。");
                if(outcome==DiagnosticRunState.Stopped&&!native.GetProperty("Stopped").GetBoolean())throw new InvalidDataException("停止結果未保留相符的部分場景報告。");
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
            {outcome=DiagnosticRunState.Failed;error??=ex.Message;}
        }
        return status with{State=outcome,PendingResult=null,WorkerExitCode=exitCode,FirstError=error,FinishedUtc=DateTimeOffset.UtcNow,LastUpdatedUtc=DateTimeOffset.UtcNow,
            Phase=outcome switch{DiagnosticRunState.Passed=>status.Scope=="ProcessProtocolOnly"?"程序通訊檢查完成":"場景及保存重載核對完成",DiagnosticRunState.Stopped=>"已停止，報告已保留",_=>"檢查失敗，現場已保留"}};
    }

    private async Task DrainOutputAsync(StreamReader reader, string path)
    {
        try
        {
            await using var writer = new StreamWriter(path, false);
            var retained = 0;
            while (await reader.ReadLineAsync() is { } line)
            {
                // Continue draining beyond the cap so a noisy child cannot block.
                if (retained >= 1024 * 1024) continue;
                var bounded = line.Length > 8192 ? line[..8192] : line;
                await writer.WriteLineAsync(bounded); retained += bounded.Length; await writer.FlushAsync();Refresh();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        finally {lock(outputReaders)outputReaders.Remove(reader);}
    }

    internal static Process? FindOwnedProcess(DiagnosticRunStatus status)
    {
        if (status.WorkerProcessId is not { } id || status.WorkerStartedUtc is not { } started || string.IsNullOrEmpty(status.ExecutablePath)) return null;
        Process? process = null;
        try
        {
            process = Process.GetProcessById(id);
            if (process.HasExited || Math.Abs((process.StartTime.ToUniversalTime() - started.UtcDateTime).TotalSeconds) > 1
                || !string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? ""), Path.GetFullPath(status.ExecutablePath), StringComparison.OrdinalIgnoreCase))
            { process.Dispose(); return null; }
            return process;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { process?.Dispose(); return null; }
    }

    private static bool CapturedProcessStillAlive(DiagnosticRunStatus status)
    {
        if(status.WorkerProcessId is not{}id||status.WorkerStartedUtc is not{}started)return false;
        try
        {
            using var process=Process.GetProcessById(id);
            return !process.HasExited&&Math.Abs((process.StartTime.ToUniversalTime()-started.UtcDateTime).TotalSeconds)<=1;
        }
        catch(ArgumentException){return false;}
        catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException){return true;}
    }

    private static DiagnosticBinaryFile[] SnapshotBinaries(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        var entries = new List<DiagnosticBinaryFile>();
        foreach (var path in EnumerateBinaryFiles(source))
        {
            EnsureNoReparsePoints(path, source);
            var relative = Path.GetRelativePath(source, path);
            var copied = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
            File.Copy(path, copied, false);
            var hash = HashFile(copied);
            if (!string.Equals(hash, HashFile(path), StringComparison.Ordinal)) throw new IOException("程式檔案在複製期間改變，請重新啟動診斷。");
            File.SetAttributes(copied, File.GetAttributes(copied) | FileAttributes.ReadOnly);
            entries.Add(new(relative, hash));
        }
        return entries.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> EnumerateBinaryFiles(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("程式副本不能包含重新導向或連結。");
            if ((attributes & FileAttributes.Directory) != 0) { foreach (var child in EnumerateBinaryFiles(path)) yield return child; }
            else yield return path;
        }
    }

    internal static void EnsureNoReparsePoints(string path, string stopAt)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("診斷路徑不能使用重新導向或連結。");
            if (string.Equals(current.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(stopAt).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return;
        }
        throw new IOException("診斷檔案不在允許的目錄內。");
    }

    internal static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    internal static DiagnosticRunStatus? ReadStatus(string directory)
    {
        try { return JsonSerializer.Deserialize<DiagnosticRunStatus>(ReadText(Path.Combine(directory, "run-status.json")), JsonOptions); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    internal static void WriteStatus(string directory, DiagnosticRunStatus status) => AtomicWrite(Path.Combine(directory, "run-status.json"), JsonSerializer.Serialize(status, JsonOptions));
    private static string ReadText(string path)
    {
        // Windows readers must permit replacement, otherwise a progress read can
        // prevent the worker's atomic terminal-status rename.
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var reader=new StreamReader(input);return reader.ReadToEnd();
    }
    internal static void AtomicWrite(string path, string text)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text);
            for(var attempt=0;;attempt++)
            {
                try {if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);break;}
                catch(Exception ex) when(attempt<5&&ex is IOException or UnauthorizedAccessException)
                { Thread.Sleep(20*(attempt+1)); }
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Dispose()
    {
        disposed = true; reconciliation.Dispose(); watcher.Dispose();
        StreamReader[] readers;lock(outputReaders){readers=outputReaders.ToArray();outputReaders.Clear();}
        foreach(var reader in readers)reader.Dispose();
    }
}
