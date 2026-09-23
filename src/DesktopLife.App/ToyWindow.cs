using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopLife.Core;
using DesktopLife.Windows;
namespace DesktopLife.App;

public sealed class ToyWindow : Window
{
    // HWND bounds must contain the rotated and held square, not only its collision body.
    public const double VisualPadding=12;
    private readonly Canvas artwork=new(){Width=40,Height=40,Background=null,ClipToBounds=false};
    public RoomItem? RoomItem {get;set;}
    public IReadOnlyList<RoomPlatform> Platforms {get;set;}=[];
    public InteractiveToy Model {get;}
    public event Action<double>? Rang;
    private double ringingTime;
    public event Action? Played;
    public event Action? RemoveRequested;
    public void SetToyAppearance(FurnitureKind kind)
    {
        Title=UiText.Label(kind);
        if(shape is Shape drawing){drawing.Fill=kind==FurnitureKind.BellBall?Brushes.Goldenrod:kind==FurnitureKind.ToyMouse?Brushes.SlateGray:Brushes.CadetBlue;drawing.Stroke=Brushes.PaleTurquoise;}
        if(kind==FurnitureKind.Yarn)for(var i=0;i<3;i++)artwork.Children.Add(new Path{Data=Geometry.Parse($"M 5,{12+i*7} Q 20,{25+i*4} 35,{9+i*7}"),Stroke=Brushes.PaleTurquoise,StrokeThickness=1.3,IsHitTestVisible=false});
        if(kind==FurnitureKind.ToyMouse)
        {artwork.Children.Add(new Path{Data=Geometry.Parse("M 5,9 Q 1,0 11,3 M 29,3 Q 39,0 34,11 M 10,22 L 11,22 M 27,22 L 28,22 M 18,29 L 22,29"),Stroke=Brushes.LightPink,StrokeThickness=4,IsHitTestVisible=false});}
        if(kind==FurnitureKind.BellBall)artwork.Children.Add(new Path{Data=Geometry.Parse("M 20,9 L 20,25 M 14,26 Q 20,33 26,26"),Stroke=Brushes.SaddleBrown,StrokeThickness=3,IsHitTestVisible=false});
        var menu=new ContextMenu();var item=new MenuItem{Header="收起這件玩具"};item.Click+=(_,_)=>RemoveRequested?.Invoke();menu.Items.Add(item);shape.ContextMenu=menu;
    }
    private readonly FrameworkElement shape;
    private readonly RotateTransform rotation=new();
    private readonly ScaleTransform scale=new(1,1);
    private readonly SurfaceDrag drag;
    private double angle;
    public ToyWindow(bool ball,BodyBounds bounds)
    {
        Model=new(bounds.Left+bounds.Width*(ball?.7:.3),bounds.Top+bounds.Height-65);
        Width=Height=InteractiveToy.Size+VisualPadding*2;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;
        ResizeMode=ResizeMode.NoResize;ShowActivated=false;ShowInTaskbar=false;Topmost=true;
        Title=ball?"桌寵圓球":"桌寵方塊";
        var canvas=new Canvas{Background=null,ClipToBounds=false};Content=canvas;
        Canvas.SetLeft(artwork,VisualPadding);Canvas.SetTop(artwork,VisualPadding);canvas.Children.Add(artwork);
        shape=ball?new Ellipse{Width=38,Height=38,Fill=Brushes.Coral,Stroke=Brushes.OrangeRed,StrokeThickness=2}
            :new Rectangle{Width=36,Height=36,Fill=Brushes.MediumPurple,Stroke=Brushes.Lavender,StrokeThickness=3,RadiusX=3,RadiusY=3};
        Canvas.SetLeft(shape,ball?1:2);Canvas.SetTop(shape,ball?1:2);artwork.Children.Add(shape);
        var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(rotation);
        artwork.RenderTransformOrigin=new Point(.5,.5);artwork.RenderTransform=transforms;
        shape.ToolTip=ball?"拖曳圓球；輕點可彈動，桌寵會追球。":"拖曳玩具方塊；這不是 Windows 快捷圖示。";
        SourceInitialized+=(_,_)=>{DesktopInteraction.MakeNonActivating(new WindowInteropHelper(this).Handle);Sync();};
        drag=new(this,shape,held=>{Model.Held=held;scale.ScaleX=scale.ScaleY=held?1.12:1;},
            (x,y)=>{Model.Place(x+VisualPadding,y+VisualPadding,Bounds());Sync();},
            moved=>{if(moved)Model.Kick(drag!.VelocityX,drag.VelocityY);else Model.Hit(drag!.PressPoint.X-VisualPadding,drag.PressPoint.Y-VisualPadding);if(RoomItem?.Kind==FurnitureKind.BellBall)Rang?.Invoke(.8);Played?.Invoke();});
        Sync();
    }
    public void SmokeDirectionalHit()
    {
        void Click(double x)
        {
            drag.WithDiagnosticPointer(new Point(Left+VisualPadding+x,Top+VisualPadding+20),()=>
            {
                shape.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.MouseDownEvent});
                shape.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.MouseUpEvent});
            });
        }
        Click(5);if(Model.VelocityX<=0)throw new Exception("Left contact did not propel toy right.");
        Click(35);if(Model.VelocityX>=0)throw new Exception("Right contact did not propel toy left.");
    }
    private static BodyBounds Bounds()=>DisplayWorkspace.Bounds;
    public void SmokeRotatedBounds(string path)
    {
        var saved=rotation.Angle;
        try
        {
            rotation.Angle=45;scale.ScaleX=scale.ScaleY=1.12;UpdateLayout();
            var frame=new System.Windows.Media.Imaging.RenderTargetBitmap(64,64,96,96,PixelFormats.Pbgra32);
            frame.Render((Visual)Content);
            var pixels=new byte[64*64*4];frame.CopyPixels(pixels,64*4,0);
            for(var i=0;i<64;i++)
                if(pixels[(i*64)*4+3]!=0||pixels[(i*64+63)*4+3]!=0||pixels[i*4+3]!=0||pixels[(63*64+i)*4+3]!=0)
                    throw new Exception("Rotating toy paint reaches the native window edge.");
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
            using var file=System.IO.File.Create(path);encoder.Save(file);
            if(InputHitTest(new Point(0,0)) is not null)throw new Exception("Toy transparent padding blocks input.");
        }
        finally{rotation.Angle=saved;scale.ScaleX=scale.ScaleY=1;}
    }
    private void Sync()=>DisplayWorkspace.Position(this,Model.X-VisualPadding,Model.Y-VisualPadding);
    public void Step(double dt)
    {
        var vx=Model.VelocityX;var vy=Model.VelocityY;
        Model.Step(dt,Bounds(),Platforms);
        ringingTime+=dt;
        if(RoomItem?.Kind==FurnitureKind.BellBall&&ringingTime>.25&&(Math.Abs(Model.VelocityX-vx)>60||Math.Abs(Model.VelocityY-vy)>100||(Math.Abs(Model.VelocityX)>55&&ringingTime>.5)))
        {Rang?.Invoke(Math.Clamp((Math.Abs(vx)+Math.Abs(vy))/450,.25,1));ringingTime=0;}
        angle+=Model.VelocityX*dt;rotation.Angle=angle;Sync();
    }
}
