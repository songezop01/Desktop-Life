using System.Windows;
using System.Windows.Media;
using DesktopLife.Core;

namespace DesktopLife.App;

internal static class FoodArt
{
    private static readonly Dictionary<FoodKind,SpriteAssetFrame> frames=[];
    private static bool loaded;
    internal static string? Failure {get;private set;}
    internal static int LoadedCount {get{Load();return frames.Count;}}
    private static void Load()
    {
        if(loaded)return;loaded=true;
        try
        {
            var reader=new SpriteAssetReader("Food/meals-v1.png",1024);
            var kinds=new[]{FoodKind.BurgerMeal,FoodKind.FriedChickenMeal,FoodKind.HotPot,FoodKind.Ramen,FoodKind.ChickenCutlet,FoodKind.SharedKibble};
            for(var i=0;i<kinds.Length;i++)frames[kinds[i]]=reader.Extract([i%3*512,i/3*512,512,512]);
            frames[FoodKind.CatKibble]=frames[FoodKind.SharedKibble];frames[FoodKind.DogKibble]=frames[FoodKind.SharedKibble];
        }
        catch(Exception ex) when(ex is System.IO.IOException or InvalidOperationException or NotSupportedException or System.IO.FileFormatException){Failure=ex.Message;}
    }
    internal static bool Draw(DrawingContext dc,FoodKind kind,Rect area,double remaining)
    {
        Load();if(!frames.TryGetValue(kind,out var frame)||remaining<=0)return false;
        var scale=Math.Min(area.Width/frame.Width,area.Height/frame.Height);
        var bounds=new Rect(area.X+(area.Width-frame.Width*scale)/2,area.Bottom-frame.Height*scale,frame.Width*scale,frame.Height*scale);
        var kibble=FoodCatalog.IsPetFood(kind);
        if(kibble)dc.PushClip(new RectangleGeometry(new(bounds.X,bounds.Bottom-bounds.Height*Math.Clamp(remaining,0,1),bounds.Width,bounds.Height)));
        dc.DrawImage(frame.Image,bounds);
        if(kibble)dc.Pop();
        return true;
    }
}
