using System.Windows;
using DesktopLife.Core;
using DesktopLife.Windows;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Microsoft.Win32;
namespace DesktopLife.App;
public partial class MainWindow : Window
{
    public PetWindow Pet { get; } = new();
    private readonly CancellationTokenSource sensorCancellation = new();
    private Task? sensorTask;
    public EnvironmentState? LatestEnvironment { get; private set; }
    public HomeostasisSession Life { get; }
    private readonly OrganismStore organismStore;
    private readonly Stopwatch lifeClock = new();
    private readonly DispatcherTimer lifeTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TimeSpan lastLifeTick;
    private TimeSpan lastSaveTick;
    private DateTimeOffset? suspendedAt;
    public MainWindow(string dataRoot, AppSettings settings)
    {
        InitializeComponent();
        organismStore = new OrganismStore(dataRoot);
        var checkpoint=organismStore.LoadOrMigrate(settings,DateTimeOffset.UtcNow);
        var saved = checkpoint.Pet;
        personality=checkpoint.Personality;
        Life = new(saved.State, saved.TotalRuntimeSeconds);
        Life.ApplyCompanionOffline(DateTimeOffset.UtcNow - saved.LastSaveTime);
        // Persist the applied offline interval once, so rapid restarts cannot reapply it.
        InitializeLearning(dataRoot,settings,checkpoint.Learning);
        if(!SavePetState())throw new IOException("初始狀態無法保存。");
        ShowHomeostasis();
        SettingsText.Text = "本機陪伴與偏好記憶，不連接雲端。真實桌面圖示互動需另外啟用；移動前備份，可一鍵恢復。";
        Actions.ItemsSource = Enum.GetValues<BodyAction>();
        lifeTimer.Tick += (_, _) => TickLife();
        Loaded += (_, _) =>
        {
            Pet.Show(); lifeClock.Start(); lifeTimer.Start();
            InitializeTray();
            SystemEvents.PowerModeChanged += PowerChanged;
            sensorTask ??= Task.Run(() => MonitorAsync(sensorCancellation.Token));
        };
        Closing += (_,e)=>{if(!explicitExit&&this.settings.CloseBehavior==CloseBehavior.Tray){e.Cancel=true;Hide();}};
        Closing += (_, e) => { if(e.Cancel)return; TickLife(); if (!SavePetState()) e.Cancel = true; };
        Closed += (_, _) =>
        {
            lifeTimer.Stop(); SystemEvents.PowerModeChanged -= PowerChanged;
            sensorCancellation.Cancel(); Pet.Close();
        };
        InitializeDesktopIcons(dataRoot);
        InitializeCompanion();
        InitializeRoom();
        InitializeAudio(dataRoot);
        InitializeDisplays();
    }
    private bool explicitExit;
    public void PrepareExit()=>explicitExit=true;
    public void RequestExit(){explicitExit=true;Close();}
    private void ChangeCloseBehavior(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {if(settingsStore is null||CloseOptions.SelectedItem is not CloseBehavior behavior)return;settings=settings with{CloseBehavior=behavior};settingsStore.Save(settings);}
    private PetSnapshot Snapshot() => new() { State = Life.State, LastSaveTime = DateTimeOffset.UtcNow, TotalRuntimeSeconds = Life.TotalRuntimeSeconds };
    public bool SavePetState()
    {
        try
        {

            Learning.State.Room=Pet.CaptureRoom();
            Learning.State.Artworks=Pet.Art.Works.ToList();
            organismStore.Save(new OrganismSnapshot{Pet=Snapshot(),Learning=Learning.State,Personality=personality,Settings=settings});
            lastSaveTick = lifeClock.Elapsed;
            SaveStatus.Text = $"本機生理狀態已保存 {DateTime.Now:HH:mm:ss} · 每 60 秒自動保存";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveStatus.Text = $"保存失敗：{ex.Message}。請排除寫入問題後再試；保存成功前會保留視窗。";
            return false;
        }
    }
    public bool VerifyPetSave()
    {
        var saved=organismStore.Load();
        return saved.Pet.State==Life.State && saved.Learning.PositiveRewards==Learning.State.PositiveRewards
            && saved.Learning.Companion.Bond==Learning.State.Companion.Bond
            && saved.Learning.Companion.Memories.Count==Learning.State.Companion.Memories.Count;
    }
    private void TickLife()
    {
        if (suspendedAt is not null) return;
        var elapsed = lifeClock.Elapsed;
        Life.AdvanceCompanion(elapsed-lastLifeTick,Pet.PhysiologicalAction,LatestEnvironment?.IdleSeconds.Value is <300);
        TickCompanion();
        lastLifeTick = elapsed;
        ShowHomeostasis();
        if (elapsed - lastSaveTick >= TimeSpan.FromSeconds(60)) SavePetState();
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        // SystemEvents arrives off the UI thread; preserve session/save ordering on the dispatcher.
        Dispatcher.Invoke(() =>
        {
            if (e.Mode == PowerModes.Suspend && suspendedAt is null)
            {
                TickLife(); SavePetState(); suspendedAt = DateTimeOffset.UtcNow;
            }
            else if (e.Mode == PowerModes.Resume && suspendedAt is { } began)
            {
                Life.ApplyCompanionOffline(DateTimeOffset.UtcNow - began);
                suspendedAt = null; lastLifeTick = lifeClock.Elapsed;
                LatestEnvironment = null; // do not reuse a pre-suspend activity sample
                SavePetState(); ShowHomeostasis();
            }
        });
    }
    private sealed record StateRow(string Name, double Value);
    private void ShowHomeostasis()
    {
        if(!IsVisible)return;
        var s = Life.State;
        HomeostasisRows.ItemsSource = new[]
        {
            new StateRow("能量",s.Energy), new StateRow("飢餓",s.Hunger),
            new StateRow("心情",s.Mood), new StateRow("無聊",s.Boredom),
            new StateRow("好奇",s.Curiosity), new StateRow("孤單",s.Loneliness),
            new StateRow("興奮",s.Excitement), new StateRow("疲勞",s.Fatigue)
        };
        FeedingStatus.Text = "飯飯補充能量，睡眠恢復精神；玩耍與陪伴讓牠開心。";
    }
    private async Task MonitorAsync(CancellationToken cancellation)
    {
        try
        {
            using var sensor = new CompanionPresenceSensor();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            do
            {
                var snapshot = sensor.Sample();
                await Dispatcher.InvokeAsync(() => ShowEnvironment(snapshot), System.Windows.Threading.DispatcherPriority.Background, cancellation);
            } while (await timer.WaitForNextTickAsync(cancellation));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!cancellation.IsCancellationRequested)
                await Dispatcher.InvokeAsync(() => SensorStatus.Text = $"感測器已停止：{ex.GetType().Name}。角色可繼續使用，重新啟動可重試。");
        }
    }
    private sealed record SensorRow(string Name, string Value, string Status);
    private static SensorRow Row(string name, SensorValue reading, string unit, double scale = 1) =>
        new(name, reading.Value is { } value ? $"{value / scale:F1} {unit}" : "無資料", reading.Status=="OK"?"正常":reading.Status);
    private void ShowEnvironment(EnvironmentState state)
    {
        LatestEnvironment = state;
        if(!IsVisible)return;
        SensorStatus.Text = $"每秒更新 · {state.Timestamp.ToLocalTime():HH:mm:ss} · 取樣 {state.SampleMilliseconds:F1} ms";
        SensorRows.ItemsSource = new[]
        {
            Row("處理器", state.CpuPercent, "%"), Row("顯示卡（最忙引擎）", state.GpuPercent, "%"),
            Row("記憶體", state.RamPercent, "%"), Row("磁碟讀取", state.DiskReadBytesPerSecond, "KiB/s", 1024),
            Row("磁碟寫入", state.DiskWriteBytesPerSecond, "KiB/s", 1024),
            Row("網路上傳", state.NetworkUploadBytesPerSecond, "KiB/s", 1024),
            Row("網路下載", state.NetworkDownloadBytesPerSecond, "KiB/s", 1024),
            Row("使用者閒置", state.IdleSeconds, "秒"), Row("滑鼠取樣位移", state.MousePixelsPerSecond, "像素／秒")
        };
    }
    private void ShowPet(object sender, RoutedEventArgs e) {userHidden=false;UpdatePetVisibility();}
    private void HidePet(object sender, RoutedEventArgs e) {userHidden=true;UpdatePetVisibility();}
    private void ResetPet(object sender, RoutedEventArgs e) => Pet.ResetPosition();
    private void ClearArt(object sender,RoutedEventArgs e){Pet.Art.ClearWorks();SavePetState();}
    private void KeepArt(object sender,RoutedEventArgs e){Pet.Art.KeepWorks();SavePetState();}
    private void ToggleArt(object sender,RoutedEventArgs e)=>Pet.Art.ToggleWorks();
    private void AutoPet(object sender, RoutedEventArgs e) { automaticActions=true;Pet.AllowCursorAttraction=QuietMode.IsChecked!=true; runningAction?.Stop();runningAction=null; }
    private void SelectAction(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (Actions.SelectedItem is BodyAction action) SetManualAction(action); }
}
