namespace DesktopLife.Core;
public static class ToyContactPhysics
{
    /// <summary>Finds a point on the real toy circumference inside the scaled paw/contact area.</summary>
    public static RoomPoint? ReachableContactPoint(double toyX,double toyY,double petX,double petY,double petHeight,double minimumLocalY)
    {
        if(!double.IsFinite(toyX)||!double.IsFinite(toyY)||!double.IsFinite(petX)||!double.IsFinite(petY)||!double.IsFinite(petHeight)||!double.IsFinite(minimumLocalY)||petHeight<=0||minimumLocalY<0||minimumLocalY>140)
            throw new ArgumentOutOfRangeException(nameof(petHeight));
        var scale=petHeight/DesktopBody.Height;var radius=InteractiveToy.Size/2;
        var cx=toyX+radius;var cy=toyY+radius;
        var left=petX+6*scale;var right=petX+110*scale;
        var top=petY+minimumLocalY*scale;var bottom=petY+140*scale;
        var readyX=petX+(cx>=petX+58*scale?82:34)*scale;var readyY=petY+112*scale;
        var found=false;var bestX=0d;var bestY=0d;var bestDistance=double.PositiveInfinity;
        // The nearest circumference point to the resting paw is optimal if it is reachable.
        // Otherwise an optimum on the clipped arc lies at a circle/rectangle edge intersection.
        var dx=readyX-cx;var dy=readyY-cy;var length=Math.Sqrt(dx*dx+dy*dy);
        if(length>0)Consider(cx+dx/length*radius,cy+dy/length*radius);
        else Consider(cx,cy+radius);
        Vertical(left);Vertical(right);Horizontal(top);Horizontal(bottom);
        return found?new(bestX,bestY):null;

        void Consider(double x,double y)
        {
            const double epsilon=.0000001;
            if(x<left-epsilon||x>right+epsilon||y<top-epsilon||y>bottom+epsilon)return;
            var distance=(x-readyX)*(x-readyX)+(y-readyY)*(y-readyY);
            if(distance<bestDistance){bestDistance=distance;bestX=x;bestY=y;found=true;}
        }
        void Vertical(double x)
        {
            var square=radius*radius-(x-cx)*(x-cx);if(square<0)return;
            var offset=Math.Sqrt(square);Consider(x,cy-offset);Consider(x,cy+offset);
        }
        void Horizontal(double y)
        {
            var square=radius*radius-(y-cy)*(y-cy);if(square<0)return;
            var offset=Math.Sqrt(square);Consider(cx-offset,y);Consider(cx+offset,y);
        }
    }
    public static ObjectApproach Approach(double toyX,double toyY,double petX,BodyBounds bounds,double petWidth=116,double petHeight=144)
    {
        var center=toyX+InteractiveToy.Size/2;var reach=50*petHeight/DesktopBody.Height;
        var left=center-reach-petWidth/2;var right=center+reach-petWidth/2;
        var x=petX+petWidth/2<center?left:right;
        if(x<bounds.Left)x=right;if(x>bounds.Left+bounds.Width-petWidth)x=left;
        x=Math.Clamp(x,bounds.Left,bounds.Left+Math.Max(0,bounds.Width-petWidth));
        return new(x,Math.Clamp(toyY+InteractiveToy.Size-petHeight,bounds.Top,bounds.Top+Math.Max(0,bounds.Height-petHeight)),Math.Sign(center-x-petWidth/2),-.35);
    }
    public static (double X,double Y) Push(double toyX,BodyBounds bounds,double direction,double force)
    {
        if(toyX-bounds.Left<75)return(Math.Max(280,force),-560);
        if(bounds.Left+bounds.Width-InteractiveToy.Size-toyX<75)return(-Math.Max(280,force),-560);
        return(Math.Sign(direction)*Math.Max(60,force),-Math.Max(160,force*.7));
    }
    public static void Separate(InteractiveToy toy,double petX,double petY,BodyBounds bounds,double petWidth=116,double petHeight=144)
    {
        var bodyScale=petHeight/DesktopBody.Height;var separation=48*bodyScale;
        var dx=toy.X+20-petX-petWidth/2;
        if(toy.Held||Math.Abs(toy.Y+20-petY-petHeight*.875)>Math.Max(20,30*bodyScale)||Math.Abs(dx)>=separation)return;
        var impulse=Push(toy.X,bounds,dx>=0?1:-1,Math.Abs(toy.VelocityX)*.65);
        // In a corner, lift over the pet instead of pinning the toy against the wall again.
        var edge=toy.X-bounds.Left<75||bounds.Left+bounds.Width-40-toy.X<75;
        if(!edge)toy.Place(petX+petWidth/2+Math.Sign(dx==0?1:dx)*separation-20,toy.Y,bounds);
        toy.Kick(impulse.X,Math.Min(toy.VelocityY,impulse.Y));
    }
}
