using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace DesktopLife.App;
public partial class MainWindow
{
    private bool displaysReady;
    private void InitializeDisplays()
    {
        RefreshDisplayList();
        SystemEvents.DisplaySettingsChanged+=DisplaySettingsChanged;
        SystemParameters.StaticPropertyChanged+=WorkAreaChanged;
        Closed+=(_,_)=>{SystemEvents.DisplaySettingsChanged-=DisplaySettingsChanged;SystemParameters.StaticPropertyChanged-=WorkAreaChanged;};
    }
    private void RefreshDisplayList()
    {
        displaysReady=false;DisplayOptions.ItemsSource=DisplayWorkspace.Enumerate();
        DisplayOptions.SelectedItem=((IReadOnlyList<RoomDisplay>)DisplayOptions.ItemsSource).FirstOrDefault(d=>d.Id==DisplayWorkspace.Active.Id);
        displaysReady=true;
        DisplayStatus.Text=$"Windows 縮放 {DisplayWorkspace.Active.Scale:P0}；角色、家具、玩具共用同一個房間座標。";
    }
    private void DisplaySettingsChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(new Action(RefreshWorkspace));
    private void WorkAreaChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    {if(e.PropertyName==nameof(SystemParameters.WorkArea))Dispatcher.BeginInvoke(new Action(RefreshWorkspace));}
    private void RefreshWorkspace()
    {
        if(!IsLoaded)return;
        var old=DisplayWorkspace.Active;var current=DisplayWorkspace.Select(settings.ActiveDisplay);
        if(old.Bounds!=current.Bounds||old.Scale!=current.Scale||old.Id!=current.Id)
        {Pet.RemapWorkspace(old.Bounds,current.Bounds);SavePetState();UpdatePetVisibility();}
        RefreshDisplayList();
    }
    private void ChangeDisplay(object sender,SelectionChangedEventArgs e)
    {
        if(!displaysReady||DisplayOptions.SelectedItem is not RoomDisplay choice)return;
        var previous=DisplayWorkspace.Bounds;DisplayWorkspace.Select(choice.Id);
        settings=settings with{ActiveDisplay=choice.Id};settingsStore.Save(settings);
        Pet.RemapWorkspace(previous,DisplayWorkspace.Bounds);SavePetState();UpdatePetVisibility();
        DisplayStatus.Text=$"房間已移到 {choice.Label}。拔除螢幕時會移至主要螢幕。";
    }
}
