using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    private HouseLayout? ownedHouse;
    private HouseWindow? houseSurface;
    private readonly CharacterFurnitureForegroundVisual furnitureForeground=new();
    public HouseLayout? House=>worldOwner?.House??ownedHouse;
    private double sceneScale=1;
    private double BodyWidth=>body.BodyWidth;
    private double BodyHeight=>body.BodyHeight;
    private double HalfWidth=>BodyWidth/2;
    private Viewbox? characterViewport;
    private readonly Queue<HouseTravelWaypoint> houseRoute=[];
    private (double X,double Feet)? houseGoal;
    private bool houseControlledThisFrame;
    private string? houseConnector;
    private double LocalFloor=>FloorFeetAt(body.Y+BodyHeight);
    private double FloorFeetAt(double feet)=>House is {} h?h.Floors[BaseFloor(feet)].Y:Bounds().Top+Bounds().Height;
    private double NavigationFloor=>House?.Floors[0].Y??Bounds().Top+Bounds().Height;
    private BodyBounds ToyBounds=>Bounds() with{Height=NavigationFloor-Bounds().Top};
    private bool HouseMotionActive=>houseRoute.Count>0;
    private bool EnteredHouseStair(HouseTravelWaypoint step)=>step.Kind==HouseTravelKind.Stair&&
        (Math.Abs(body.X+HalfWidth-step.From.X)>.000001||Math.Abs(body.Y+BodyHeight-step.From.Y)>.000001);
    public int StairTrips {get;private set;}
    public int HouseRouteFailures {get;private set;}
    public IEnumerable<Window> HouseSurfaces=>worldOwner is null&&houseSurface is not null?[houseSurface]:[];

    private void ApplyCharacterGeometry()
    {
        if(!IsInitialized)return;
        var geometry=CharacterGeometry.For(appearance,sceneScale);
        body.Resize(geometry.Width,geometry.Height,Bounds());Width=geometry.Width;Height=geometry.Height;
        if(characterViewport is null)
        {
            Content=null;Surface.Width=116;Surface.Height=144;
            characterViewport=new Viewbox{Child=Surface,Stretch=Stretch.Fill};Content=characterViewport;
        }
        characterViewport.Width=Width;characterViewport.Height=Height;
        ApplyPosition();
    }
    public void ConfigureHouse(int floors,bool arrangeFurniture)
    {
        if(worldOwner is not null){ApplyHouseGeometry();return;}
        ownedHouse=HouseLayout.Create(floors,Bounds());
        houseSurface??=new HouseWindow();houseSurface.Configure(ownedHouse);
        foreach(var toy in AllToys){toy.RoomBounds=ToyBounds;toy.Step(0);}
        foreach(var f in Furniture)f.SetSceneScale(ownedHouse.SceneScale);
        if(arrangeFurniture)ArrangeHouseFurniture();
        else foreach(var f in Furniture)f.SetFloor(Math.Min(f.Item.FloorIndex,floors-1));
        ApplyHouseGeometry();foreach(var resident in sharedCharacters)resident.ApplyHouseGeometry();
        platformCacheKey=int.MinValue;
    }
    private void ApplyHouseGeometry()
    {
        CancelDirectCare();
        ClearHouseRoute();CancelRoute();Occupancy.Release(appearance);sequence?.Interrupt(BehaviorInterruptReason.Safety);
        var previousHeight=BodyHeight;var feet=body.Y+previousHeight;sceneScale=House?.SceneScale??1;ApplyCharacterGeometry();
        if(House is {} h)
        {
            var floor=h.Floors.OrderBy(f=>Math.Abs(f.Y-feet)).First();
            body.Place(h.SafeFootX(floor.Index,body.X+HalfWidth,HalfWidth)-HalfWidth,floor.Y-BodyHeight,Bounds());
        }
        petGravity.VX=petGravity.VY=0;StepPetGravity(0);ApplyPosition();ChooseTarget();
    }
    public void ArrangeHouseFurniture()
    {
        if(House is not {} h)return;
        var perFloor=Enumerable.Range(0,h.FloorCount).Select(_=>new List<RoomWindow>()).ToArray();
        // Keep work/play and resting corners separated, retain every existing item and identity.
        foreach(var f in Furniture)
        {
            var preferred=f.Item.Kind is FurnitureKind.HumanBed or FurnitureKind.PetBed or FurnitureKind.Sofa? h.FloorCount-1:
                f.Item.Kind is FurnitureKind.Desk or FurnitureKind.Bookshelf or FurnitureKind.Chair or FurnitureKind.Computer or FurnitureKind.DrawingBook?Math.Min(1,h.FloorCount-1):0;
            var least=perFloor.Select((items,index)=>(Count:items.Count,Index:index)).OrderBy(v=>v.Count).First();
            if(perFloor[preferred].Count>least.Count+2)preferred=least.Index;
            perFloor[preferred].Add(f);
        }
        for(var i=0;i<h.FloorCount;i++)
        {
            var list=perFloor[i];var f=h.Floors[i];var margin=24*h.SceneScale;
            var available=Math.Max(1,f.Width-2*margin);
            for(var j=0;j<list.Count;j++)
            {
                var item=list[j];item.SetFloor(i);item.SetSceneScale(h.SceneScale);
                var x=f.Left+margin+(list.Count==1?Math.Max(0,(available-item.Width)/2):j*Math.Max(0,available-item.Width)/Math.Max(1,list.Count-1));
                item.Relocate(x,f.Y-item.Height);
            }
        }
        foreach(var toy in AllToys){var f=h.Floors[0];toy.Model.Place(Math.Clamp(toy.Model.X,f.Left,f.Right-40),f.Y-40,Bounds());}
        platformCacheKey=int.MinValue;
    }
    public bool CanRebuildHouse=>!drag.Active&&petGravity.Grounded&&!HouseMotionActive&&MovementPhase is not (NavigationPhase.Airborne or NavigationPhase.Recovering);
    public bool IsOnStair=>houseRoute.TryPeek(out var step)&&step.Kind==HouseTravelKind.Stair;
    private void ClearHouseRoute(bool preserveRecovery=false)
    {
        if(houseConnector is {} key)Occupancy.Release(key,appearance);
        houseConnector=null;if(!preserveRecovery){houseRoute.Clear();houseGoal=null;}houseControlledThisFrame=false;
    }
    private int BaseFloor(double feet)=>House is {} h?h.Floors.Where(f=>f.Y>=feet-5).OrderBy(f=>f.Y-feet).FirstOrDefault()?.Index??0:0;
    private IReadOnlyList<RoomPlatform> RecoveryObstacles(IReadOnlyList<RoomPlatform> platforms)=>House is {} h?platforms.Where(p=>!h.Platforms.Contains(p)).ToArray():platforms;
    private bool CanReach(double x,double feet)
    {
        if(House is {} h&&BaseFloor(feet)!=BaseFloor(body.Y+BodyHeight))return true;
        return RoomNavigation.Plan(RoomPlatforms(),Bounds(),body.X+HalfWidth,body.Y+BodyHeight,x,feet,HalfWidth,BodyHeight) is not null;
    }
    private double EstimateApproachBudget(double goalCenter,double goalFeet,double speed)
    {
        speed=Math.Max(1,speed);var startX=body.X+HalfWidth;var startFeet=body.Y+BodyHeight;
        var seconds=Math.Abs(goalCenter-startX)/speed+Math.Abs(goalFeet-startFeet)/100+8;
        if(House is {} h)
        {
            var from=BaseFloor(startFeet);var to=BaseFloor(goalFeet);
            if(from!=to)
            {
                // This estimate does not move the body: descent from furniture is still real navigation.
                var start=h.SafeFootX(from,startX,HalfWidth);
                var plan=HouseTraversal.Plan(h,start,h.Floors[from].Y,to,goalCenter,HalfWidth);
                if(plan is not null)
                {
                    var houseSpeed=Math.Max(35,speed)*h.SceneScale;
                    var occupants=worldOwner is {} owner?owner.sharedCharacters.Count+1:sharedCharacters.Count+1;
                    var queue=plan.Where(p=>p.Kind==HouseTravelKind.Stair).Sum(p=>p.Length/(35*h.SceneScale))*Math.Max(0,occupants-1);
                    seconds=plan.Sum(p=>p.Length)/houseSpeed*1.2+queue+12;
                    if(Math.Abs(startFeet-h.Floors[from].Y)>4)seconds+=12;
                    if(Math.Abs(goalFeet-h.Floors[to].Y)>4)seconds+=12;
                }
            }
        }
        return Math.Clamp(seconds,12,600);
    }
    private bool TryHouseNavigation(double x,double y,double dt,BodyBounds bounds,double speed)
    {
        if(House is not {} h||h.FloorCount<2)return false;
        var goalFeet=y+BodyHeight;var currentFeet=body.Y+BodyHeight;
        var goalFloor=BaseFloor(goalFeet);var currentFloor=BaseFloor(currentFeet);
        if(houseRoute.TryPeek(out var active)&&houseGoal is {} previousGoal&&
            (BaseFloor(previousGoal.Feet)!=goalFloor||Math.Abs(previousGoal.X-x)>35||Math.Abs(previousGoal.Feet-goalFeet)>12))
        {
            // A toy can move while it is being pursued. Replan on real floor contact,
            // without finishing the old destination floor or starting more obsolete stairs.
            // Once a stair has been entered it retains its support and reservation until
            // the actual endpoint; cancelling it halfway would leave the character in air.
            if(!EnteredHouseStair(active)&&h.FindFloor(body.X+HalfWidth,currentFeet,h.FloorContactTolerance) is not null)
                ClearHouseRoute();
        }
        if(houseRoute.Count==0&&currentFloor==goalFloor)return false;
        if(houseRoute.Count==0&&(houseGoal is not {} goal||Math.Abs(goal.X-x)>35||Math.Abs(goal.Feet-y-BodyHeight)>12))
        {
            ClearHouseRoute();houseGoal=(x,y+BodyHeight);
            // First leave a furniture support using the existing gravity/drop navigation.
            if(h.FindFloor(body.X+HalfWidth,body.Y+BodyHeight,h.FloorContactTolerance) is null)
            {
                houseGoal=null;
                NavigateOnRoom(x,h.Floors[currentFloor].Y-BodyHeight,dt,Bounds(),speed);return true;
            }
            var planned=HouseTraversal.Plan(h,body.X+HalfWidth,body.Y+BodyHeight,goalFloor,x+HalfWidth,HalfWidth);
            if(planned is null){HouseRouteFailures++;return false;}
            foreach(var step in planned)houseRoute.Enqueue(step);
            route.Clear();navigationProgress.Reset();MovementPhase=NavigationPhase.Approaching;
        }
        return AdvanceHouseTravel(dt,speed);
    }
    private bool StopHousePursuit(double dt,double speed)
    {
        if(houseRoute.TryPeek(out var step)&&EnteredHouseStair(step))
        {
            // A failed toy pursuit can stop walking on a floor immediately. On a stair,
            // keep only the current continuous segment and reach its real floor endpoint.
            // The shared step implementation releases the connector at that endpoint.
            if(houseRoute.Count>1){houseRoute.Clear();houseRoute.Enqueue(step);}
            houseGoal=null;AdvanceHouseTravel(dt,speed);return true;
        }
        ClearHouseRoute();return false;
    }
    private bool AdvanceHouseTravel(double dt,double speed)
    {
        if(House is not {} h)return false;
        if(!houseRoute.TryPeek(out var travel)){houseGoal=null;return false;}
        if(travel.ConnectorId is {} connector)
        {
            if(!Occupancy.TryAcquire(connector,appearance)){poseAction=BodyAction.Sit;MovementPhase=NavigationPhase.Orienting;navigationStepped=false;return true;}
            houseConnector=connector;
        }
        var next=HouseTraversal.Advance(travel,new(body.X+HalfWidth,body.Y+BodyHeight),dt,Math.Max(35,speed)*h.SceneScale);
        if(!next.Supported){HouseRouteFailures++;ClearHouseRoute();RecoverNavigation("house-support-lost");return true;}
        body.Place(next.Feet.X-HalfWidth,next.Feet.Y-BodyHeight,Bounds());
        RememberHousePresentation(travel,next.Reached);
        var support=travel.Kind==HouseTravelKind.Stair?h.Stairs.Single(s=>s.ReservationKey==travel.ConnectorId).Support:h.Floors[travel.SourceFloor].Platform;
        petGravity.PlaceSupported(body.X,body.Y,BodyWidth,BodyHeight,support);
        houseControlledThisFrame=true;navigationStepped=true;poseAction=BodyAction.Walk;MovementPhase=NavigationPhase.Approaching;
        if(next.Reached)
        {
            houseRoute.Dequeue();navigationProgress.Reset();if(travel.Kind==HouseTravelKind.Stair){StairTrips++;if(houseConnector is {} key)Occupancy.Release(key,appearance);houseConnector=null;}
            if(houseRoute.Count==0){houseGoal=null;MovementPhase=NavigationPhase.Idle;}
        }
        return true;
    }
}
