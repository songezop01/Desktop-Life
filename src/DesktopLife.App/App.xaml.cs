using System.IO;
using System.Windows;
using System.Windows.Threading;
using DesktopLife.Core;

namespace DesktopLife.App;
public partial class App : Application
{
    private FileStream? instanceLock;
    private AppInstanceChannel? instanceChannel;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Contains("--icons-read-test")||e.Args.Contains("--icons-restore-test"))
        {Shutdown(IconIntegrationCheck.Run(e.Args.Contains("--icons-restore-test")));return;}
        var smoke = e.Args.Contains("--smoke-test");
        var house=e.Args.Contains("--house-test");
        var stress=e.Args.Contains("--stress-test");
        var performance=e.Args.Contains("--performance-test");
        var restart=e.Args.Contains("--restart-verify-test");
        var root = smoke||house||performance||stress ? Path.Combine(Path.GetTempPath(), "DesktopLifeSmoke", Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopLife");
        if(restart)
        {
            var argument=e.Args.SingleOrDefault(value=>value.StartsWith("--restart-root=",StringComparison.Ordinal));
            var requested=argument is null?"":Path.GetFullPath(argument["--restart-root=".Length..]).TrimEnd(Path.DirectorySeparatorChar);
            var allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopLifeSmoke"));
            if(!string.Equals(Path.GetDirectoryName(requested),allowed,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(requested),"N",out _)||!File.Exists(Path.Combine(requested,"stress-report.json")))
            {Shutdown(2);return;}
            root=requested;
        }
        if(e.Args.Contains("--shutdown"))
        {
            // Exit code 2 means the running app could not finish a safe shutdown; never force-terminate it.
            Shutdown(AppInstanceChannel.ShutdownAsync(root).GetAwaiter().GetResult()?0:2);
            return;
        }
        var log = new FileAppLog(Path.Combine(root, "logs", "app.log"));
        try
        {
            Directory.CreateDirectory(root);
            if(smoke||house||stress||performance||restart)File.WriteAllText(Path.Combine(root,"diagnostic-process.json"),System.Text.Json.JsonSerializer.Serialize(new{ProcessId=Environment.ProcessId,StartedUtc=DateTimeOffset.UtcNow,Executable=Environment.ProcessPath,Arguments=e.Args}));
            // Prevent two instances from overwriting the same organism's state.
            try { instanceLock = new FileStream(Path.Combine(root,"instance.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) when(!smoke&&!house&&!performance&&!stress)
            {
                if(AppInstanceChannel.SendAsync(root,AppInstanceChannel.Command.Activate).GetAwaiter().GetResult())
                {Shutdown();return;}
                MessageBox.Show("Desktop Life 已在執行。請從右下角托盤開啟控制台；若正在更新舊版，請先在托盤選單選擇「結束」，再啟動新版。", "Desktop Life");
                Shutdown(2);return;
            }
            var store = new SettingsStore(Path.Combine(root, "settings.json"));
            var snapshots=new OrganismStore(root);
            var imported=snapshots.ApplyPendingImport();
            var snapshot=snapshots.Exists?snapshots.Load():null;
            var settings=snapshot?.Settings??new AppSettings();
            if(!imported&&File.Exists(Path.Combine(root,"settings.json")))
            {
                try{settings=store.Load();}
                catch(Exception ex) when(snapshot is not null&&ex is System.Text.Json.JsonException or InvalidDataException)
                {
                    File.Copy(Path.Combine(root,"settings.json"),Path.Combine(root,"settings.damaged-"+Guid.NewGuid().ToString("N")+".json"));
                    log.Write("Settings recovered from organism snapshot.");
                }
            }
            store.Save(settings);
            log.Write("Startup: Desktop Life companion edition, local care and relationship memory.");
            var restartExpected=restart?snapshot:null;
            var window = new MainWindow(root, settings);
            window.ShowStorageNotice(snapshots.RecoveryMessage);
            MainWindow = window;
            if(restart)window.FreezeRestartVerification();
            window.Show();
            if(!smoke&&!house&&!performance&&!stress&&!restart)
                instanceChannel=new AppInstanceChannel(root,command=>Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(command==AppInstanceChannel.Command.Shutdown)window.RequestExit();
                    else {window.Show();window.WindowState=WindowState.Normal;window.Activate();}
                })));
            if(smoke||house||performance||stress)window.BeginDiagnostic();
            if(restart)
            {
                Dispatcher.BeginInvoke(new Action(async()=>
                {
                    try
                    {
                        window.FreezeRestartVerification();
                        if(!await window.SavePetStateAsync())throw new IOException("Restart save failed");
                        var actual=snapshots.Load();
                        if(restartExpected is null||!RestartHistoryVerification.IsPreserved(restartExpected,actual,DateTimeOffset.UtcNow))
                        {
                            if(restartExpected is not null)File.WriteAllText(Path.Combine(root,"restart-expected-history.json"),RestartHistoryVerification.History(restartExpected).ToJsonString());
                            File.WriteAllText(Path.Combine(root,"restart-actual-history.json"),RestartHistoryVerification.History(actual).ToJsonString());
                            throw new InvalidDataException("Restart changed persisted character history or room identities");
                        }
                        File.WriteAllText(Path.Combine(root,"restart-report.json"),System.Text.Json.JsonSerializer.Serialize(new{Succeeded=true,ProcessId=Environment.ProcessId,VerifiedUtc=DateTimeOffset.UtcNow,Schema=actual.SchemaVersion,Characters=Enum.GetValues<PetAppearance>().Select(kind=>actual.GetCharacter(kind)?.Kind.ToString()).ToArray(),Compared="All persisted history and settings; room identities/kinds. Needs, runtime and positions may advance."}));
                        log.Write("Restart PASS: new process loaded preserved history and saved safely");window.RequestExit();
                    }
                    catch(Exception ex){log.Write("Restart FAIL: "+ex);Shutdown(2);}
                }),DispatcherPriority.ApplicationIdle);
            }
            if(performance)
            {
                var argument=e.Args.FirstOrDefault(a=>a.StartsWith("--performance-seconds=",StringComparison.Ordinal));
                var duration=argument is not null&&int.TryParse(argument.Split('=')[1],out var parsed)?Math.Clamp(parsed,10,600):45;
                var process=System.Diagnostics.Process.GetCurrentProcess();
                var startedUtc=DateTimeOffset.UtcNow;
                var started=System.Diagnostics.Stopwatch.StartNew();var cpu=process.TotalProcessorTime.TotalSeconds;
                var allocated=GC.GetTotalAllocatedBytes();var gen0=GC.CollectionCount(0);var gen1=GC.CollectionCount(1);var gen2=GC.CollectionCount(2);
                object? setupPerformance=null;double steadyMeasurementStartedAtSeconds=0;
                var minimize=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
                minimize.Tick+=(_,_)=>
                {minimize.Stop();window.Hide();setupPerformance=window.BeginSteadyMeasurement();steadyMeasurementStartedAtSeconds=started.Elapsed.TotalSeconds;};minimize.Start();
                var finish=new DispatcherTimer{Interval=TimeSpan.FromSeconds(duration)};
                finish.Tick+=async (_,_)=>
                {
                    finish.Stop();
                    try
                    {
                        if(!await window.SavePetStateAsync())throw new IOException("Performance run could not persist state.");process.Refresh();
                        var seconds=started.Elapsed.TotalSeconds;
                        var report=new
                        {
                            ProcessId=process.Id,RequestedSeconds=duration,Seconds=seconds,StartedUtc=startedUtc,SteadyStartedUtc=startedUtc.AddSeconds(steadyMeasurementStartedAtSeconds),
                            OneCoreCpuPercent=100*(process.TotalProcessorTime.TotalSeconds-cpu)/seconds,
                            MachineCpuPercent=100*(process.TotalProcessorTime.TotalSeconds-cpu)/seconds/Environment.ProcessorCount,
                            AllocatedMB=(GC.GetTotalAllocatedBytes()-allocated)/1048576d,ManagedBytes=GC.GetTotalMemory(false),
                            Gen0=GC.CollectionCount(0)-gen0,Gen1=GC.CollectionCount(1)-gen1,Gen2=GC.CollectionCount(2)-gen2,
                            WorkingSetMB=process.WorkingSet64/1048576d,PrivateMB=process.PrivateMemorySize64/1048576d,
                            FurnitureContextRebuilds=window.Pet.Furniture.Sum(f=>f.ContextRebuilds),SetupPerformance=setupPerformance,
                            SteadyMeasurementStartedAtSeconds=steadyMeasurementStartedAtSeconds,Performance=window.CapturePerformance()
                        };
                        File.WriteAllText(Path.Combine(root,"performance-report.json"),System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
                        log.Write("Performance PASS");window.RequestExit();
                    }
                    catch(Exception ex){log.Write("Performance FAIL: "+ex);Shutdown(2);}
                };finish.Start();
            }
            SessionEnding += (_, args) => { window.PrepareExit(); if (!window.SavePetState()) args.Cancel = true; };
            if(stress)
            {
                var argument=e.Args.FirstOrDefault(a=>a.StartsWith("--stress-seconds=",StringComparison.Ordinal));
                var duration=argument is not null&&int.TryParse(argument.Split('=')[1],out var parsed)?Math.Clamp(parsed,60,28800):600;
                var presenceArgument=e.Args.FirstOrDefault(a=>a.StartsWith("--stress-presence=",StringComparison.Ordinal));
                var presenceName=presenceArgument?.Split('=')[1]??"Both";
                var stressPresence=presenceName.Equals("Both",StringComparison.OrdinalIgnoreCase)?PresenceMode.Both:
                    presenceName.Equals("All",StringComparison.OrdinalIgnoreCase)?PresenceMode.All:throw new ArgumentException("Stress presence must be Both or All.");
                var floorArgument=e.Args.FirstOrDefault(a=>a.StartsWith("--stress-floors=",StringComparison.Ordinal));
                var stressFloors=floorArgument is null?1:int.Parse(floorArgument.Split('=')[1]);
                if(stressFloors is <1 or >3)throw new ArgumentOutOfRangeException("stress-floors");
                Dispatcher.BeginInvoke(new Action(async()=>{try{await window.RunHomeStress(root,duration,stressPresence,stressFloors);log.Write("Stress PASS: "+stressPresence);}catch(Exception ex){log.Write("Stress FAIL: "+ex);Shutdown(2);}}));
            }
            if(house)
            {
                Dispatcher.BeginInvoke(new Action(async()=>
                {
                    try
                    {
                        await Task.Delay(900);
                        window.SmokeIllustrations(root);
                        window.Pet.SmokeHouseTravel(root);
                        window.Pet.SmokeHouseToyFloor(root);
                        window.Pet.SmokeHouseSequences(root);
                        window.Pet.SmokeScaledToyContacts(root);
                        window.Pet.SmokeLowToySupports(root);
                        window.Pet.SmokeHouseMovingGoals(root);
                        window.PrepareHousePreview(root);
                        await window.SmokeDisplays(root);
                        window.RenderHousePreview(Path.Combine(root,"house-final.png"));
                        if(!await window.SavePetStateAsync())throw new IOException("House diagnostic save failed");
                        var loaded=snapshots.Load();loaded.Validate();
                        File.WriteAllText(Path.Combine(root,"house-summary.json"),System.Text.Json.JsonSerializer.Serialize(new{Succeeded=true,Schema=loaded.SchemaVersion,LoadedFloors=loaded.Learning.Room.FloorCount,CompletedUtc=DateTimeOffset.UtcNow,InstalledDataTouched=false}));
                        log.Write("House PASS: production floors, stairs, proportions, native mixed-DPI placements and schema roundtrip");window.RequestExit();
                    }
                    catch(Exception ex){log.Write("House FAIL: "+ex);Shutdown(2);}
                }),DispatcherPriority.ApplicationIdle);
            }
            if (smoke)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                timer.Tick += async (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        if (!window.IsLoaded || !window.Pet.IsLoaded) throw new Exception("Window not loaded.");
                        if (window.LatestEnvironment is not { } state || (DateTimeOffset.UtcNow - state.Timestamp).TotalSeconds > 3)
                            throw new Exception("Sensor UI did not receive a fresh snapshot.");
                        if (state.IdleSeconds.Value is null || state.CpuPercent.Value is not null) throw new Exception("Companion presence sample invalid.");
                        window.Life.State.Validate();
                        if (window.Life.TotalRuntimeSeconds < 3 || window.Life.Feeding.Level is null || window.Life.State == new PetState())
                            throw new Exception("Homeostasis did not advance from live samples.");
                        window.SmokeIllustrations(root);
                        window.SmokeCompanion();
                        await Task.Delay(600);
                        if (!window.SavePetState() || !window.VerifyPetSave()) throw new Exception("PetState save/load mismatch.");
                        if (!window.SmokeReward()) throw new Exception("Hitbox reward routing failed.");
                        if (window.Pet.InputHitTest(new Point(0, window.Pet.Height-2)) != null) throw new Exception("Transparent corner unexpectedly hit-testable.");
                        if (window.Pet.InputHitTest(window.Pet.DiagnosticHitPoint()) == null) throw new Exception("Character hitbox missing.");
                        await Task.Delay(2200);
                        await window.Pet.SmokeGravity();
                        window.SmokeRoom(Path.Combine(root,"room-check.png"));
                        await window.SmokePresentation();
                        window.SmokeGenerative(Path.Combine(root,"generative-check.png"));
                        window.SmokeIdentity(root);
                        await window.SmokeDualPresence();
                        window.SmokeCharacterLife(root);
                        await window.SmokeBackgroundSaving(root);
                        window.RenderCompanionPanel(Path.Combine(root,"companion-panel.png"));
                        window.Pet.RenderDiagnosticArt(Path.Combine(root,"appearance-check.png"));
                        foreach (var action in Enum.GetValues<BodyAction>()) window.Pet.SetAction(action);
                        window.Pet.Hide(); window.Pet.Show(); window.Pet.ResetPosition();
                        log.Write("Smoke PASS: live homeostasis; state save/load; routed hitbox reward; companion care; presence-only sensor; windows; action poses; hide/show/reset.");
                        window.RequestExit();
                    }
                    catch (Exception ex) { log.Write("Smoke FAIL: " + ex.Message);window.Pet.RenderDiagnosticArt(Path.Combine(root,"failed-art.png")); Shutdown(2); }
                };
                timer.Start();
            }
            Exit += (_, _) => log.Write("Normal shutdown.");
        }
        catch (Exception ex)
        {
            log.Write("Startup failed: " + ex);
            MessageBox.Show("啟動失敗，請檢查設定與紀錄：\n" + root + "\n" + ex.Message, "Desktop Life");
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { instanceChannel?.Dispose(); instanceLock?.Dispose(); base.OnExit(e); }
}
