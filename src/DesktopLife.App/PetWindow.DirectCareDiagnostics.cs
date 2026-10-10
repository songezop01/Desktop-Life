using System.Windows;
using System.Windows.Interop;
using DesktopLife.Core;
using DesktopLife.Windows;

namespace DesktopLife.App;

public partial class PetWindow
{
    internal bool HasLegacyCareMenu=>Character.ContextMenu is not null;
    internal bool HoverHandVisible=>hoverHand.Visibility==Visibility.Visible;
    internal Point DirectHeadDiagnosticPoint()
    {
        UpdateLayout();ApplyPose(0);
        var r=HeadRegion;
        for(var y=r.Top+3;y<r.Bottom-3;y+=2)
            for(var x=r.Left+8;x<r.Right-8;x+=2)
            {
                var room=new Point(body.X+x*BodyWidth/116,body.Y+y*BodyHeight/144);
                if(PaintedHeadStrip(room))return room;
            }
        throw new System.Exception("No exposed illustrated head contact strip.");
    }
    private bool PaintedHeadStrip(Point start)
        =>Enumerable.Range(0,9).All(i=>PaintedCarePoint(new(start.X+i,start.Y),true)&&ExposedCarePoint(new(start.X+i,start.Y)));
    internal Point DirectGroomDiagnosticPoint()
    {
        UpdateLayout();ApplyPose(0);
        var r=HeadRegion;
        for(var y=r.Top+5;y<r.Bottom-5;y+=2)
            for(var x=r.Left+8;x<r.Right-8;x+=2)
            {
                var room=new Point(body.X+x*BodyWidth/116,body.Y+y*BodyHeight/144);
                // Keep the stroke inside actual hair/fur with a small margin
                // for the production breathing transform during the native wait.
                if(Enumerable.Range(0,13).All(i=>new[]{-.6,0,.6}.All(d=>
                    PaintedCarePoint(new(room.X+i,room.Y+d),false)&&ExposedCarePoint(new(room.X+i,room.Y+d)))))return room;
            }
        throw new System.Exception("No exposed illustrated grooming contact strip.");
    }
    internal void SampleDirectCareDiagnostic(Point? point,double dt)=>StepDirectCare(point,dt,null);
    internal object HoverContactDiagnostic(Point point)
    {
        var pixel=DisplayWorkspace.ToPixels(point);
        return new{appearance,Point=point,Local=LocalCarePoint(point),HeadRegion,Painted=PaintedCarePoint(point,true),Exposed=ExposedCarePoint(point),
            Blocks=DirectCareBlocks().ToString(),HoverHandVisible,Expected=new WindowInteropHelper(this).Handle.ToInt64(),Top=PointerExposure.WindowAt(pixel.X,pixel.Y).ToInt64(),Body=Position,Grounded=petGravity.Grounded,
            BodyWidth,BodyHeight,Feet=body.Y+BodyHeight,Supports=RoomPlatforms().Where(p=>body.X+HalfWidth>=p.X&&body.X+HalfWidth<=p.X+p.Width).ToArray()};
    }
    internal void TickHeldCombDiagnostic(double dt)=>StepDirectCare(null,dt,null);
    internal void PlaceDirectCareDiagnostic(RoomPoint point)
    {
        body.Place(point.X,point.Y,Bounds());StepPetGravity(.016);
        // Native pixel rounding can leave a fractional DIP above a floor after
        // a preset change. The frozen fixture must let real gravity settle it;
        // contact gates keep rejecting the genuinely airborne samples.
        for(var frame=0;frame<8&&!petGravity.Grounded;frame++)StepPetGravity(.016);
        ApplyPose();ApplyPosition();
    }
    internal object CombContactDiagnostic(RoomWindow comb)
    {
        var tip=comb.CombTip;var pixel=DisplayWorkspace.ToPixels(tip);
        return new{comb.CombHeld,comb.CombDragId,Tip=tip,Local=LocalCarePoint(tip),Painted=PaintedCarePoint(tip,false),
            Exposed=ExposedCarePoint(tip,comb.NativeHandle),Blocks=DirectCareBlocks().ToString(),DirectGroomContacts,
            Expected=new WindowInteropHelper(this).Handle.ToInt64(),Tool=comb.NativeHandle.ToInt64(),Top=PointerExposure.WindowAt(pixel.X,pixel.Y).ToInt64()};
    }
    internal DirectCareContact PendingDirectContactDiagnostic(CareKind kind)=>directCareSession.CreateContact(appearance,kind);
    internal void RenderDirectCareDiagnostic(string path)
    {
        UpdateLayout();var visual=(System.Windows.Media.Visual)Content;
        var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(Width),(int)Math.Ceiling(Height),96,96,System.Windows.Media.PixelFormats.Pbgra32);
        image.Render(visual);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using var stream=System.IO.File.Create(path);encoder.Save(stream);
    }
}
