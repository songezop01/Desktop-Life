using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    private bool saveClosing,saveCloseComplete;
    private long lastReportedSaveRevision;

    private FrozenOrganismSnapshot CapturePetSave()
    {
        var started=Stopwatch.GetTimestamp();
        try
        {
            FlushPendingSettings();
            Learning.State.Room=Pet.CaptureRoom();Learning.State.Artworks=Pet.Art.Works.ToList();
            return FrozenOrganismSnapshot.Capture(new OrganismSnapshot{Pet=Snapshot(),Learning=Learning.State,Personality=personality,Settings=settings,
                OtherCharacter=otherCharacter?.Capture(),AdditionalCharacter=additionalCharacter?.Capture(),PrimaryPosition=Pet.Position});
        }
        finally{performance.ObserveSnapshotCapture(Stopwatch.GetElapsedTime(started).TotalMilliseconds);}
    }

    private async void QueuePetSave()=>await SavePetStateAsync();

    public async Task<bool> SavePetStateAsync()
    {
        if(saveClosing)return false;
        try
        {
            var pending=saveQueue.Enqueue(CapturePetSave());lastSaveTick=lifeClock.Elapsed;
            SaveStatus.Text="正在背景保存本機狀態……";
            var receipt=await pending;
            if(receipt.Revision<lastReportedSaveRevision||saveClosing)return true;
            lastReportedSaveRevision=receipt.Revision;
            SaveStatus.Text=$"本機生理狀態已保存 {DateTime.Now:HH:mm:ss} · 每 60 秒自動保存";
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {SaveStatus.Text="保存失敗："+ex.Message+"。目前狀態仍保留，請排除問題後重試；退出時會再次確認保存。";return false;}
    }

    private async void PersistBeforeClosing(object? sender,CancelEventArgs e)
    {
        if(e.Cancel||saveCloseComplete)return;
        e.Cancel=true;
        if(saveClosing)return;
        saveClosing=true;
        var wasPaused=aiPaused;
        try
        {
            TickLife();lifeTimer.Stop();learningTimer.Stop();visibilityTimer.Stop();iconTimer.Stop();SetPaused(true);
            if(tray?.ContextMenuStrip is {} menu)menu.Enabled=false;
            IsEnabled=false;foreach(var surface in Pet.Surfaces.Concat(characterWindows).Distinct())surface.IsEnabled=false;
            foreach(var window in characterWindows)window.SetSimulationEnabled(false);
            SaveStatus.Text="正在保存，完成後結束……";
            await saveQueue.Enqueue(CapturePetSave());
            saveCloseComplete=true;Close();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            saveClosing=false;IsEnabled=true;foreach(var surface in Pet.Surfaces.Concat(characterWindows).Distinct())surface.IsEnabled=true;
            if(tray?.ContextMenuStrip is {} menu)menu.Enabled=true;
            foreach(var window in characterWindows)window.SetSimulationEnabled(true);
            SetPaused(wasPaused);lifeTimer.Start();learningTimer.Start();visibilityTimer.Start();if(desktopIcons is not null&&!iconClosing)iconTimer.Start();
            SaveStatus.Text="無法完成保存："+ex.Message+"。視窗及目前狀態仍保留，請排除問題後再結束。";
            Show();WindowState=WindowState.Normal;
        }
    }
}
