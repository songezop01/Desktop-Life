using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using DesktopLife.Core;
using DesktopLife.Windows;
namespace DesktopLife.App;

/// <summary>A static, click-through structure shared by all residents.</summary>
public sealed class HouseWindow : Window
{
    private readonly HouseDrawing drawing=new();
    public HouseWindow()
    {
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;ResizeMode=ResizeMode.NoResize;
        ShowActivated=false;ShowInTaskbar=false;Title="Desktop Life · 小屋樓板";Content=drawing;
        SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;DesktopInteraction.MakeNonActivating(h);DesktopInteraction.SetClickThrough(h,true);Place();};
    }
    public void Configure(HouseLayout layout){drawing.Layout=layout;Width=layout.WorkArea.Width;Height=layout.WorkArea.Height;drawing.InvalidateVisual();Place();}
    private void Place(){if(drawing.Layout is {} l)DisplayWorkspace.Position(this,l.WorkArea.Left,l.WorkArea.Top);}
    private sealed class HouseDrawing : FrameworkElement
    {
        public HouseLayout? Layout {get;set;}
        private static readonly Brush Wood=new SolidColorBrush(Color.FromRgb(207,157,126));
        private static readonly Brush Top=new SolidColorBrush(Color.FromRgb(250,224,192));
        private static readonly Brush Edge=new SolidColorBrush(Color.FromRgb(169,119,100));
        private static readonly Pen Grain=new(new SolidColorBrush(Color.FromArgb(90,169,119,100)),1);
        protected override void OnRender(DrawingContext dc)
        {
            if(Layout is not {} l)return;
            var s=l.SceneScale;var ox=l.WorkArea.Left;var oy=l.WorkArea.Top;
            foreach(var f in l.Floors)
            {
                var y=f.Y-oy;var x=f.Left-ox;
                dc.DrawRoundedRectangle(Wood,new Pen(Edge,.8*s),new Rect(x,y, f.Width,10*s),3*s,3*s);
                dc.DrawRectangle(Top,null,new Rect(x,y,f.Width,3*s));
                for(double plank=x+44*s;plank<x+f.Width;plank+=68*s)dc.DrawLine(Grain,new Point(plank,y+3*s),new Point(plank+1*s,y+9*s));
                dc.DrawLine(Grain,new Point(x+8*s,y+6*s),new Point(x+f.Width-8*s,y+6*s));
                // Warm carved brackets below the upper shelves.
                if(f.Index>0)foreach(var bx in new[]{x+32*s,x+f.Width-42*s})
                {dc.DrawRoundedRectangle(Edge,null,new Rect(bx,y+10*s,7*s,22*s),2*s,2*s);dc.DrawLine(new Pen(Wood,5*s),new Point(bx,y+28*s),new Point(bx+22*s,y+12*s));}
            }
            foreach(var stair in l.Stairs)
            {
                foreach(var step in stair.Steps)
                {
                    var r=new Rect(step.X-ox,step.Y-oy,step.Width+1*s,Math.Max(3*s,step.Rise));
                    dc.DrawRectangle(Wood,new Pen(Edge,.7*s),r);dc.DrawRectangle(Top,null,new Rect(r.X,r.Y,r.Width,3*s));
                }
                // The slender handrail follows the same support line as the feet.
                var a=new Point(stair.LowerLanding.X-ox,stair.LowerLanding.Y-oy-36*s);
                var b=new Point(stair.UpperLanding.X-ox,stair.UpperLanding.Y-oy-36*s);
                dc.DrawLine(new Pen(Edge,4*s),a,b);dc.DrawLine(new Pen(Top,1.3*s),a,b);
                for(var i=0;i<=4;i++){var t=i/4d;var x=a.X+(b.X-a.X)*t;var y=a.Y+(b.Y-a.Y)*t;dc.DrawLine(new Pen(Wood,3*s),new Point(x,y),new Point(x,y+36*s));}
            }
        }
    }
}
