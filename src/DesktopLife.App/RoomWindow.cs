using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Interop;
using DesktopLife.Core;
using DesktopLife.Windows;
namespace DesktopLife.App;

public sealed class RoomWindow : Window
{
    public RoomItem Item {get;private set;}
    private readonly Canvas canvas=new(){Background=Brushes.Transparent};
    private readonly SurfaceDrag drag;
    public event Action<RoomWindow>? Removed;
    public event Action? Changed;
    private bool editing;
    public HangingToy? Teaser { get; }
    private readonly Line teaserString = new() { Stroke = Brushes.Tan, StrokeThickness = 2, IsHitTestVisible = false };
    private readonly Ellipse teaserBall = new() { Width = 18, Height = 18, Fill = new SolidColorBrush(Color.FromRgb(214,155,125)), Stroke = Brushes.Bisque, StrokeThickness = 1, IsHitTestVisible = false };
    public Point TeaserPosition => new(Left + 45 + (Teaser?.X ?? 0), Top + 31 + (Teaser?.Y ?? 0));
    public RoomWindow(RoomItem item)
    {
        Item=item;(Width,Height)=Size(item.Kind);
        if(item.Kind==FurnitureKind.CatTree)Teaser=new();
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;ShowActivated=false;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;
        Title="房間家具 · "+UiText.Label(item.Kind);Content=canvas;Draw();Place(item.X,item.Y);
        SourceInitialized+=(_,_)=>{DesktopInteraction.MakeNonActivating(new WindowInteropHelper(this).Handle);Place(Item.X,Item.Y);SetEditing(editing);};
        drag=new(this,canvas,_=>{},(x,y)=>Place(x,y),_=>Changed?.Invoke());
        var menu=new ContextMenu();var remove=new MenuItem{Header="收起這件家具"};remove.Click+=(_,_)=>Removed?.Invoke(this);menu.Items.Add(remove);canvas.ContextMenu=menu;
    }
    public static (double Width,double Height) Size(FurnitureKind kind)=>kind switch
    {FurnitureKind.CatTree=>(180,230),FurnitureKind.Slide=>(260,190),FurnitureKind.Desk=>(230,160),FurnitureKind.Bookshelf=>(180,220),_=>(140,45)};
    public void SetEditing(bool value)
    {
        editing=value;canvas.IsHitTestVisible=value;Opacity=value?.8:1;
        var handle=new WindowInteropHelper(this).Handle;if(handle!=0)DesktopInteraction.SetClickThrough(handle,!value);
    }
    private void Place(double x,double y)
    {
        var r=DisplayWorkspace.Bounds;x=Math.Clamp(x,r.Left,Math.Max(r.Left,r.Left+r.Width-Width));y=Math.Clamp(y,r.Top,Math.Max(r.Top,r.Top+r.Height-Height));
        Item=Item with{X=x,Y=y};DisplayWorkspace.Position(this,x,y);
    }
    public void Relocate(double x,double y)=>Place(x,y);
    public IReadOnlyList<RoomPlatform> Platforms=>Item.Kind switch
    {
        FurnitureKind.CatTree=>[new(Left+18,125,Top+15,Top+15),new(Left+65,110,Top+112,Top+112)],
        FurnitureKind.Slide=>[new(Left+12,65,Top+20,Top+20),new(Left+77,170,Top+20,Top+174)],
        FurnitureKind.Desk=>[new(Left+5,220,Top+22,Top+22)],
        FurnitureKind.Bookshelf=>[new(Left+5,170,Top+12,Top+12),new(Left+15,150,Top+105,Top+105)],
        _=>[new(Left+10,120,Top+15,Top+15)]
    };
    public void StepTeaser(double seconds)
    {
        if(Teaser is null)return;
        Teaser.Step(seconds);
        teaserString.X1=45;teaserString.Y1=31;
        teaserString.X2=45+Teaser.X;teaserString.Y2=31+Teaser.Y;
        Canvas.SetLeft(teaserBall,teaserString.X2-9);Canvas.SetTop(teaserBall,teaserString.Y2-9);
    }
    private void Draw()
    {
        Brush B(string color)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        void Box(double x,double y,double w,double h,string color,double radius=5)
        {var r=new Rectangle{Width=w,Height=h,Fill=B(color),RadiusX=radius,RadiusY=radius};Canvas.SetLeft(r,x);Canvas.SetTop(r,y);canvas.Children.Add(r);}
        void Line(string data,string color,double thickness)
        {canvas.Children.Add(new Path{Data=Geometry.Parse(data),Stroke=B(color),StrokeThickness=thickness,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});}
        switch(Item.Kind)
        {
            case FurnitureKind.CatTree:
                Box(13,215,154,15,"#9B7759");Box(82,23,20,195,"#D4BA91");
                for(var y=30;y<210;y+=9)Line($"M 83,{y} L 101,{y+4}","#AA8D68",2);
                Box(18,15,125,16,"#8DAF9B");Box(65,112,110,14,"#8DAF9B");Box(17,159,64,57,"#C7A886");
                Box(30,175,38,41,"#65564B",18);
                canvas.Children.Add(teaserString);canvas.Children.Add(teaserBall);StepTeaser(0);break;
            case FurnitureKind.Slide:
                Box(20,26,12,152,"#BA936F");Box(62,26,12,152,"#BA936F");
                for(var y=50;y<170;y+=25)Box(25,y,43,7,"#CFAE87");
                Box(12,20,65,13,"#87AB96");Line("M 77,24 L 247,178","#7AA4A9",17);Line("M 79,13 L 250,166","#B2D2CA",5);break;
            case FurnitureKind.Desk:
                Box(22,32,16,125,"#A47D5F");Box(193,32,16,125,"#A47D5F");Box(5,22,220,16,"#C9A981");
                Box(133,40,65,32,"#DFC3A0");Box(157,52,17,4,"#987A5E");Box(22,7,42,15,"#9BB6A0");Box(27,2,36,7,"#F0E1C4");break;
            case FurnitureKind.Bookshelf:
                Box(5,12,170,203,"#B49370");Box(16,27,148,175,"#E0CAAC");
                foreach(var row in new[]{35,121}){for(var i=0;i<7;i++)Box(24+i*18,row+i%3*4,13,64-i%3*4,i%3==0?"#8BAE9A":i%3==1?"#BC8E83":"#D1B469",2);}
                Box(10,105,160,9,"#A98461");Box(10,198,160,10,"#A98461");break;
            default:
                Box(4,17,132,27,"#A9BBA1",18);Box(10,12,120,22,"#DADEC6",15);Line("M 32,27 Q 70,37 108,27","#A9BBA1",2);break;
        }
    }
}
