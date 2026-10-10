using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    private void SmokeFoodArtwork(string root)
    {
        if(FoodArt.LoadedCount!=Enum.GetValues<FoodKind>().Length)throw new Exception("Food illustrations failed to load: "+FoodArt.Failure);
        var visual=new DrawingVisual();var hashes=new HashSet<string>();
        using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.FloralWhite,null,new Rect(0,0,660,220));
            var kinds=new[]{FoodKind.BurgerMeal,FoodKind.FriedChickenMeal,FoodKind.HotPot,FoodKind.Ramen,FoodKind.ChickenCutlet};
            for(var i=0;i<kinds.Length;i++)
            {
                var isolated=new DrawingVisual();using(var foodDc=isolated.RenderOpen())FoodArt.Draw(foodDc,kinds[i],new(0,0,110,95),1);
                var bmp=FoodBitmap(isolated,110,95);var data=new byte[110*95*4];bmp.CopyPixels(data,110*4,0);
                if(data[3]!=0||!Enumerable.Range(0,110*95).Any(p=>data[p*4+3]>100))throw new Exception("Food alpha is not isolated or its dish is missing.");
                hashes.Add(Convert.ToHexString(SHA256.HashData(data)));dc.DrawImage(bmp,new(i*130+10,10,110,95));
            }
            var previous=int.MaxValue;
            for(var i=0;i<3;i++)
            {
                var isolated=new DrawingVisual();using(var foodDc=isolated.RenderOpen())FoodArt.Draw(foodDc,FoodKind.SharedKibble,new(0,0,110,70),new[]{1,.5,.1}[i]);
                var bmp=FoodBitmap(isolated,110,70);var data=new byte[110*70*4];bmp.CopyPixels(data,110*4,0);
                var painted=Enumerable.Range(0,110*70).Count(p=>data[p*4+3]>100);
                if(painted>=previous||painted==0)throw new Exception("Visible kibble did not decrease with true stock.");previous=painted;
                dc.DrawImage(bmp,new(i*130+10,125,110,70));
            }
        }
        if(hashes.Count!=5)throw new Exception("Five recipes share the same displayed meal.");
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(FoodBitmap(visual,660,220)));
        using var stream=File.Create(Path.Combine(root,"food-art-sheet.png"));encoder.Save(stream);
    }
    private static RenderTargetBitmap FoodBitmap(Visual visual,int width,int height)
    {var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bmp.Render(visual);return bmp;}
}
