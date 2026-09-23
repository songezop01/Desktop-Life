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
        var performance=e.Args.Contains("--performance-test");
        var root = smoke||performance ? Path.Combine(Path.GetTempPath(), "DesktopLifeSmoke", Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopLife");
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
            // Prevent two instances from overwriting the same organism's state.
            try { instanceLock = new FileStream(Path.Combine(root,"instance.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) when(!smoke&&!performance)
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
            var window = new MainWindow(root, settings);
            window.ShowStorageNotice(snapshots.RecoveryMessage);
            MainWindow = window;
            window.Show();
            if(!smoke&&!performance)
                instanceChannel=new AppInstanceChannel(root,command=>Dispatcher.BeginInvoke(new Action(()=>
                {
                    if(command==AppInstanceChannel.Command.Shutdown)window.RequestExit();
                    else {window.Show();window.WindowState=WindowState.Normal;window.Activate();}
                })));
            if(smoke||performance)window.BeginDiagnostic();
            if(performance)
            {
                var minimize=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
                minimize.Tick+=(_,_)=>{minimize.Stop();window.Hide();};minimize.Start();
                var finish=new DispatcherTimer{Interval=TimeSpan.FromSeconds(45)};
                finish.Tick+=(_,_)=>{finish.Stop();window.RequestExit();};finish.Start();
            }
            SessionEnding += (_, args) => { window.PrepareExit(); if (!window.SavePetState()) args.Cancel = true; };
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
                        window.SmokeCompanion();
                        await Task.Delay(600);
                        if (!window.SavePetState() || !window.VerifyPetSave()) throw new Exception("PetState save/load mismatch.");
                        if (!window.SmokeReward()) throw new Exception("Hitbox reward routing failed.");
                        if (window.Pet.InputHitTest(new Point(0, 140)) != null) throw new Exception("Transparent corner unexpectedly hit-testable.");
                        if (window.Pet.InputHitTest(new Point(60, 112)) == null) throw new Exception("Character hitbox missing.");
                        await Task.Delay(2200);
                        await window.Pet.SmokeGravity();
                        window.SmokeRoom(Path.Combine(root,"room-check.png"));
                        await window.SmokePresentation();
                        window.SmokeGenerative(Path.Combine(root,"generative-check.png"));
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
