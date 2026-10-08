namespace DesktopLife.Core;

public enum FurnitureKind { CatTree, Slide, Desk, Bookshelf, Cushion, Yarn, BellBall, ToyMouse, Box, Scratcher, PetBed, HumanBed, Chair, DiningTable, Computer, DrawingBook, LegoBox, Sofa, CatBowl }
public sealed record RoomItem(Guid Id,FurnitureKind Kind,double X,double Y,int FloorIndex=0);
public sealed record RoomPoint(double X,double Y);
public sealed class RoomState
{
    public RoomPoint? Ball {get;set;}
    public RoomPoint? Square {get;set;}
    public List<RoomItem> Items {get;set;}=[];
    public BodyBounds? WorkArea {get;set;}
    public int FloorCount {get;set;}=1;
    public void Validate()
    {if(FloorCount<1||FloorCount>HouseLayout.MaximumFloorCount)throw new InvalidDataException("樓層數必須為 1、2 或 3。");
     if(WorkArea is {} area&&(new[]{area.Left,area.Top,area.Width,area.Height}.Any(n=>!double.IsFinite(n)||Math.Abs(n)>100000)||area.Width<=0||area.Height<=0))throw new InvalidDataException("房間螢幕範圍無效。");
     if(new[]{Ball,Square}.Any(p=>p is not null&&(!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>100000||Math.Abs(p.Y)>100000)))throw new InvalidDataException("玩具位置無效。");
     if(Items is null||Items.Count>24||Items.Any(i=>i is null||!Enum.IsDefined(i.Kind)||i.FloorIndex<0||i.FloorIndex>=FloorCount||!double.IsFinite(i.X)||!double.IsFinite(i.Y)||Math.Abs(i.X)>100000||Math.Abs(i.Y)>100000)||Items.Select(i=>i.Id).Distinct().Count()!=Items.Count)throw new InvalidDataException("房間配置無效。");}
}
public readonly record struct RoomPlatform(double X,double Width,double Y,double EndY)
{
    public double HeightAt(double x)=>Y+(EndY-Y)*Math.Clamp((x-X)/Width,0,1);
}
public sealed class GravityBody
{
    public double X,Y,VX,VY;
    public bool Grounded {get;private set;}
    public const double Gravity=1000;
    public const double MaximumSubstepSeconds=.008;
    public const double PlatformContactTolerance=5;
    public void PlaceSupported(double x,double y,double width,double height,RoomPlatform platform)
    {
        var center=x+width/2;var feet=y+height;
        if(!double.IsFinite(x)||!double.IsFinite(y)||center<platform.X-.001||center>platform.X+platform.Width+.001||Math.Abs(platform.HeightAt(center)-feet)>1)
            throw new InvalidOperationException("角色腳底未接觸樓梯或樓板。");
        X=x;Y=y;VX=VY=0;Grounded=true;
    }
    public void Step(double elapsed,BodyBounds bounds,double width,double height,double bounce,IReadOnlyList<RoomPlatform>? platforms=null)
    {
        if(!double.IsFinite(elapsed)||elapsed<0)throw new ArgumentOutOfRangeException(nameof(elapsed));
        var count=Math.Max(1,(int)Math.Ceiling(Math.Min(elapsed,.1)/MaximumSubstepSeconds));var dt=Math.Min(elapsed,.1)/count;
        for(var i=0;i<count;i++)
        {
            var oldBottom=Y+height;VY+=Gravity*dt;X+=VX*dt;Y+=VY*dt;
            var right=bounds.Left+Math.Max(0,bounds.Width-width);var floor=bounds.Top+bounds.Height;
            if(X<bounds.Left){X=bounds.Left;VX=bounce>0?Math.Max(180,Math.Abs(VX)*.88):0;}
            if(X>right){X=right;VX=bounce>0?-Math.Max(180,Math.Abs(VX)*.88):0;}
            if(Y<bounds.Top){Y=bounds.Top;VY=Math.Max(0,VY);}
            RoomPlatform? support=null;
            foreach(var platform in platforms??[])
            {
                var center=X+width/2;
                if(center<platform.X||center>platform.X+platform.Width)continue;
                var surface=platform.HeightAt(center);
                if(surface>=bounds.Top+height&&VY>=0&&oldBottom<=surface+PlatformContactTolerance&&Y+height>=surface&&surface<floor){floor=surface;support=platform;}
            }
            Grounded=false;
            if(Y+height>=floor)
            {
                Y=floor-height;
                VY=Math.Abs(VY)>90?-VY*bounce:0;
                Grounded=VY==0;
                if(support is {} slope && Math.Abs(slope.EndY-slope.Y)>1){VX+=Gravity*(slope.EndY-slope.Y)/slope.Width*dt*.65;}
            }
            VX*=Math.Exp(-(Grounded?2.2:.18)*dt);
            if(Math.Abs(VX)<.4)VX=0;
        }
    }
    public static (double X,double Y) ClickImpulse(double clickX,double clickY,double size,double speed=320)
    {
        var dx=size/2-clickX;var dy=size/2-clickY;var length=Math.Sqrt(dx*dx+dy*dy);
        // A dead-center click has no lateral direction: give a small vertical lift.
        if(length<1)return (0,-speed*.6);
        return(dx/length*speed,dy/length*speed);
    }
}
