namespace DesktopLife.Core;
public static class ToyContactPhysics
{
    public static (double X,double Y) Push(double toyX,BodyBounds bounds,double direction,double force)
    {
        if(toyX-bounds.Left<75)return(Math.Max(280,force),-560);
        if(bounds.Left+bounds.Width-InteractiveToy.Size-toyX<75)return(-Math.Max(280,force),-560);
        return(Math.Sign(direction)*Math.Max(60,force),-Math.Max(160,force*.7));
    }
    public static void Separate(InteractiveToy toy,double petX,double petY,BodyBounds bounds)
    {
        var dx=toy.X+20-petX-58;
        if(toy.Held||Math.Abs(toy.Y+20-petY-126)>30||Math.Abs(dx)>=48)return;
        var impulse=Push(toy.X,bounds,dx>=0?1:-1,Math.Abs(toy.VelocityX)*.65);
        // In a corner, lift over the pet instead of pinning the toy against the wall again.
        var edge=toy.X-bounds.Left<75||bounds.Left+bounds.Width-40-toy.X<75;
        if(!edge)toy.Place(petX+58+Math.Sign(dx==0?1:dx)*48-20,toy.Y,bounds);
        toy.Kick(impulse.X,Math.Min(toy.VelocityY,impulse.Y));
    }
}
