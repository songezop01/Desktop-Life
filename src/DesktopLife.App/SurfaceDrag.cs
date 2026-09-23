using System.Windows;
using System.Windows.Input;
using DesktopLife.Core;
namespace DesktopLife.App;

// Capture only a painted pet/toy; transparent desktop areas never receive input.
public sealed class SurfaceDrag
{
    private readonly DragGesture gesture=new();
    public bool Active=>gesture.Active;
    public bool Dragged=>gesture.Dragged;
    public Point PressPoint {get;private set;}
    public double VelocityX {get;private set;}
    public double VelocityY {get;private set;}
    public string Diagnostic {get;private set;}="none";
    private Point? diagnosticPointer;
    internal void WithDiagnosticPointer(Point point,Action action)
    {
        diagnosticPointer=point;
        try{action();}finally{diagnosticPointer=null;}
    }
    public SurfaceDrag(Window window,UIElement surface,Action<bool> held,Action<double,double> move,Action<bool> released)
    {
        var time=System.Diagnostics.Stopwatch.StartNew();
        Point previous=default;double previousTime=0;
        Point Pointer(MouseEventArgs e)
        {
            if(diagnosticPointer is {} sample)return sample;
            var physical=window.PointToScreen(e.GetPosition(window));
            return DisplayWorkspace.FromPixels(physical);
        }
        surface.AddHandler(Mouse.MouseDownEvent,new MouseButtonEventHandler((_,e)=>
        {
            if(e.ChangedButton!=MouseButton.Left)return;
            Diagnostic="down";
            PressPoint=diagnosticPointer is {} local?new Point(local.X-window.Left,local.Y-window.Top):e.GetPosition(window);
            var p=Pointer(e);gesture.Begin(p.X,p.Y,window.Left,window.Top);
            previous=p;previousTime=time.Elapsed.TotalSeconds;VelocityX=VelocityY=0;
            if(!surface.CaptureMouse()){Diagnostic="capture failed";gesture.End();return;}
            Diagnostic="captured";
            held(true);e.Handled=true;
        }));
        surface.MouseMove+=(_,e)=>
        {
            if(!gesture.Active)return;
            var p=Pointer(e);var now=time.Elapsed.TotalSeconds;var dt=Math.Max(.008,now-previousTime);
            VelocityX=Math.Clamp((p.X-previous.X)/dt,-500,500);VelocityY=Math.Clamp((p.Y-previous.Y)/dt,-500,500);
            previous=p;previousTime=now;
            if(gesture.Move(p.X,p.Y) is {} at)move(at.X,at.Y);
            e.Handled=true;
        };
        surface.AddHandler(Mouse.MouseUpEvent,new MouseButtonEventHandler((_,e)=>
        {
            if(e.ChangedButton!=MouseButton.Left||!gesture.Active)return;
            Diagnostic="released";
            if(time.Elapsed.TotalSeconds-previousTime>.15)VelocityX=VelocityY=0;
            var dragged=gesture.End();surface.ReleaseMouseCapture();held(false);released(dragged);e.Handled=true;
        }));
        surface.LostMouseCapture+=(_,_)=>{if(gesture.Active){gesture.End();held(false);}};
        window.IsVisibleChanged+=(_,_)=>{if(!window.IsVisible&&gesture.Active){gesture.End();surface.ReleaseMouseCapture();held(false);}};
    }
}
