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
    private readonly OrganismSaveQueue saveQueue;
    private readonly Stopwatch lifeClock = new();
    private readonly DispatcherTimer lifeTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TimeSpan lastLifeTick;
    private TimeSpan lastSaveTick;
    private DateTimeOffset? suspendedAt;
    private bool verificationFrozen;
    public MainWindow(string dataRoot, AppSettings settings,Action<FrozenOrganismSnapshot>? diagnosticWriter=null)
    {
        InitializeComponent();
        var buildVersion=System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(App).Assembly)?.InformationalVersion.Split('+')[0];
        VersionBanner.Text=$"DESKTOP LIFE  /  陪伴小屋 {buildVersion??typeof(App).Assembly.GetName().Version?.ToString(3)}";
        InitializePerformance();
        InitializeIllustrations();
        organismStore = new OrganismStore(dataRoot);
        saveQueue=diagnosticWriter is null?new(organismStore):new(diagnosticWriter);
        var checkpoint=organismStore.LoadOrMigrate(settings,DateTimeOffset.UtcNow);
        var saved = checkpoint.Pet;
        personality=checkpoint.Personality;
        Life = new(saved.State, saved.TotalRuntimeSeconds);
        Life.ApplyCompanionOffline(DateTimeOffset.UtcNow - saved.LastSaveTime);
        // Persist the applied offline interval once, so rapid restarts cannot reapply it.
        InitializeLearning(dataRoot,settings with{PetAppearance=checkpoint.Settings.PetAppearance},checkpoint.Learning);
        InitializePresence(checkpoint);
        InitializeFood(checkpoint);
        if(!SavePetState())throw new IOException("初始狀態無法保存。");
        ShowHomeostasis();
        SettingsText.Text = "本機陪伴與偏好記憶，不連接雲端。真實桌面圖示互動需另外啟用；移動前備份，可一鍵恢復。";
        Actions.ItemsSource = Enum.GetValues<BodyAction>();
        lifeTimer.Tick += (_, _) => TickLife();
        Loaded += (_, _) =>
        {
            Pet.Show(); lifeClock.Start(); if(!verificationFrozen)lifeTimer.Start();
            InitializeTray();
            if(verificationFrozen)visibilityTimer.Stop();
            SystemEvents.PowerModeChanged += PowerChanged;
            sensorTask ??= Task.Run(() => MonitorAsync(sensorCancellation.Token));
        };
        Closing += (_,e)=>{if(!explicitExit&&this.settings.CloseBehavior==CloseBehavior.Tray){e.Cancel=true;Hide();}};
        Closing += PersistBeforeClosing;
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
        var started=Stopwatch.GetTimestamp();
        try
        {
            // Startup, session ending and isolated diagnostics need a durable barrier.
            // Normal controls and periodic saves use QueuePetSave and never wait for disk on the dispatcher.
            saveQueue.Enqueue(CapturePetSave()).GetAwaiter().GetResult();
            lastSaveTick = lifeClock.Elapsed;
            SaveStatus.Text = $"本機生理狀態已保存 {DateTime.Now:HH:mm:ss} · 每 60 秒自動保存";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SaveStatus.Text = $"保存失敗：{ex.Message}。請排除寫入問題後再試；保存成功前會保留視窗。";
            return false;
        }
        finally{performance.ObserveSave(Stopwatch.GetElapsedTime(started).TotalMilliseconds);}
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
        TickOther(Math.Max(0,(elapsed-lastLifeTick).TotalSeconds));
        TickFood();
        lastLifeTick = elapsed;
        ShowHomeostasis();
        if (!saveClosing&&elapsed - lastSaveTick >= TimeSpan.FromSeconds(60)) QueuePetSave();
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
                Life.ApplyCompanionOffline(DateTimeOffset.UtcNow - began); foreach(var character in secondaryCharacters)character.Life.ApplyCompanionOffline(DateTimeOffset.UtcNow-began);
                suspendedAt = null; lastLifeTick = lifeClock.Elapsed;lastDispatchProbe=Stopwatch.GetTimestamp();
                LatestEnvironment = null; // do not reuse a pre-suspend activity sample
                QueuePetSave(); ShowHomeostasis();
            }
        });
    }
    private sealed class StateRow(string name):System.ComponentModel.INotifyPropertyChanged
    {
        private static readonly System.ComponentModel.PropertyChangedEventArgs valueChanged=new(nameof(Value));
        public string Name {get;}=name;
        public double Value {get;private set;}
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void Set(double value)
        {if(Value==value)return;Value=value;PropertyChanged?.Invoke(this,valueChanged);}
    }
    private readonly StateRow[] homeostasisRows=[new("能量"),new("飢餓"),new("心情"),new("無聊"),new("好奇"),new("孤單"),new("興奮"),new("疲勞")];
    private void ShowHomeostasis()
    {
        if(!IsVisible||!HomeostasisRows.IsVisible){performance.SkippedDiagnosticRefreshes++;return;}
        performance.HomeostasisDisplayRefreshes++;
        var s = presenceReady?SelectedState:Life.State;
        if(HomeostasisRows.ItemsSource is null)HomeostasisRows.ItemsSource=homeostasisRows;
        homeostasisRows[0].Set(s.Energy);homeostasisRows[1].Set(s.Hunger);homeostasisRows[2].Set(s.Mood);homeostasisRows[3].Set(s.Boredom);
        homeostasisRows[4].Set(s.Curiosity);homeostasisRows[5].Set(s.Loneliness);homeostasisRows[6].Set(s.Excitement);homeostasisRows[7].Set(s.Fatigue);
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
        if(!IsVisible||!SensorRows.IsVisible){performance.SkippedDiagnosticRefreshes++;return;}
        performance.SensorDisplayRefreshes++;
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
    private void ResetPet(object sender, RoutedEventArgs e) => CharacterWindow(careTarget).ResetPosition();
    private void ClearArt(object sender,RoutedEventArgs e){Pet.Art.ClearWorks();QueuePetSave();}
    private void KeepArt(object sender,RoutedEventArgs e){Pet.Art.KeepWorks();QueuePetSave();}
    private void ToggleArt(object sender,RoutedEventArgs e)=>Pet.Art.ToggleWorks();
    private void AutoPet(object sender, RoutedEventArgs e) { automaticActions=true;Pet.AllowCursorAttraction=QuietMode.IsChecked!=true; runningAction?.Stop();runningAction=null; }
    private void SelectAction(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (Actions.SelectedItem is BodyAction action) SetManualAction(action); }
}
