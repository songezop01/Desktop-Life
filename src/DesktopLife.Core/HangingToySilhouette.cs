namespace DesktopLife.Core;

/// <summary>Conservative occupied bands for a contact pose through facing and breathing interpolation.</summary>
public sealed class HangingToySilhouette
{
    private readonly record struct Band(double Left,double Right,double Top,double Bottom);
    private readonly Band[] bands;
    private HangingToySilhouette(Band[] occupied){bands=occupied;}

    public static HangingToySilhouette FromAlpha(byte[] alpha,int width,int height,double imageScale,
        double viewportWidth=116,double viewportHeight=144)
    {
        ArgumentNullException.ThrowIfNull(alpha);
        if(width<1||height<1||alpha.Length!=(long)width*height||!double.IsFinite(imageScale)||imageScale<=0||
            !double.IsFinite(viewportWidth)||viewportWidth<=0||!double.IsFinite(viewportHeight)||viewportHeight<=0)
            throw new ArgumentOutOfRangeException(nameof(imageScale));
        var occupied=new List<Band>();var left=(viewportWidth-width*imageScale)/2;
        var top=viewportHeight-height*imageScale;var pivot=viewportWidth/2;
        for(var y=0;y<height;y++)
        {
            var first=width;var last=-1;
            for(var x=0;x<width;x++)if(alpha[y*width+x]>=24){first=Math.Min(first,x);last=x;}
            if(last<0)continue;
            var extent=Math.Max(Math.Abs(left+first*imageScale-pivot),Math.Abs(left+(last+1)*imageScale-pivot));
            // Every horizontal flip value -1..1 lies inside this symmetric band.
            // Include the current grounded pose's breathing and 2.5% transition ease.
            var y0=top+y*imageScale;var y1=y0+imageScale;
            occupied.Add(new(pivot-extent,pivot+extent,
                viewportHeight+(y0-viewportHeight)*1.008,viewportHeight+(y1-viewportHeight)*.968));
        }
        return new(occupied.ToArray());
    }
    public bool IsSphereClear(RoomPoint center,double radius,double margin=.5)
    {
        if(!double.IsFinite(center.X)||!double.IsFinite(center.Y)||!double.IsFinite(radius)||radius<=0||
            !double.IsFinite(margin)||margin<0)throw new ArgumentOutOfRangeException(nameof(radius));
        var padded=radius+margin;
        foreach(var band in bands)
        {
            var dx=center.X-Math.Clamp(center.X,band.Left,band.Right);
            var dy=center.Y-Math.Clamp(center.Y,band.Top,band.Bottom);
            if(dx*dx+dy*dy<=padded*padded)return false;
        }
        return true;
    }
}
