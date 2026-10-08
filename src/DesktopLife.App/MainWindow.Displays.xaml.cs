using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
namespace DesktopLife.App;
public partial class MainWindow
{
    private bool displaysReady;
    private bool displaysClosed;
    private readonly DispatcherTimer displayRefreshTimer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    public int WorkspaceRemaps {get;private set;}
    private void InitializeDisplays()
    {
        RefreshDisplayList();
        displayRefreshTimer.Tick+=(_,_)=>{displayRefreshTimer.Stop();RefreshWorkspace();};
        SystemEvents.DisplaySettingsChanged+=DisplaySettingsChanged;
        SystemParameters.StaticPropertyChanged+=WorkAreaChanged;
        DisplayWorkspace.WorkspaceDpiChanged+=ScheduleWorkspaceRefresh;
        Loaded+=(_,_)=>RefreshWorkspace();
        Closed+=(_,_)=>{
            displaysClosed=true;displayRefreshTimer.Stop();
            SystemEvents.DisplaySettingsChanged-=DisplaySettingsChanged;
            SystemParameters.StaticPropertyChanged-=WorkAreaChanged;
            DisplayWorkspace.WorkspaceDpiChanged-=ScheduleWorkspaceRefresh;
        };
        // Upgrade legacy DISPLAY-number settings only when that display is currently connected.
        // During disconnection the user's preferred hardware identity remains saved for reconnection.
        var selected=DisplayWorkspace.Active;
        if(string.Equals(settings.ActiveDisplay,selected.DeviceName,StringComparison.OrdinalIgnoreCase)&&settings.ActiveDisplay!=selected.Id)
        {settings=settings with{ActiveDisplay=selected.Id};settingsStore.Save(settings);}
    }
    private void RefreshDisplayList(IReadOnlyList<RoomDisplay>? inventory=null)
    {
        displaysReady=false;var displays=inventory??DisplayWorkspace.Enumerate();
        DisplayOptions.ItemsSource=displays;
        DisplayOptions.SelectedItem=displays.FirstOrDefault(d=>d.Id==DisplayWorkspace.Active.Id);
        displaysReady=true;
        var current=DisplayWorkspace.Active;
        var fallback=settings.ActiveDisplay is not null&&!displays.Any(d=>d.Id==settings.ActiveDisplay||d.DeviceName==settings.ActiveDisplay);
        DisplayStatus.Text=$"{current.Label}；可用空間 {current.Bounds.Width:0} × {current.Bounds.Height:0}。"
            +(fallback?"偏好的螢幕暫時離線，已移到主要螢幕，重新連線後自動返回。":"角色、家具與樓層使用相同縮放。");
    }
    private void DisplaySettingsChanged(object? sender,EventArgs e)=>ScheduleWorkspaceRefresh();
    private void WorkAreaChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    {if(e.PropertyName==nameof(SystemParameters.WorkArea))ScheduleWorkspaceRefresh();}
    private void ScheduleWorkspaceRefresh()
    {
        if(displaysClosed||Dispatcher.HasShutdownStarted)return;
        Dispatcher.BeginInvoke(new Action(()=>{
            if(displaysClosed)return;
            // A spacedesk reconnect can send several display/workarea changes while the driver settles.
            displayRefreshTimer.Stop();displayRefreshTimer.Start();
        }));
    }
    private void RefreshWorkspace()
    {
        if(!IsLoaded||displaysClosed)return;
        var old=DisplayWorkspace.Active;var inventory=DisplayWorkspace.Enumerate();
        var current=DisplayWorkspace.Select(settings.ActiveDisplay,inventory);
        if(old.Bounds!=current.Bounds||old.Scale!=current.Scale||old.Id!=current.Id)
            RemapDisplayWorkspace(old,current);
        // Select always refreshes HMONITOR, even if the driver reused the same coordinates.
        RefreshDisplayList(inventory);
    }
    private void RemapDisplayWorkspace(RoomDisplay previous,RoomDisplay current)
    {
        Pet.RemapWorkspace(previous.Bounds,current.Bounds);
        foreach(var character in secondaryCharacters)character.Window.RemapWorkspace(previous.Bounds,current.Bounds);
        RefreshHouseWorkspace();WorkspaceRemaps++;
        QueuePetSave();UpdatePetVisibility();
    }
    private void ChangeDisplay(object sender,SelectionChangedEventArgs e)
    {
        if(!displaysReady||DisplayOptions.SelectedItem is not RoomDisplay choice)return;
        var previous=DisplayWorkspace.Active;var current=DisplayWorkspace.Select(choice.Id);
        settings=settings with{ActiveDisplay=current.Id};settingsStore.Save(settings);
        if(previous.Bounds!=current.Bounds||previous.Scale!=current.Scale||previous.Id!=current.Id)
            RemapDisplayWorkspace(previous,current);
        RefreshDisplayList();
    }
}
