using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopLife.App;

public sealed partial class RoomWindow
{
    private readonly Ellipse teaserHit=new(){Width=26,Height=26,Fill=new SolidColorBrush(Color.FromArgb(1,255,255,255))};
    private bool teaserHeld;
    private Point previousTeaserPointer;
    internal event Action<RoomWindow>? TeaserTouched;
    private void InitializeTeaserSurface()
    {
        if(Teaser is null)return;
        canvas.Children.Add(teaserHit);UpdateTeaserHit();
        teaserHit.MouseLeftButtonDown+=(_,e)=>
        {
            if(editing)return;
            if(!teaserHit.CaptureMouse())return;
            teaserHeld=true;previousTeaserPointer=e.GetPosition(canvas);
            Teaser.Bat(previousTeaserPointer.X<teaserString.X2?140:-140);TeaserTouched?.Invoke(this);e.Handled=true;
        };
        teaserHit.MouseMove+=(_,e)=>
        {
            if(!teaserHeld)return;
            var p=e.GetPosition(canvas);var dx=p.X-previousTeaserPointer.X;previousTeaserPointer=p;
            if(Math.Abs(dx)>.25)Teaser.Bat(Math.Clamp(dx*10,-100,100));e.Handled=true;
        };
        teaserHit.MouseLeftButtonUp+=(_,e)=>{if(!teaserHeld)return;teaserHeld=false;teaserHit.ReleaseMouseCapture();e.Handled=true;};
        teaserHit.LostMouseCapture+=(_,_)=>teaserHeld=false;
        IsVisibleChanged+=(_,_)=>{if(!IsVisible){teaserHeld=false;teaserHit.ReleaseMouseCapture();}};
    }
    private void UpdateTeaserHit()
    {
        if(Teaser is null)return;
        Canvas.SetLeft(teaserHit,teaserString.X2-13);Canvas.SetTop(teaserHit,teaserString.Y2-13);
    }
    private void CancelTeaserInput()
    {if(teaserHeld){teaserHeld=false;teaserHit.ReleaseMouseCapture();}}
}
