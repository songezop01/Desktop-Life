namespace DesktopLife.Core;

public sealed class DragGesture
{
    public bool Active {get;private set;}
    public bool Dragged {get;private set;}
    private double pointerX,pointerY,originX,originY;
    public void Begin(double x,double y,double left,double top)
    {Active=true;Dragged=false;pointerX=x;pointerY=y;originX=left;originY=top;}
    public (double X,double Y)? Move(double x,double y)
    {
        if(!Active)return null;
        var dx=x-pointerX;var dy=y-pointerY;
        Dragged|=dx*dx+dy*dy>=25;
        return Dragged?(originX+dx,originY+dy):null;
    }
    public bool End(){var moved=Active&&Dragged;Active=false;return moved;}
}

public sealed class InteractiveToy(double x,double y)
{
    public const double Size=40;
    private readonly GravityBody physics=new(){X=x,Y=y};
    public double X=>physics.X;
    public double Y=>physics.Y;
    public double VelocityX=>physics.VX;
    public double VelocityY=>physics.VY;
    public bool Held {get;set;}
    public void Place(double x,double y,BodyBounds bounds)
    {physics.X=Math.Clamp(x,bounds.Left,bounds.Left+Math.Max(0,bounds.Width-Size));physics.Y=Math.Clamp(y,bounds.Top,bounds.Top+Math.Max(0,bounds.Height-Size));physics.VX=physics.VY=0;}
    public void Kick(double x,double y)
    {if(!Held){physics.VX=Math.Clamp(x,-650,650);physics.VY=Math.Clamp(y,-650,650);}}
    public void Hit(double x,double y){var impulse=GravityBody.ClickImpulse(x,y,Size);Kick(impulse.X,impulse.Y);}
    public void Step(double elapsed,BodyBounds bounds,IReadOnlyList<RoomPlatform>? platforms=null)
    {if(!Held)physics.Step(elapsed,bounds,Size,Size,.55,platforms);}
    public static void Collide(InteractiveToy a,InteractiveToy b)
    {
        var dx=b.X-a.X;var dy=b.Y-a.Y;var distance=Math.Sqrt(dx*dx+dy*dy);
        if(distance>=Size||a.Held&&b.Held)return;
        var nx=distance>.01?dx/distance:1;var ny=distance>.01?dy/distance:0;var overlap=Size-distance;
        var massA=a.Held?0:1;var massB=b.Held?0:1;var total=massA+massB;
        a.physics.X-=nx*overlap*massA/total;a.physics.Y-=ny*overlap*massA/total;
        b.physics.X+=nx*overlap*massB/total;b.physics.Y+=ny*overlap*massB/total;
        var relative=(b.VelocityX-a.VelocityX)*nx+(b.VelocityY-a.VelocityY)*ny;
        if(relative>=0)return;
        var impulse=-(1+.7)*relative/total;
        a.physics.VX-=impulse*nx*massA;a.physics.VY-=impulse*ny*massA;
        b.physics.VX+=impulse*nx*massB;b.physics.VY+=impulse*ny*massB;
    }
}
