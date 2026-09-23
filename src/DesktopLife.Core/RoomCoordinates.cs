namespace DesktopLife.Core;
public static class RoomCoordinates
{
    public static RoomPoint Rehome(RoomPoint point,BodyBounds from,BodyBounds to,double width,double height)
    {
        double Map(double value,double origin,double size,double nextOrigin,double nextSize,double extent)
        {var fraction=Math.Clamp((value-origin)/Math.Max(1,size-extent),0,1);return nextOrigin+fraction*Math.Max(0,nextSize-extent);}
        return new(Map(point.X,from.Left,from.Width,to.Left,to.Width,width),Map(point.Y,from.Top,from.Height,to.Top,to.Height,height));
    }
    public static BodyBounds FromPixels(double left,double top,double width,double height,double scale)
    {
        if(!double.IsFinite(scale)||scale<=0)throw new ArgumentOutOfRangeException(nameof(scale));
        return new(left/scale,top/scale,width/scale,height/scale);
    }
}
