using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Interop;
using DesktopLife.Core;
using DesktopLife.Windows;
namespace DesktopLife.App;

public sealed partial class RoomWindow : Window
{
    public RoomItem Item {get;private set;}
    private (RoomItem Item,double Scale,bool Editing)? contextKey;
    private FurnitureContext? cachedContext;
    public int ContextRebuilds {get;private set;}
    public FurnitureContext Context
    {
        get
        {
            var key=(Item,SceneScale,editing);
            if(contextKey!=key||cachedContext is null)
            {
                contextKey=key;cachedContext=new(Item,FurnitureAffordance.For(Item.Kind),Platforms,!editing);ContextRebuilds++;
            }
            return cachedContext;
        }
    }
    private readonly Canvas canvas=new(){Background=Brushes.Transparent};
    private readonly SurfaceDrag drag;
    public event Action<RoomWindow>? Removed;
    public event Action? Changed;
    private bool editing;
    private bool editingApplied;
    public HangingToy? Teaser { get; }
    private readonly Line teaserString = new() { Stroke = Brushes.Tan, StrokeThickness = 2, IsHitTestVisible = false };
    private readonly Ellipse teaserBall = new() { Width = 18, Height = 18, Fill = new SolidColorBrush(Color.FromRgb(214,155,125)), Stroke = Brushes.Bisque, StrokeThickness = 1, IsHitTestVisible = false };
    public double SceneScale {get;private set;}=1;
    private Point TeaserAnchor {get{var size=Size(Item.Kind);return FurnitureArt.LoadedCount>0?FurnitureArt.Contact(Item.Kind,70,126,size.Width,size.Height):new Point(45,31);}}
    internal Point TeaserAnchorPosition {get{var anchor=TeaserAnchor;return new(Item.X+anchor.X*SceneScale,Item.Y+anchor.Y*SceneScale);}}
    public Point TeaserPosition {get{var anchor=TeaserAnchor;return new(Item.X+(anchor.X+(Teaser?.X??0))*SceneScale,Item.Y+(anchor.Y+(Teaser?.Y??0))*SceneScale);}}
    public RoomWindow(RoomItem item)
    {
        Item=item;(Width,Height)=Size(item.Kind);canvas.Width=Width;canvas.Height=Height;
        if(item.Kind==FurnitureKind.CatTree)Teaser=new();
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;ShowActivated=false;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;
        Title="房間家具 · "+UiText.Label(item.Kind);Content=canvas;Draw();Place(item.X,item.Y);
        SourceInitialized+=(_,_)=>{DesktopInteraction.MakeNonActivating(new WindowInteropHelper(this).Handle);AttachSurfaceInput();Place(Item.X,Item.Y);SetEditing(editing);};
        drag=new(this,canvas,held=>SetCombHeld(held),(x,y)=>{if(editing)Place(x,y);else MoveComb(x,y);},_=>{if(editing)Changed?.Invoke();},()=>editing||IsCombTool);
        var menu=new ContextMenu();var remove=new MenuItem{Header="收起這件家具"};remove.Click+=(_,_)=>Removed?.Invoke(this);menu.Items.Add(remove);canvas.ContextMenu=menu;
        InitializeFoodSurface();
        InitializeTeaserSurface();
    }
    public static (double Width,double Height) Size(FurnitureKind kind)=>kind switch
    {FurnitureKind.HumanBed=>(280,110),FurnitureKind.Sofa=>(260,120),FurnitureKind.Chair=>(110,145),FurnitureKind.DiningTable=>(250,160),FurnitureKind.Computer=>(110,90),FurnitureKind.DrawingBook=>(100,28),FurnitureKind.LegoBox=>(130,65),FurnitureKind.CatBowl=>(80,28),FurnitureKind.Comb=>(120,40),FurnitureKind.CatTree=>(180,230),FurnitureKind.Slide=>(260,190),FurnitureKind.Desk=>(230,160),FurnitureKind.Bookshelf=>(180,220),FurnitureKind.Box=>(160,100),FurnitureKind.Scratcher=>(160,35),FurnitureKind.PetBed=>(170,55),_=>(140,45)};
    public void SetEditing(bool value)
    {
        if(editing==value&&editingApplied)return;
        CancelCombDrag();CancelTeaserInput();editing=value;canvas.IsHitTestVisible=value||IsFoodSurface||IsCombTool||Teaser is not null;Opacity=value?.8:1;
        canvas.Background=value||(!IsFoodSurface&&!IsCombTool&&Teaser is null)?Brushes.Transparent:null;
        var handle=new WindowInteropHelper(this).Handle;
        if(handle!=0){DesktopInteraction.SetClickThrough(handle,!value&&!IsFoodSurface&&!IsCombTool&&Teaser is null);editingApplied=true;}
    }
    private void Place(double x,double y)
    {
        CancelCombDrag();var r=DisplayWorkspace.Bounds;x=Math.Clamp(x,r.Left,Math.Max(r.Left,r.Left+r.Width-Width));y=Math.Clamp(y,r.Top,Math.Max(r.Top,r.Top+r.Height-Height));
        Item=Item with{X=x,Y=y};DisplayWorkspace.Position(this,x,y);
    }
    public void Relocate(double x,double y)=>Place(x,y);
    public void SetFloor(int floor){if(floor<0||floor>2)throw new System.ArgumentOutOfRangeException(nameof(floor));Item=Item with{FloorIndex=floor};}
    public void SetSceneScale(double scale)
    {
        if(!double.IsFinite(scale)||scale<=0||scale>4)throw new System.ArgumentOutOfRangeException(nameof(scale));
        if(System.Math.Abs(SceneScale-scale)<.00001)return;SceneScale=scale;
        var size=Size(Item.Kind);Width=size.Width*scale;Height=size.Height*scale;
        canvas.LayoutTransform=new ScaleTransform(scale,scale);platformKey=null;contextKey=null;
        Place(Item.X,Item.Y);
    }
    private (RoomItem Item,double Scale)? platformKey;
    private IReadOnlyList<RoomPlatform> cachedPlatforms=[];
    public IReadOnlyList<RoomPlatform> Platforms
    {
        get {var key=(Item,SceneScale);if(platformKey!=key){platformKey=key;cachedPlatforms=BuildPlatforms();}return cachedPlatforms;}
    }
    private IReadOnlyList<RoomPlatform> BuildLegacyPlatforms()=>Item.Kind switch
    {
        FurnitureKind.HumanBed=>[new(Item.X+12,256,Item.Y+50,Item.Y+50)],
        FurnitureKind.Sofa=>[new(Item.X+20,220,Item.Y+67,Item.Y+67)],
        FurnitureKind.Chair=>[new(Item.X+10,90,Item.Y+80,Item.Y+80)],
        FurnitureKind.DiningTable=>[new(Item.X+5,240,Item.Y+22,Item.Y+22)],
        FurnitureKind.Computer or FurnitureKind.DrawingBook or FurnitureKind.LegoBox or FurnitureKind.CatBowl or FurnitureKind.Comb=>[],
        FurnitureKind.PetBed=>[new(Item.X+15,140,Item.Y+28,Item.Y+28)],
        FurnitureKind.Scratcher=>[new(Item.X+10,140,Item.Y+16,Item.Y+16)],
        FurnitureKind.Box=>[new(Item.X+18,124,Item.Y+94,Item.Y+94)],
        FurnitureKind.CatTree=>[new(Item.X+18,125,Item.Y+15,Item.Y+15),new(Item.X+65,110,Item.Y+112,Item.Y+112)],
        FurnitureKind.Slide=>[new(Item.X+12,65,Item.Y+20,Item.Y+20),new(Item.X+77,170,Item.Y+20,Item.Y+174)],
        FurnitureKind.Desk=>[new(Item.X+5,220,Item.Y+22,Item.Y+22)],
        FurnitureKind.Bookshelf=>[new(Item.X+5,170,Item.Y+12,Item.Y+12),new(Item.X+15,150,Item.Y+105,Item.Y+105)],
        _=>[new(Item.X+10,120,Item.Y+15,Item.Y+15)]
    };
    public void StepTeaser(double seconds)
    {
        if(Teaser is null||seconds>0&&Teaser.IsResting)return;
        Teaser.Step(seconds);
        var anchor=TeaserAnchor;teaserString.X1=anchor.X;teaserString.Y1=anchor.Y;
        teaserString.X2=anchor.X+Teaser.X;teaserString.Y2=anchor.Y+Teaser.Y;
        Canvas.SetLeft(teaserBall,teaserString.X2-9);Canvas.SetTop(teaserBall,teaserString.Y2-9);
        UpdateTeaserHit();
    }
    private IReadOnlyList<RoomPlatform> BuildPlatforms()
    {
        var size=Size(Item.Kind);
        if(FurnitureArt.LoadedCount==0)return BuildLegacyPlatforms().Select(p=>new RoomPlatform(
            Item.X+(p.X-Item.X)*SceneScale,p.Width*SceneScale,
            Item.Y+(p.Y-Item.Y)*SceneScale,Item.Y+(p.EndY-Item.Y)*SceneScale)).ToArray();
        return FurnitureArt.Platforms(Item.Kind,size.Width,size.Height).Select(p=>new RoomPlatform(Item.X+p.X*SceneScale,p.Width*SceneScale,Item.Y+p.Y*SceneScale,Item.Y+p.EndY*SceneScale)).ToArray();
    }
    private void Draw()
    {
        if(FurnitureArt.LoadedCount==0){DrawLegacy();return;}
        var size=Size(Item.Kind);canvas.Children.Add(new FurnitureSpriteVisual(Item.Kind,size.Width,size.Height,removeStaticTeaser:Teaser is not null,interactive:IsCombTool));
        if(Teaser is not null){canvas.Children.Add(teaserString);canvas.Children.Add(teaserBall);StepTeaser(0);}
    }
    private void DrawLegacy()
    {
        Brush B(string color)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        void Box(double x,double y,double w,double h,string color,double radius=5)
        {var r=new Rectangle{Width=w,Height=h,Fill=B(color),RadiusX=radius,RadiusY=radius};Canvas.SetLeft(r,x);Canvas.SetTop(r,y);canvas.Children.Add(r);}
        void Line(string data,string color,double thickness)
        {canvas.Children.Add(new Path{Data=Geometry.Parse(data),Stroke=B(color),StrokeThickness=thickness,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});}
        switch(Item.Kind)
        {
            case FurnitureKind.HumanBed:
                Box(5,12,16,88,"#A47D5F");Box(10,56,260,35,"#BA9673");Box(20,48,245,24,"#EEE3CB",10);
                Box(30,36,58,19,"#FAF4E7",9);Box(93,50,164,28,"#A2BDB0",8);Box(24,90,14,20,"#99785C");Box(244,90,14,20,"#99785C");break;
            case FurnitureKind.Sofa:
                Box(15,20,230,75,"#829F96",18);Box(24,62,210,32,"#BACBBC",12);Box(5,52,24,48,"#91ACA0",10);Box(232,52,24,48,"#91ACA0",10);
                Line("M 128,67 L 128,90","#92AC9B",2);Box(24,100,14,18,"#A47D5F");Box(222,100,14,18,"#A47D5F");break;
            case FurnitureKind.Chair:
                Box(14,10,82,72,"#C7A782",8);Box(20,18,70,51,"#A6BCAB",7);Box(10,80,90,15,"#C7A782");Box(16,94,12,50,"#A47D5F");Box(83,94,12,50,"#A47D5F");break;
            case FurnitureKind.DiningTable:
                Box(20,37,15,120,"#A47D5F");Box(216,37,15,120,"#A47D5F");Box(5,22,240,17,"#D3B58E");Line("M 24,29 L 225,29","#E7CFAD",2);break;
            case FurnitureKind.Computer:
                Box(9,4,92,56,"#657A79");Box(15,10,80,43,"#B8D5CF",2);Box(49,60,12,12,"#657A79");Box(29,70,52,7,"#8A9B94");Box(12,79,86,10,"#DDD8C5",3);Line("M 22,84 L 86,84","#A5ADA1",2);break;
            case FurnitureKind.DrawingBook:
                Box(4,5,92,22,"#A9B9AA",2);Box(9,2,84,21,"#FAF1DC",2);Line("M 50,3 L 50,22","#CEBDA4",1);Line("M 62,14 L 83,7","#C89368",3);break;
            case FurnitureKind.LegoBox:
                Box(6,18,118,44,"#BC9B7B");Box(12,10,26,18,"#94B5A2",2);Box(45,6,27,22,"#D6AD72",2);Box(79,13,28,18,"#BC8F8C",2);Box(6,28,118,34,"#DDC3A1");break;
            case FurnitureKind.CatBowl:
                Box(8,9,64,17,"#9CB9AF",10);Box(14,7,52,9,"#DAC39F",6);Line("M 20,10 L 61,10","#A7855C",3);break;
            case FurnitureKind.PetBed:
                Box(3,10,164,43,"#8EA896",22);Box(15,18,140,28,"#ECE1C5",20);
                Line("M 34,30 Q 85,49 136,30","#CBBDA0",2);Box(4,39,162,13,"#A7BCAC",8);break;
            case FurnitureKind.Scratcher:
                Box(4,18,152,16,"#9B7759",6);Box(10,16,140,12,"#D4BA91",3);
                for(var x=16;x<146;x+=6)Line($"M {x},18 L {x-3},26","#AF926B",1);
                break;
            case FurnitureKind.Box:
                Box(10,27,140,69,"#AD8057",3);Box(18,31,124,57,"#65503E",2);
                Line("M 10,30 L 2,12 L 72,20 M 150,30 L 157,12 L 86,20","#CBA77F",10);
                Box(10,60,140,38,"#D5AE7F",3);Line("M 81,62 L 81,96","#B48B62",3);
                Line("M 32,77 L 45,77 M 118,82 L 130,82","#AA805D",2);break;
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
