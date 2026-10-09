using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DesktopLife.App;

// Durable, bounded worker protocol. Cancellation is independent of the control
// panel's lifetime, and only this synthetic profile is ever opened by the worker.
internal sealed class DiagnosticWorkerSession : IDisposable
{
    private readonly string directory;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Timer cancelWatch;
    private readonly object stateGate = new();
    private DiagnosticRunStatus status;
    public CancellationToken Cancellation => cancellation.Token;
    public string ProfileDirectory { get; }
    public int Seconds => status.RequestedSeconds;
    public int Floors => status.Floors;
    public bool ProtocolOnly { get; }
    public int ProtocolExitDelaySeconds {get;private init;}

    private DiagnosticWorkerSession(string runDirectory, bool protocolOnly)
    {
        directory = Path.GetFullPath(runDirectory).TrimEnd(Path.DirectorySeparatorChar);
        ValidateRunDirectory(directory, protocolOnly);
        status = DiagnosticRunCoordinator.ReadStatus(directory) ?? throw new InvalidDataException("診斷作業資料遺失。");
        if (!string.Equals(status.RunId, Path.GetFileName(directory), StringComparison.Ordinal) || status.Terminal
            || status.Floors is < 1 or > 3 || (!protocolOnly && status.RequestedSeconds is not (600 or 7200)))
            throw new InvalidDataException("診斷作業參數不正確。");
        var manifestPath = Path.Combine(directory, "binary-manifest.json");
        if (!string.Equals(DiagnosticRunCoordinator.HashFile(manifestPath), status.BinaryManifestSha256, StringComparison.Ordinal))
            throw new InvalidDataException("診斷程式清單雜湊不符。");
        var binaryRoot = Path.Combine(directory, "binary");
        var manifest = JsonSerializer.Deserialize<DiagnosticBinaryFile[]>(File.ReadAllText(manifestPath)) ?? throw new InvalidDataException("診斷程式清單遺失。");
        foreach (var entry in manifest)
        {
            var path = Path.GetFullPath(Path.Combine(binaryRoot, entry.Name));
            if (!path.StartsWith(binaryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("診斷檔案超出副本目錄。");
            DiagnosticRunCoordinator.EnsureNoReparsePoints(path, binaryRoot);
            if (DiagnosticRunCoordinator.HashFile(path) != entry.Sha256) throw new InvalidDataException("診斷程式檔案已改變。");
        }
        if (!string.Equals(Path.GetFullPath(Environment.ProcessPath ?? ""), status.ExecutablePath, StringComparison.OrdinalIgnoreCase)
            || DiagnosticRunCoordinator.HashFile(status.ExecutablePath) != status.ExecutableSha256)
            throw new InvalidDataException("診斷未使用原始程式的不可變副本。");
        ProtocolOnly = protocolOnly;
        ProfileDirectory = Path.Combine(Path.GetTempPath(), "DesktopLifeSmoke", status.RunId);
        if (Directory.Exists(ProfileDirectory)) throw new IOException("診斷隔離資料已存在，不能重用。");
        Directory.CreateDirectory(ProfileDirectory);
        using var process = Process.GetCurrentProcess();
        status = status with { State = DiagnosticRunState.Running, Phase = "啟動隔離場景", WorkerProcessId = process.Id,
            WorkerStartedUtc = new DateTimeOffset(process.StartTime.ToUniversalTime()), LastUpdatedUtc = DateTimeOffset.UtcNow };
        Publish();
        cancelWatch = new Timer(_ =>
        {
            if (!File.Exists(Path.Combine(directory, "cancel-request.json"))) return;
            try { cancellation.Cancel(); } catch(ObjectDisposedException) { }
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(250));
    }

    public static DiagnosticWorkerSession? FromArguments(string[] args)
    {
        if (!args.Contains("--diagnostic-worker"))
        {
            if(args.Any(argument=>argument.StartsWith("--diagnostic-",StringComparison.Ordinal)))throw new ArgumentException("診斷參數必須由隔離測試程序使用。");
            return null;
        }
        if(!args.Contains("--diagnostic-protocol-only")&&!args.Contains("--stress-test"))throw new ArgumentException("隔離診斷缺少測試場景。");
        var value = args.SingleOrDefault(argument => argument.StartsWith("--diagnostic-run-directory=", StringComparison.Ordinal));
        if (value is null) throw new ArgumentException("缺少隔離診斷作業。");
        var delay=args.SingleOrDefault(argument=>argument.StartsWith("--diagnostic-protocol-exit-delay=",StringComparison.Ordinal));
        return new(value["--diagnostic-run-directory=".Length..], args.Contains("--diagnostic-protocol-only"))
            {ProtocolExitDelaySeconds=delay is not null&&int.TryParse(delay["--diagnostic-protocol-exit-delay=".Length..],out var seconds)?Math.Clamp(seconds,0,5):0};
    }

    private static void ValidateRunDirectory(string path, bool protocolOnly)
    {
        if (!Guid.TryParseExact(Path.GetFileName(path), "N", out _)) throw new ArgumentException("診斷作業編號不正確。");
        var parent = Path.GetDirectoryName(path);
        var allowed = Path.GetFullPath(Path.Combine(DiagnosticRunCoordinator.DefaultRoot, "runs"));
        if (!string.Equals(parent, allowed, StringComparison.OrdinalIgnoreCase))
        {
            // The tiny native protocol test is allowed only in TEMP/GUID/runs;
            // it cannot redirect a real run into a person's profile.
            var testRoot = parent is null ? null : Path.GetDirectoryName(parent);
            var testParent = testRoot is null ? null : Path.GetDirectoryName(testRoot);
            var tempParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DesktopLifeRuntimeDiagnostics"));
            if (!protocolOnly || Path.GetFileName(parent) != "runs" || !Guid.TryParseExact(Path.GetFileName(testRoot), "N", out _)
                || !string.Equals(testParent, tempParent, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("診斷作業不在專用目錄內。");
            allowed = testRoot!;
        }
        DiagnosticRunCoordinator.EnsureNoReparsePoints(path, string.Equals(parent, Path.Combine(DiagnosticRunCoordinator.DefaultRoot,"runs"),StringComparison.OrdinalIgnoreCase)?DiagnosticRunCoordinator.DefaultRoot:allowed);
    }

    public void Progress(double seconds, string characters, string phase = "三角色小屋場景")
    {
        lock (stateGate)
        {
            status = status with { WorkloadSeconds = seconds, WorkloadStartedUtc = status.WorkloadStartedUtc??DateTimeOffset.UtcNow.AddSeconds(-seconds), Characters = characters, Phase = cancellation.IsCancellationRequested ? "停止中，保留最後樣本" : phase,
                State = cancellation.IsCancellationRequested ? DiagnosticRunState.Stopping : DiagnosticRunState.Running, LastUpdatedUtc = DateTimeOffset.UtcNow };
            Publish();
        }
    }

    public void Finish(DiagnosticRunState state, string? error = null)
    {
        if (state is not (DiagnosticRunState.Passed or DiagnosticRunState.Failed or DiagnosticRunState.Stopped)) throw new ArgumentOutOfRangeException(nameof(state));
        lock (stateGate)
        {
            var evidence = Path.Combine(directory, "evidence");
            Directory.CreateDirectory(evidence);
            // Explicit allowlist: there is no path to the production profile,
            // and no export of actual names, memories or desktop shortcuts.
            foreach (var name in new[] { "stress-process.json", "stress-progress.json", "stress-samples.jsonl", "stress-report.json", "diagnostic-process.json" })
            {
                var source = Path.Combine(ProfileDirectory, name);
                if (File.Exists(source)) File.Copy(source, Path.Combine(evidence, name), true);
            }
            var log = Path.Combine(ProfileDirectory, "logs", "app.log");
            if (File.Exists(log)) CopyTail(log, Path.Combine(evidence, "app.log"), 2 * 1024 * 1024);
            if (state == DiagnosticRunState.Passed && !ProtocolOnly)
            {
                using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence,"stress-report.json")));
                var native = report.RootElement;
                if (status.WorkloadSeconds < status.RequestedSeconds || native.GetProperty("CompletedWorkload").GetBoolean() != true
                    || native.GetProperty("DurableReloadVerification").GetProperty("Succeeded").GetBoolean() != true)
                    throw new InvalidDataException("診斷未完成全部主場景與保存核對，不能標示通過。");
            }
            if (state == DiagnosticRunState.Passed && cancellation.IsCancellationRequested) state = DiagnosticRunState.Stopped;
            status = status with { State = DiagnosticRunState.Completing, PendingResult=state, FirstError = status.FirstError ?? error,
                Phase="結果已保存，正在結束隔離程序",LastUpdatedUtc = DateTimeOffset.UtcNow };
            Publish();
        }
    }

    public void RecordExit(int code)
    {
        lock(stateGate){status=status with{WorkerExitCode=code,LastUpdatedUtc=DateTimeOffset.UtcNow};Publish();}
    }

    private void Publish()
    {
        DiagnosticRunCoordinator.WriteStatus(directory, status);
        // The parent may close or crash. Its stdout pipe is optional; durable
        // status/report files remain the authoritative worker protocol.
        try {Console.WriteLine(JsonSerializer.Serialize(new { status.RunId, State = status.State.ToString(), status.Phase, status.WorkloadSeconds }));}
        catch(Exception ex) when(ex is IOException or ObjectDisposedException) { }
    }

    private static void CopyTail(string source, string destination, int cap)
    {
        using var input = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = File.Create(destination);
        if (input.Length > cap) input.Seek(-cap, SeekOrigin.End);
        input.CopyTo(output);
    }

    public async Task RunProtocolOnlyAsync()
    {
        // No WPF scene or stress workload: validates process handoff, immutable
        // binary identity, cooperative cancellation and durable reconnection.
        try
        {
            for (var second = 0; second < Seconds; second++) { await Task.Delay(1000, Cancellation); Progress(second + 1, "程序通訊檢查", "程序通訊檢查"); }
            Finish(DiagnosticRunState.Passed);
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested) { Finish(DiagnosticRunState.Stopped); }
        catch (Exception ex) { Finish(DiagnosticRunState.Failed, ex.Message); throw; }
    }

    public void Dispose() { cancelWatch.Dispose(); cancellation.Dispose(); }
}
