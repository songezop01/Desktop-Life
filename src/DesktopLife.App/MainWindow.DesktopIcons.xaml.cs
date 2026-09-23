using System.IO;
using System.Windows;
using System.Windows.Threading;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    private DesktopIconsWorker? desktopIcons;
    private readonly DispatcherTimer iconTimer=new(){Interval=TimeSpan.FromSeconds(3)};
    private bool iconReady,iconBusy,restoringIcons,iconClosing,iconCloseComplete;
    private DesktopIconLayout? iconLayout;
    private bool shortcutOnce;
    private void InitializeDesktopIcons(string dataRoot)
    {
        if(dataRoot.StartsWith(Path.Combine(Path.GetTempPath(),"DesktopLifeSmoke"),StringComparison.OrdinalIgnoreCase))
        {IconStatus.Text="測試模式不會移動真實圖示。";return;}
        desktopIcons=new(Path.Combine(dataRoot,"desktop-icons-backup.json"));
        EnableDesktopIcons.IsChecked=settings.DesktopIconsEnabled;iconReady=true;
        Pet.ShortcutPushRequested+=MoveShortcut;
        iconTimer.Tick+=async (_,_)=>await ReadShortcuts();
        Loaded+=async (_,_)=>
        {
            try{var result=await desktopIcons.Run(s=>(Count:s.Restore(pendingOnly:true),Saved:s.HasBackup));IconStatus.Text=result.Count>0?$"已恢復上次留下的 {result.Count} 個圖示位置；長期備份仍保留。":result.Saved?"已載入長期圖示備份，可隨時恢復，或更新為目前排列。":"尚未建立備份，可按「更新備份為目前排列」。";}
            catch(Exception ex){IconStatus.Text="原始排列備份仍保留："+ex.Message;EnableDesktopIcons.IsChecked=false;}
            if(!iconClosing){iconTimer.Start();await ReadShortcuts();}
        };
        Closing+=async (_,e)=>
        {
            if(iconCloseComplete||e.Cancel)return;
            e.Cancel=true;iconClosing=true;iconTimer.Stop();Pet.Shortcuts=[];
            try{await desktopIcons.Run(s=>s.Restore(pendingOnly:true)).WaitAsync(TimeSpan.FromSeconds(8));}
            catch(Exception ex){new FileAppLog(Path.Combine(dataRoot,"logs","app.log")).Write("Icon restore pending; backup retained: "+ex.GetType().Name);}
            iconCloseComplete=true;Close();
        };
        Closed+=(_,_)=>{iconTimer.Stop();desktopIcons.Dispose();};
    }
    private async void DesktopIconsChanged(object sender,RoutedEventArgs e)
    {
        if(!iconReady||desktopIcons is null)return;
        settings=settings with{DesktopIconsEnabled=EnableDesktopIcons.IsChecked==true};settingsStore.Save(settings);
        if(settings.DesktopIconsEnabled)await ReadShortcuts();else await RestoreIcons();
    }
    private async Task ReadShortcuts()
    {
        if(desktopIcons is null||iconBusy||iconClosing||!settings.DesktopIconsEnabled)return;
        iconBusy=true;
        try
        {
            var layout=await desktopIcons.Run(s=>s.Read());
            if(!settings.DesktopIconsEnabled||iconClosing)return;
            iconLayout=layout;
            var work=DisplayWorkspace.Bounds;
            Pet.Shortcuts=layout.Icons.Where(i=>i.Shortcut).Select(i=>
            {
                var local=DisplayWorkspace.FromPixels(new Point(layout.OriginX+i.X,layout.OriginY+i.Y));
                return new ShortcutTarget(i.Id,local.X,local.Y);
            }).Where(i=>i.X>=work.Left&&i.X<=work.Left+work.Width&&i.Y>=work.Top&&i.Y<=work.Top+work.Height).ToArray();
            IconStatus.Text=$"可互動快捷圖示：{Pet.Shortcuts.Count} 個。移動前自動備份；恢復會停止圖示互動。";
        }
        catch(Exception ex){Pet.Shortcuts=[];IconStatus.Text="圖示互動暫停："+ex.Message;}
        finally{iconBusy=false;}
    }
    private async void MoveShortcut(string id,double x,double y)
    {
        if(desktopIcons is null||iconLayout is null||iconBusy||iconClosing||!settings.DesktopIconsEnabled)return;
        iconBusy=true;var feedback="";
        try
        {
            var physical=Pet.PointToScreen(new Point(x-Pet.Left,y-Pet.Top));
            var px=(int)Math.Round(physical.X-iconLayout.OriginX);var py=(int)Math.Round(physical.Y-iconLayout.OriginY);
            await desktopIcons.Run(s=>{s.MoveShortcut(id,px,py);return true;});
            feedback="已移動快捷圖示；原始排列已備份，可隨時恢復。";
        }
        catch(Exception ex){feedback="未完成圖示移動："+ex.Message;}
        finally{iconBusy=false;if(shortcutOnce){shortcutOnce=false;SetManualAction(BodyAction.Idle);}}
        await ReadShortcuts();IconStatus.Text=feedback;
    }
    private async Task RestoreIcons()
    {
        if(desktopIcons is null||restoringIcons)return;
        restoringIcons=true;Pet.Shortcuts=[];
        settings=settings with{DesktopIconsEnabled=false};settingsStore.Save(settings);
        iconReady=false;EnableDesktopIcons.IsChecked=false;iconReady=true;
        try{var count=await desktopIcons.Run(s=>s.Restore());IconStatus.Text=count==0?"目前沒有待恢復的圖示排列。":$"已恢復 {count} 個圖示的位置及原本的自動排列／格線設定。";}
        catch(Exception ex){IconStatus.Text="恢復未完成，原始備份已保留："+ex.Message;}
        finally{restoringIcons=false;}
    }
    private async void UpdateDesktopIconBackup(object sender,RoutedEventArgs e)
    {
        if(desktopIcons is null||restoringIcons||iconClosing)return;
        restoringIcons=true;Pet.Shortcuts=[];
        settings=settings with{DesktopIconsEnabled=false};settingsStore.Save(settings);
        iconReady=false;EnableDesktopIcons.IsChecked=false;iconReady=true;
        try{var count=await desktopIcons.Run(s=>s.UpdateBackup());IconStatus.Text=$"已將目前 {count} 個圖示排列設為長期備份；之前版本另行保留。";}
        catch(Exception ex){IconStatus.Text="更新備份失敗："+ex.Message;}
        finally{restoringIcons=false;}
    }
    private async void RestoreDesktopIcons(object sender,RoutedEventArgs e)=>await RestoreIcons();
    private async void PlayWithShortcut(object sender,RoutedEventArgs e)
    {
        if(desktopIcons is null)return;
        iconReady=false;EnableDesktopIcons.IsChecked=true;iconReady=true;
        settings=settings with{DesktopIconsEnabled=true};settingsStore.Save(settings);await ReadShortcuts();
        if(Pet.Shortcuts.Count>0){shortcutOnce=true;SetManualAction(BodyAction.PushShortcut);}
    }
}
