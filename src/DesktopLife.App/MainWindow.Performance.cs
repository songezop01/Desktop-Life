using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    private readonly RuntimePerformance performance=new();
    private readonly DispatcherTimer dispatchProbe=new(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(100)};
    private readonly DispatcherTimer settingsSaveTimer=new(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(350)};
    private long lastDispatchProbe;
    private bool settingsSavePending;

    private void InitializePerformance()
    {
        HomeostasisRows.IsVisibleChanged+=(_,_)=>{if(HomeostasisRows.IsVisible)ShowHomeostasis();};
        dispatchProbe.Tick+=(_,_)=>
        {
            var stamp=Stopwatch.GetTimestamp();
            if(suspendedAt is null&&lastDispatchProbe!=0)
                performance.ObserveDispatchDelay(Math.Max(0,Stopwatch.GetElapsedTime(lastDispatchProbe,stamp).TotalMilliseconds-dispatchProbe.Interval.TotalMilliseconds));
            lastDispatchProbe=stamp;
        };
        settingsSaveTimer.Tick+=(_,_)=>
        {
            settingsSaveTimer.Stop();
            try{FlushPendingSettings();}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
            {SaveStatus.Text="設定保存失敗："+ex.Message+"。目前設定仍保留，下次保存時會重試。";}
        };
        Loaded+=(_,_)=>{lastDispatchProbe=Stopwatch.GetTimestamp();dispatchProbe.Start();};
        Closed+=(_,_)=>{dispatchProbe.Stop();settingsSaveTimer.Stop();};
    }

    // A slider changes audio immediately, but persists only after the gesture settles.
    private void QueueSettingsSave()
    {
        settingsSavePending=true;settingsSaveTimer.Stop();settingsSaveTimer.Start();
    }

    private void FlushPendingSettings()
    {
        if(!settingsSavePending||settingsStore is null)return;
        settingsSaveTimer.Stop();settingsStore.Save(settings);settingsSavePending=false;performance.DebouncedSettingsWrites++;
    }

    public object CapturePerformance()=>performance.Capture(saveQueue.CaptureMetrics());
    public object BeginSteadyMeasurement()
    {
        var persistence=saveQueue.CaptureMetrics();var setup=performance.Capture(persistence);
        performance.ResetObservation(persistence);lastDispatchProbe=Stopwatch.GetTimestamp();return setup;
    }
}
