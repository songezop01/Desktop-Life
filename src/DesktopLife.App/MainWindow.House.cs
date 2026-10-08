using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    private bool houseMenuReady;
    private int? requestedHouseFloors;
    private readonly DispatcherTimer houseChangeTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private void InitializeHouseMenu()
    {
        HouseFloors.ItemsSource=Enumerable.Range(1,HouseLayout.MaximumFloorCount).Select(n=>n switch {1=>"1 層樓 · 平房",2=>"2 層樓 · 上下小屋",3=>"3 層樓 · 閣樓小屋",_=>$"{n} 層樓 · 小屋"}).ToArray();
        HouseFloors.SelectedIndex=(Pet.House?.FloorCount??1)-1;houseMenuReady=true;
        houseChangeTimer.Tick+=(_,_)=>ApplyPendingHouse();
        Closed+=(_,_)=>houseChangeTimer.Stop();UpdateHouseStatus();
    }
    private void ChangeHouseFloors(object sender,SelectionChangedEventArgs e)
    {
        if(!houseMenuReady||HouseFloors.SelectedIndex<0)return;
        requestedHouseFloors=HouseFloors.SelectedIndex+1;
        HouseStatus.Text="正在等角色落地，再切換小屋樓層…";houseChangeTimer.Start();ApplyPendingHouse();
    }
    private void ApplyPendingHouse()
    {
        if(requestedHouseFloors is not {} count)return;
        if(characterWindows.Any(c=>c.IsVisible&&!c.CanRebuildHouse))return;
        houseChangeTimer.Stop();requestedHouseFloors=null;
        Pet.ConfigureHouse(count,true);QueuePetSave();UpdatePetVisibility();UpdateHouseStatus();
    }
    private void RefreshHouseWorkspace()
    {
        if(Pet.House is not {} h)return;
        // A vanished display invalidates every native support: commit one replacement geometry for the household.
        Pet.ConfigureHouse(h.FloorCount,true);UpdateHouseStatus();
    }
    private void UpdateHouseStatus()
    {
        if(Pet.House is not {} h)return;
        HouseStatus.Text=$"目前 {h.FloorCount} 層樓 · 共同縮放 {h.SceneScale:P0}。樓板、樓梯採固定配置；家具可拖曳佈置。";
    }
    private void ArrangeFurniture(object sender,RoutedEventArgs e)
    {Pet.ArrangeHouseFurniture();QueuePetSave();UpdatePetVisibility();ShowRoomStatus();}
}
