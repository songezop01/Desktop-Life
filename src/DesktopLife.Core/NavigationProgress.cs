namespace DesktopLife.Core;
public sealed class NavigationProgress
{
    private double x,y,still,total;
    private bool initialized;
    public void Reset(){initialized=false;still=total=0;}
    public bool Stalled(double px,double py,double dt,bool attempting)
    {
        if(!attempting){Reset();return false;}
        if(!initialized){x=px;y=py;initialized=true;}
        total+=dt;still+=dt;
        if(Math.Abs(px-x)+Math.Abs(py-y)>3){x=px;y=py;still=0;}
        return still>3||total>20;
    }
}
