using System.Windows;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    public List<RoomWindow> Furniture {get;}=[];
    public List<ToyWindow> ExtraToys {get;}=[];
    public IEnumerable<ToyWindow> AllToys=>new[]{Square,Ball}.Concat(ExtraToys);
    public event Action? RoomChanged;
    public bool EditingRoom {get;private set;}
    private readonly GravityBody petGravity=new();
    private double lastJump;
    private ToyWindow? playTarget;
    private RoomWindow? teaserTarget;
    private Point? teaserPawTarget;
    private int playSession;
    private double lastTeaserTap = -10;
    public int TeaserContactCount { get; private set; }
    public void RestoreRoom(RoomState state,BodyBounds? legacyBounds=null)
    {
        state.Validate();var from=state.WorkArea??legacyBounds??Bounds();
        RoomPoint Map(RoomPoint point,double w,double h)=>RoomCoordinates.Rehome(point,from,Bounds(),w,h);
        if(state.Ball is {} ball){var p=Map(ball,40,40);Ball.Model.Place(p.X,p.Y,Bounds());}
        if(state.Square is {} square){var p=Map(square,40,40);Square.Model.Place(p.X,p.Y,Bounds());}
        foreach(var item in state.Items)
        {var size=item.Kind is FurnitureKind.Yarn or FurnitureKind.BellBall or FurnitureKind.ToyMouse?(40d,40d):RoomWindow.Size(item.Kind);var p=Map(new(item.X,item.Y),size.Item1,size.Item2);AddRoomItem(item with{X=p.X,Y=p.Y});}
    }
    public RoomState CaptureRoom()=>new(){WorkArea=Bounds(),Ball=new(Ball.Model.X,Ball.Model.Y),Square=new(Square.Model.X,Square.Model.Y),Items=Furniture.Select(f=>f.Item).Concat(ExtraToys.Select(t=>t.RoomItem! with{X=t.Model.X,Y=t.Model.Y})).ToList()};
    public void RemapWorkspace(BodyBounds from,BodyBounds to)
    {
        CancelRoute();
        var p=RoomCoordinates.Rehome(new(body.X,body.Y),from,to,116,144);body.Place(p.X,p.Y,to);petGravity.VX=petGravity.VY=0;
        foreach(var toy in AllToys){p=RoomCoordinates.Rehome(new(toy.Model.X,toy.Model.Y),from,to,40,40);toy.Model.Place(p.X,p.Y,to);toy.Step(0);}
        foreach(var furniture in Furniture){p=RoomCoordinates.Rehome(new(furniture.Item.X,furniture.Item.Y),from,to,furniture.Width,furniture.Height);furniture.Relocate(p.X,p.Y);}
        Art.Update(to,[],body.Action,clock.Elapsed.TotalSeconds,body.X,body.Y);ApplyPosition();
    }
    public void SetRoomEditing(bool editing)
    {EditingRoom=editing;foreach(var f in Furniture)f.SetEditing(editing);}
    public void AddRoomItem(RoomItem item)
    {
        if(Furniture.Count+ExtraToys.Count>=24)return;
        if(item.Kind is FurnitureKind.Yarn or FurnitureKind.BellBall or FurnitureKind.ToyMouse)
        {
            var toy=new ToyWindow(true,Bounds()){RoomItem=item,Title="毛線球"};toy.Model.Place(item.X,item.Y,Bounds());
            toy.Rang+=strength=>SoundRequested?.Invoke(PetSound.Bell,strength);
            toy.SetToyAppearance(item.Kind);toy.Played+=()=>{playTarget=toy;InteractionRequested?.Invoke(BodyAction.PlayToy);RoomChanged?.Invoke();};
            toy.RemoveRequested+=()=>{ExtraToys.Remove(toy);toy.Close();RoomChanged?.Invoke();};ExtraToys.Add(toy);
        }
        else
        {
            var window=new RoomWindow(item);window.SetEditing(EditingRoom);window.Changed+=()=>RoomChanged?.Invoke();
            window.Removed+=f=>{if(teaserTarget==f){teaserTarget=null;teaserPawTarget=null;}Furniture.Remove(f);f.Close();RoomChanged?.Invoke();};Furniture.Add(window);
        }
    }
    public async Task SmokeGravity()
    {
        var bounds=Bounds();var start=bounds.Top+50;
        body.Place(bounds.Left+bounds.Width-160,start,bounds);petGravity.VX=petGravity.VY=0;
        await Task.Delay(500);
        if(body.Y<start+50)throw new Exception("Pet window did not fall under gravity.");
        ResetPosition();
    }
    private IReadOnlyList<RoomPlatform> RoomPlatforms()=>Furniture.SelectMany(f=>f.Platforms).ToArray();
    private bool PlayTeaser(double dt, double now, double speed)
    {
        if(teaserTarget?.Teaser is not {} pendulum)return false;
        var platform=teaserTarget.Platforms[1];
        // Approach from the lower shelf, on the right of the string anchor.
        // The upper shelf is above the string; sitting there cannot reach this ball.
        var x=teaserTarget.Left+25;
        var y=platform.Y-DesktopBody.Height;
        MovePetToward(x,y,dt,Bounds(),speed);
        var ball=teaserTarget.TeaserPosition;
        facing=-1;
        if(Math.Abs(body.X-x)>10 || Math.Abs(body.Y-y)>2 || !petGravity.Grounded)
        {poseAction=BodyAction.Walk;lastTeaserTap=Math.Max(lastTeaserTap,now-.9);return true;}
        poseAction=BodyAction.ObserveCursor;
        if(feline.IsTurning){lastTeaserTap=Math.Max(lastTeaserTap,now-.9);return true;}
        var local=new Point(ball.X-body.X,ball.Y-body.Y);
        var shoulder=new Point(40,114);
        if(local.X<6||local.X>110||local.Y<72||local.Y>140||(local-shoulder).Length>58)
        {lastTeaserTap=Math.Max(lastTeaserTap,now-.9);return true;}
        // A visible reach precedes impact. The same point drives paw drawing and contact.
        var since=now-lastTeaserTap;
        if(since>=.9)
        {
            poseAction=BodyAction.BatToy;teaserPawTarget=local;
            if(since>=1.1)
            {pendulum.Bat(-125);lastTeaserTap=now;TeaserContactCount++;}
        }
        return true;
    }
    private void MovePetToward(double x,double y,double dt,BodyBounds bounds,double speed)
    {
        Navigate(x,y,dt,bounds,speed);
    }
    private void StepPetGravity(double dt)
    {
        if(drag.Active)return;
        petGravity.X=body.X;petGravity.Y=body.Y;
        petGravity.Step(dt,Bounds(),DesktopBody.Width,DesktopBody.Height,0,RoomPlatforms());
        body.Place(petGravity.X,petGravity.Y,Bounds());
        if(!petGravity.Grounded)poseAction=BodyAction.Fall;
        else
        {
            if(poseAction==BodyAction.Fall)poseAction=null;
            if(body.Action==BodyAction.Fall)body.Action=BodyAction.Idle;
        }
        foreach(var toy in AllToys)ToyContactPhysics.Separate(toy.Model,body.X,body.Y,Bounds());
    }
    private ObjectApproach ToyApproach(ToyWindow toy)
    {
        var bounds=Bounds();var tx=toy.Model.X+20;var petCenter=body.X+58;
        var left=tx-68-58;var right=tx+68-58;
        var px=petCenter<tx?left:right;
        if(px<bounds.Left)px=right;if(px>bounds.Left+bounds.Width-116)px=left;
        px=Math.Clamp(px,bounds.Left,bounds.Left+Math.Max(0,bounds.Width-116));
        var direction=tx-(px+58);
        return new(px,Math.Clamp(toy.Model.Y+40-144,bounds.Top,bounds.Top+Math.Max(0,bounds.Height-144)),Math.Sign(direction),-.35);
    }
    private bool TouchingToy(ToyWindow toy)
    {return Math.Abs(body.X+58-(toy.Model.X+20))<80 && Math.Abs(body.Y+126-(toy.Model.Y+20))<36;}
}
