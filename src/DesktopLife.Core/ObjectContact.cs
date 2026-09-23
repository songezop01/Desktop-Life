namespace DesktopLife.Core;

public readonly record struct ObjectApproach(double X,double Y,double PushX,double PushY);
public static class ObjectContact
{
    public static ObjectApproach Approach(double objectX,double objectY,double size,BodyBounds bounds)
    {
        var x=Math.Clamp(objectX+size/2-DesktopBody.Width/2,bounds.Left,bounds.Left+Math.Max(0,bounds.Width-DesktopBody.Width));
        var y=Math.Clamp(objectY+size/2-95,bounds.Top,bounds.Top+Math.Max(0,bounds.Height-DesktopBody.Height));
        var dx=objectX<bounds.Left+90?1:objectX+size>bounds.Left+bounds.Width-90?-1:1;
        var dy=objectY<bounds.Top+100?1:objectY+size>bounds.Top+bounds.Height-100?-1:0;
        var length=Math.Sqrt(dx*dx+dy*dy);
        return new(x,y,dx/length,dy/length);
    }
    public static bool Reached(double x,double y,ObjectApproach approach)=>Math.Abs(x-approach.X)<12&&Math.Abs(y-approach.Y)<12;
}
