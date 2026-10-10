using System.Windows;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    public List<RoomWindow> Furniture {get;}=[];
    public List<ToyWindow> ExtraToys {get;}=[];
    public IEnumerable<ToyWindow> AllToys=>new[]{Square,Ball}.Concat(ExtraToys);
    public event Action? RoomChanged;
    public event Action<RoomWindow>? FurnitureAdded;
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
        state.Validate();ConfigureHouse(state.FloorCount,false);var from=state.WorkArea??legacyBounds??Bounds();
        RoomPoint Map(RoomPoint point,double w,double h)=>RoomCoordinates.Rehome(point,from,Bounds(),w,h);
        if(state.Ball is {} ball){var p=Map(ball,40,40);Ball.Model.Place(p.X,p.Y,ToyBounds);}
        if(state.Square is {} square){var p=Map(square,40,40);Square.Model.Place(p.X,p.Y,ToyBounds);}
        foreach(var item in state.Items)
        {var size=item.Kind is FurnitureKind.Yarn or FurnitureKind.BellBall or FurnitureKind.ToyMouse?(40d,40d):RoomWindow.Size(item.Kind);var p=Map(new(item.X,item.Y),size.Item1,size.Item2);AddRoomItem(item with{X=p.X,Y=p.Y});}
    }
    public RoomState CaptureRoom()=>new(){WorkArea=Bounds(),FloorCount=House?.FloorCount??1,Ball=new(Ball.Model.X,Ball.Model.Y),Square=new(Square.Model.X,Square.Model.Y),Items=Furniture.Select(f=>f.Item).Concat(ExtraToys.Select(t=>t.RoomItem! with{X=t.Model.X,Y=t.Model.Y,FloorIndex=BaseFloor(t.Model.Y+40)})).ToList()};
    public void RemapWorkspace(BodyBounds from,BodyBounds to)
    {
        CancelDirectCare();
        CancelRoute();sequence?.Interrupt(BehaviorInterruptReason.Safety);
        var p=RoomCoordinates.Rehome(new(body.X,body.Y),from,to,BodyWidth,BodyHeight);body.Place(p.X,p.Y,to);petGravity.VX=petGravity.VY=0;
        if(worldOwner is not null){ApplyPosition();return;}
        foreach(var toy in AllToys){p=RoomCoordinates.Rehome(new(toy.Model.X,toy.Model.Y),from,to,40,40);toy.Model.Place(p.X,p.Y,to);toy.Step(0);}
        foreach(var furniture in Furniture){p=RoomCoordinates.Rehome(new(furniture.Item.X,furniture.Item.Y),from,to,furniture.Width,furniture.Height);furniture.Relocate(p.X,p.Y);}
        Art.Update(to,[],body.Action,clock.Elapsed.TotalSeconds,body.X,body.Y);ApplyPosition();
    }
    public void SetRoomEditing(bool editing)
    {EditingRoom=editing;if(editing){CancelDirectCare();ReleaseFoodReservation();sequence?.Interrupt(BehaviorInterruptReason.Safety);CancelRoute(preserveRecovery:true);queuedAction=null;poseAction=BodyAction.ObserveCursor;if(petGravity.Grounded)petGravity.VX=0;SnapPresentation();}foreach(var f in Furniture)f.SetEditing(editing);}
    public void AddRoomItem(RoomItem item)
    {
        if(Furniture.Count+ExtraToys.Count>=24)return;
        if(item.Kind is FurnitureKind.Yarn or FurnitureKind.BellBall or FurnitureKind.ToyMouse)
        {
            var toy=new ToyWindow(true,ToyBounds){RoomItem=item,RoomBounds=ToyBounds,Title="毛線球"};toy.Model.Place(item.X,item.Y,ToyBounds);
            toy.Rang+=strength=>SoundRequested?.Invoke(PetSound.Bell,strength);
            toy.SetToyAppearance(item.Kind);toy.Played+=()=>{requestedToy=toy;InteractionRequested?.Invoke(BodyAction.PlayToy);RoomChanged?.Invoke();};
            toy.RemoveRequested+=()=>{ExtraToys.Remove(toy);toy.Close();RoomChanged?.Invoke();};ExtraToys.Add(toy);
        }
        else
        {
            var window=new RoomWindow(item);window.SetSceneScale(House?.SceneScale??1);window.SetEditing(EditingRoom);window.Changed+=()=>{if(House is {} h)window.SetFloor(h.Floors.OrderBy(f=>Math.Abs(f.Y-window.Item.Y-window.Height)).First().Index);RoomChanged?.Invoke();};
            window.Removed+=f=>{if(teaserTarget==f){teaserTarget=null;teaserPawTarget=null;}Furniture.Remove(f);restPreference.Prune(Furniture.Select(w=>w.Item.Id));f.Close();RoomChanged?.Invoke();};Furniture.Add(window);FurnitureAdded?.Invoke(window);
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
    private int platformCacheKey;
    private IReadOnlyList<RoomPlatform> platformCache=[];
    public int PlatformRebuilds {get;private set;}
    private IReadOnlyList<RoomPlatform> RoomPlatforms()
    {
        if(worldOwner is not null)return worldOwner.RoomPlatforms();
        var hash=new HashCode();hash.Add(House);foreach(var f in Furniture){hash.Add(f.Item);hash.Add(f.Item.X);hash.Add(f.Item.Y);}var key=hash.ToHashCode();
        if(key!=platformCacheKey){platformCacheKey=key;platformCache=Furniture.SelectMany(f=>f.Platforms).Concat((IEnumerable<RoomPlatform>?)House?.Platforms??Array.Empty<RoomPlatform>()).ToArray();PlatformRebuilds++;}
        return platformCache;
    }
    private bool PlayTeaser(double dt, double now, double speed)
        => TickFreeTeaser(dt, now, speed);
    private void MovePetToward(double x,double y,double dt,BodyBounds bounds,double speed)
    {
        Navigate(x,y,dt,bounds,speed);
    }
    private void StepPetGravity(double dt)
    {
        if(drag.Active)return;
        if(houseControlledThisFrame)return;
        if(houseRoute.TryPeek(out var travel)&&travel.Kind==HouseTravelKind.Stair&&House is {} h)
        {
            var support=h.Stairs.Single(s=>s.ReservationKey==travel.ConnectorId).Support;
            var center=body.X+HalfWidth;
            if(center>=support.X&&center<=support.X+support.Width&&Math.Abs(support.HeightAt(center)-body.Y-BodyHeight)<1)
            {petGravity.PlaceSupported(body.X,body.Y,BodyWidth,BodyHeight,support);return;}
        }
        petGravity.X=body.X;petGravity.Y=body.Y;
        petGravity.Step(dt,Bounds(),BodyWidth,BodyHeight,0,RoomPlatforms());
        body.Place(petGravity.X,petGravity.Y,Bounds());
        if(!petGravity.Grounded)poseAction=BodyAction.Fall;
        else
        {
            if(poseAction==BodyAction.Fall)poseAction=null;
            if(body.Action==BodyAction.Fall)body.Action=BodyAction.Idle;
        }
        foreach(var toy in AllToys)ToyContactPhysics.Separate(toy.Model,body.X,body.Y,Bounds(),BodyWidth,BodyHeight);
    }
    private ObjectApproach ToyApproach(ToyWindow toy)
    {
        TryToyApproach(toy,out var approach);return approach;
    }
    private bool TryToyApproach(ToyWindow toy,out ObjectApproach approach)
    {
        var bounds=Bounds();var ball=toy.Model;
        var first=ToyContactPhysics.Approach(ball.X,ball.Y,body.X,bounds,BodyWidth,BodyHeight);
        var center=ball.X+InteractiveToy.Size/2;
        var other=ToyContactPhysics.Approach(ball.X,ball.Y,first.PushX>0?center:center-BodyWidth,bounds,BodyWidth,BodyHeight);
        var supports=House is null?RoomPlatforms().Append(new(bounds.Left,bounds.Width,bounds.Top+bounds.Height,bounds.Top+bounds.Height)).ToArray():RoomPlatforms();
        var minimumLocalY=appearance==PetAppearance.Cat?72:96;
        approach=first;
        foreach(var stance in new[]{first,other})
        {
            var footX=stance.X+HalfWidth;
            foreach(var platform in supports.Where(p=>footX>=p.X&&footX<=p.X+p.Width&&p.HeightAt(footX)>=bounds.Top+BodyHeight)
                .OrderBy(p=>Math.Abs(p.HeightAt(footX)-ball.Y-InteractiveToy.Size)))
            {
                var feet=platform.HeightAt(footX);
                if(ToyContactPhysics.ReachableContactPoint(ball.X,ball.Y,stance.X,feet-BodyHeight,BodyHeight,minimumLocalY) is null)continue;
                approach=stance with{Y=feet-BodyHeight};return true;
            }
        }
        // A moving ball can pass above the nose or fly between shelves. Track a real
        // support below its projected approach rather than chasing its airborne Y.
        var projected=supports.Where(p=>first.X+HalfWidth>=p.X&&first.X+HalfWidth<=p.X+p.Width&&p.HeightAt(first.X+HalfWidth)>=Math.Max(bounds.Top+BodyHeight,ball.Y+InteractiveToy.Size-GravityBody.PlatformContactTolerance))
            .OrderBy(p=>Math.Abs(p.HeightAt(first.X+HalfWidth)-ball.Y-InteractiveToy.Size)).FirstOrDefault();
        if(projected.Width>0)approach=first with{Y=projected.HeightAt(first.X+HalfWidth)-BodyHeight};
        return !ball.IsResting&&projected.Width>0;
    }
    private bool TouchingToy(ToyWindow toy)
    {var bodyScale=BodyHeight/CharacterGeometry.CanonicalHeight;return Math.Abs(body.X+HalfWidth-(toy.Model.X+20))<80*bodyScale&&ToyContactPhysics.ReachableContactPoint(toy.Model.X,toy.Model.Y,body.X,body.Y,BodyHeight,appearance==PetAppearance.Cat?72:96) is not null;}
}
