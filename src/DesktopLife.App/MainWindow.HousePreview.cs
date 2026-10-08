using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    public void PrepareHousePreview(string root)
    {
        PresenceOptions.SelectedIndex=(int)PresenceMode.All;SetPaused(true);
        foreach(var window in characterWindows)window.SetSimulationEnabled(false);
        foreach(var kind in Enum.GetValues<FurnitureKind>())Pet.AddRoomItem(new(Guid.NewGuid(),kind,DisplayWorkspace.Bounds.Left,DisplayWorkspace.Bounds.Top));
        for(var count=1;count<=3;count++)
        {
            Pet.ConfigureHouse(count,true);UpdatePetVisibility();
            var h=Pet.House!;
            for(var i=0;i<characterWindows.Length;i++)
            {
                var character=characterWindows[i];var floor=h.Floors[i%count];
                var x=h.SafeFootX(floor.Index,floor.Left+floor.Width*(i+1)/4,character.Width/2);
                character.RestorePosition(new(x-character.Width/2,floor.Y-character.Height));character.SetAction(BodyAction.Idle);character.UpdateLayout();
            }
            RenderHousePreview(Path.Combine(root,$"house-{count}-floors.png"));
        }
    }
    public void RenderHousePreview(string path)
    {
        var bounds=DisplayWorkspace.Bounds;var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(247,242,235)),null,new Rect(0,0,bounds.Width,bounds.Height));
            void Draw(Window window,double x,double y)
            {
                window.UpdateLayout();
                var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(window.Width)),Math.Max(1,(int)Math.Ceiling(window.Height)),96,96,PixelFormats.Pbgra32);
                if(window.Content is Visual content)bitmap.Render(content);
                dc.DrawImage(bitmap,new Rect(x-bounds.Left,y-bounds.Top,window.Width,window.Height));
            }
            foreach(var structure in Pet.HouseSurfaces)Draw(structure,bounds.Left,bounds.Top);
            foreach(var furniture in Pet.Furniture)Draw(furniture,furniture.Item.X,furniture.Item.Y);
            foreach(var toy in Pet.AllToys)Draw(toy,toy.Model.X-ToyWindow.VisualPadding,toy.Model.Y-ToyWindow.VisualPadding);
            foreach(var character in characterWindows)Draw(character,character.Position.X,character.Position.Y);
        }
        var output=new RenderTargetBitmap((int)Math.Ceiling(bounds.Width),(int)Math.Ceiling(bounds.Height),96,96,PixelFormats.Pbgra32);output.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(output));using var file=File.Create(path);encoder.Save(file);
    }
}
