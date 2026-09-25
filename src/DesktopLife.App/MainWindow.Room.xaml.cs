using System.Windows;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    private void ForgetFurnitureHabits(object sender,RoutedEventArgs e)
    {Pet.ForgetHabits();SavePetState();RoomStatus.Text="已忘記家具習慣；名字、關係、回憶和房間佈置皆保留。";}
    private void InitializeRoom()
    {
        FurnitureOptions.ItemsSource=Enum.GetValues<FurnitureKind>();FurnitureOptions.SelectedIndex=0;
        Pet.RoomChanged+=()=>{SavePetState();UpdatePetVisibility();ShowRoomStatus();};ShowRoomStatus();
    }
    private void AddFurniture(object sender,RoutedEventArgs e)
    {
        if(FurnitureOptions.SelectedItem is not FurnitureKind kind)return;
        if(Pet.Furniture.Count+Pet.ExtraToys.Count>=24){RoomStatus.Text="房間最多放置 24 件，請先收起一件。";return;}
        var bounds=DisplayWorkspace.Bounds;var size=RoomWindow.Size(kind);var offset=(Pet.Furniture.Count+Pet.ExtraToys.Count)%6*110;
        Pet.AddRoomItem(new(Guid.NewGuid(),kind,bounds.Left+60+offset,bounds.Top+bounds.Height-(kind is FurnitureKind.Yarn or FurnitureKind.BellBall or FurnitureKind.ToyMouse?160:size.Height)));
        EditRoom.IsChecked=true;Pet.SetRoomEditing(true);SavePetState();UpdatePetVisibility();ShowRoomStatus();
    }
    private void EditRoomChanged(object sender,RoutedEventArgs e)
    {if(Learning is null)return;Pet.SetRoomEditing(EditRoom.IsChecked==true);ShowRoomStatus();}
    public void SmokeRoom(string path)
    {
        foreach(var kind in Enum.GetValues<FurnitureKind>()){FurnitureOptions.SelectedItem=kind;AddFurniture(this,new RoutedEventArgs());}
        EditRoom.IsChecked=false;
        if(!SavePetState()||organismStore.Load().Learning.Room.Items.Count!=Enum.GetValues<FurnitureKind>().Length)throw new Exception("Room layout did not persist.");
        CloseOptions.SelectedItem=CloseBehavior.Tray;Close();
        if(IsVisible||!Pet.IsVisible)throw new Exception("Close-to-tray did not preserve pet.");
        Show();
        Pet.Ball.SmokeDirectionalHit();
        Pet.SmokeFurnitureInteractions(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!,"cat-platform-check.png"));
        Pet.SmokeHome(System.IO.Path.GetDirectoryName(path)!);
        var visual=new System.Windows.Media.DrawingVisual();
        using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(System.Windows.Media.Brushes.WhiteSmoke,null,new Rect(0,0,1100,260));double x=0;
            foreach(var furniture in Pet.Furniture)
            {furniture.UpdateLayout();dc.DrawRectangle(new System.Windows.Media.VisualBrush((System.Windows.Media.Visual)furniture.Content),null,new Rect(x,250-furniture.Height,furniture.Width,furniture.Height));x+=furniture.Width+12;}
        }
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1100,260,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=System.IO.File.Create(path);encoder.Save(file);
    }
    private void ShowRoomStatus()=>RoomStatus.Text=$"已放置 {Pet.Furniture.Count+Pet.ExtraToys.Count} / 24 件。"+(Pet.EditingRoom?"拖曳家具調整位置，右鍵收起；完成後取消佈置模式。":"家具不攔截桌面點擊；角色和玩具可落在平台上。");
}
