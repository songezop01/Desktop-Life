using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DesktopLife.App;

// Draw only the furniture lip that intersects this character. It shares the
// character's existing HWND, so 19 furniture masks do not create 19 more windows.
public sealed class CharacterFurnitureForegroundVisual : FrameworkElement
{
    private readonly record struct Entry(RoomWindow Window, double X, double Y, double Scale);
    private Entry[] entries = [];
    private double bodyX, bodyY, bodyScale;
    public int MaskCount => entries.Length;
    public CharacterFurnitureForegroundVisual()
    { Width=CharacterGeometry.CanonicalWidth;Height=CharacterGeometry.CanonicalHeight;IsHitTestVisible=false;ClipToBounds=true; }

    public void SetContext(IEnumerable<RoomWindow> furniture,double x,double y,double scale,Guid? activeTarget)
    {
        if(!double.IsFinite(scale)||scale<=0)return;
        var feet=y+CharacterGeometry.CanonicalHeight*scale;
        var bodyBounds=new Rect(x,y,CharacterGeometry.CanonicalWidth*scale,CharacterGeometry.CanonicalHeight*scale);
        var next=furniture.Where(f=>f.IsVisible&&bodyBounds.IntersectsWith(new Rect(f.Item.X,f.Item.Y,f.Width,f.Height))
            &&(f.Item.Id==activeTarget||f.Platforms.Any(p=>x+bodyBounds.Width/2>=p.X&&x+bodyBounds.Width/2<=p.X+p.Width
                &&Math.Abs(p.HeightAt(x+bodyBounds.Width/2)-feet)<=6)))
            .Select(f=>new Entry(f,f.Item.X,f.Item.Y,f.SceneScale)).ToArray();
        if(bodyX==x&&bodyY==y&&bodyScale==scale&&entries.SequenceEqual(next))return;
        bodyX=x;bodyY=y;bodyScale=scale;entries=next;InvalidateVisual();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        if(bodyScale<=0)return;
        drawing.PushClip(new RectangleGeometry(new Rect(0,0,Width,Height)));
        foreach(var entry in entries)
        {
            var size=RoomWindow.Size(entry.Window.Item.Kind);
            drawing.PushTransform(new TranslateTransform((entry.X-bodyX)/bodyScale,(entry.Y-bodyY)/bodyScale));
            drawing.PushTransform(new ScaleTransform(entry.Scale/bodyScale,entry.Scale/bodyScale));
            FurnitureArt.Draw(drawing,entry.Window.Item.Kind,size.Width,size.Height,true);
            drawing.Pop();drawing.Pop();
        }
        drawing.Pop();
    }
}
