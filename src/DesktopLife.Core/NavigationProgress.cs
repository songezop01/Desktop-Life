namespace DesktopLife.Core;
public sealed class NavigationProgress
{
    private double x,y,still;
    private bool initialized;
    public void Reset(){initialized=false;still=0;}
    public bool Stalled(double px,double py,double dt,bool attempting)
    {
        if(!attempting){Reset();return false;}
        if(!initialized){x=px;y=py;initialized=true;}
        still+=dt;
        if(Math.Abs(px-x)+Math.Abs(py-y)>3){x=px;y=py;still=0;}
        // A long floor walk or a multi-floor stair route may legitimately take more than twenty seconds.
        // Failure is lack of displacement while movement is requested, never the age of a moving route.
        return still>3;
    }
}
