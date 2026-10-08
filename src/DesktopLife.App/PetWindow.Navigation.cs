using DesktopLife.Core;
namespace DesktopLife.App;
public sealed record NavigationFailureDetail(string Reason,string Phase,double ElapsedSeconds,RoomPoint Position,double Feet,bool Grounded,double VelocityX,double VelocityY,RoomWaypoint? Waypoint,RoomPoint? Goal,RoomPoint? LaunchPoint,int PlannedGeometry,RoomPlatform[] Supports,RoomPlatform[] Platforms);
public sealed record NavigationRecoveryDetail(string Reason,double ElapsedSeconds,bool Grounded,bool ReachedEscape,bool TimedOut,RoomPoint Position,double Feet,double EscapeCenterX);
public partial class PetWindow
{
    public int PathPlans {get;private set;}
    public int NavigationFailures {get;private set;}
    public int NavigationRecoveriesCompleted {get;private set;}
    public int NavigationRecoveryTimeouts {get;private set;}
    public double NavigationRecoveryCompletionMaxSeconds {get;private set;}
    public Dictionary<string,int> NavigationRecoveryCompletionReasons {get;}=[];
    public Dictionary<string,int> NavigationRecoveryTimeoutReasons {get;}=[];
    public int UnreachableTargets {get;private set;}
    public Dictionary<string,int> NavigationFailureReasons {get;}=[];
    public List<NavigationFailureDetail> NavigationFailureDetails {get;}=[];
    public List<NavigationRecoveryDetail> NavigationRecoveryDetails {get;}=[];
    public NavigationPhase MovementPhase {get;private set;}
    private readonly Queue<RoomWaypoint> route=[];
    private double navigationElapsed,landingUntil;
    private readonly NavigationProgress navigationProgress=new();
    private double recoveryX,recoveryAge;
    private string recoveryReason="no-progress";
    private RoomPoint? navigationLaunchPoint;
    private bool navigationStepped;
    private (double X,double Y)? routeGoal;
    private int roomGeometry;
    private BodyAction? queuedAction;
    public bool FinishingMotion=>HouseMotionActive||MovementPhase is NavigationPhase.Orienting or NavigationPhase.Crouching or NavigationPhase.Airborne or NavigationPhase.Landing or NavigationPhase.Recovering;
    private void CancelRoute(bool preserveRecovery=false)
    {ClearHouseRoute(preserveRecovery);route.Clear();routeGoal=null;navigationElapsed=0;navigationLaunchPoint=null;navigationProgress.Reset();if(!preserveRecovery||MovementPhase!=NavigationPhase.Recovering)MovementPhase=NavigationPhase.Idle;}
    private void RecoverNavigation(string reason="no-progress")
    {
        var platforms=RoomPlatforms();var center=body.X+HalfWidth;var feet=body.Y+BodyHeight;
        if(NavigationFailureDetails.Count==32)NavigationFailureDetails.RemoveAt(0);
        NavigationFailureDetails.Add(new(reason,MovementPhase.ToString(),navigationElapsed,new(body.X,body.Y),feet,petGravity.Grounded,petGravity.VX,petGravity.VY,route.TryPeek(out var waypoint)?waypoint:null,routeGoal is {} goal?new(goal.X+HalfWidth,goal.Y+BodyHeight):null,navigationLaunchPoint,roomGeometry,platforms.Where(p=>center>=p.X&&center<=p.X+p.Width&&Math.Abs(p.HeightAt(center)-feet)<12).ToArray(),platforms.ToArray()));
        NavigationFailures++;route.Clear();routeGoal=null;navigationElapsed=0;navigationProgress.Reset();
        NavigationFailureReasons[reason]=NavigationFailureReasons.GetValueOrDefault(reason)+1;
        recoveryX=RoomNavigation.EscapeX(RecoveryObstacles(platforms),Bounds(),center,feet,HalfWidth)-HalfWidth;
        recoveryAge=0;recoveryReason=reason;MovementPhase=NavigationPhase.Recovering;sequence?.Interrupt(BehaviorInterruptReason.Safety);
    }
    private bool StepNavigationRecovery(double dt)
    {
        if(MovementPhase!=NavigationPhase.Recovering)return false;
        recoveryAge+=dt;poseAction=BodyAction.ObserveCursor;
        if(!petGravity.Grounded)return true;
        petGravity.VX=0;
        if(recoveryAge>.45){poseAction=BodyAction.Walk;body.MoveToward(recoveryX,body.Y,dt,Bounds(),75);}
        var reached=Math.Abs(body.X-recoveryX)<3;
        var center=body.X+HalfWidth;var feet=body.Y+BodyHeight;var bounds=Bounds();
        var grounded=petGravity.Grounded&&(Math.Abs(feet-bounds.Top-bounds.Height)<4||RoomPlatforms().Any(p=>center>=p.X&&center<=p.X+p.Width&&Math.Abs(p.HeightAt(center)-feet)<4));
        if(reached&&grounded||recoveryAge>5)
        {
            var completed=reached&&grounded&&recoveryAge<=5;
            if(completed){NavigationRecoveriesCompleted++;NavigationRecoveryCompletionMaxSeconds=Math.Max(NavigationRecoveryCompletionMaxSeconds,recoveryAge);NavigationRecoveryCompletionReasons[recoveryReason]=NavigationRecoveryCompletionReasons.GetValueOrDefault(recoveryReason)+1;}
            else{NavigationRecoveryTimeouts++;NavigationRecoveryTimeoutReasons[recoveryReason]=NavigationRecoveryTimeoutReasons.GetValueOrDefault(recoveryReason)+1;}
            if(NavigationRecoveryDetails.Count==32)NavigationRecoveryDetails.RemoveAt(0);
            NavigationRecoveryDetails.Add(new(recoveryReason,recoveryAge,grounded,reached,!completed&&recoveryAge>5,new(body.X,body.Y),body.Y+BodyHeight,recoveryX+HalfWidth));
            CancelRoute();if(sequence is {Finished:false})sequence.Interrupt(BehaviorInterruptReason.Safety);else body.Action=BodyAction.Idle;ChooseTarget();
        }
        return true;
    }
    private void Navigate(double x,double y,double dt,BodyBounds bounds,double speed)
    {
        navigationStepped=true;
        if(!EditingRoom&&MovementPhase!=NavigationPhase.Recovering&&TryHouseNavigation(x,y,dt,bounds,speed))return;
        NavigateOnRoom(x,y,dt,bounds,speed);
    }
    private void NavigateOnRoom(double x,double y,double dt,BodyBounds bounds,double speed)
    {
        navigationStepped=true;
        if(EditingRoom){poseAction=BodyAction.ObserveCursor;return;}
        if(StepNavigationRecovery(dt))return;
        var platforms=RoomPlatforms();var hash=new HashCode();foreach(var p in platforms)hash.Add(p);var geometry=hash.ToHashCode();
        x=Math.Clamp(x,bounds.Left,bounds.Left+Math.Max(0,bounds.Width-BodyWidth));
        var intendedFeet=y+BodyHeight;
        var floor=NavigationFloor;
        var support=platforms.Where(p=>x+HalfWidth>=p.X&&x+HalfWidth<=p.X+p.Width&&Math.Abs(p.HeightAt(x+HalfWidth)-intendedFeet)<35).OrderBy(p=>Math.Abs(p.HeightAt(x+HalfWidth)-intendedFeet)).ToArray();
        // Home exits and floor toys request the floor explicitly. A low bed
        // above that point must not silently replace the requested support.
        y=(Math.Abs(intendedFeet-floor)<4?floor:support.Length>0?support[0].HeightAt(x+HalfWidth):floor)-BodyHeight;
        if(geometry!=roomGeometry)
        {
            var inFlight=MovementPhase==NavigationPhase.Airborne||!petGravity.Grounded&&route.Count>0;
            if(inFlight){RecoverNavigation("geometry-changed-in-flight");roomGeometry=geometry;return;}
            CancelRoute();roomGeometry=geometry;
        }
        if(MovementPhase==NavigationPhase.Landing)
        {poseAction=BodyAction.Sit;landingUntil-=dt;if(landingUntil>0)return;MovementPhase=NavigationPhase.Idle;}
        // Gravity loses support as soon as the center crosses the shelf edge,
        // before it reaches the planned exit ten pixels farther away.
        // A nearby overlapping support can lift the feet by the contact tolerance.
        // Walking off that support briefly loses grounding while the planned
        // source platform is still below us. Let gravity settle there before
        // treating the distant source edge as the start of this drop.
        if(!petGravity.Grounded&&MovementPhase!=NavigationPhase.Airborne&&route.TryPeek(out var departing)&&departing.Drops
            &&!platforms.Any(p=>body.X+HalfWidth>=p.X&&body.X+HalfWidth<=p.X+p.Width&&Math.Abs(p.HeightAt(body.X+HalfWidth)-departing.SourceY)<=GravityBody.PlatformContactTolerance))
        {MovementPhase=NavigationPhase.Airborne;navigationElapsed=0;navigationLaunchPoint=new(body.X+HalfWidth,body.Y+BodyHeight);}
        if(MovementPhase==NavigationPhase.Airborne&&route.TryPeek(out var airborne))
        {
            navigationElapsed+=dt;
            if(!petGravity.Grounded)
            {
                if(airborne.Drops)
                {
                    var airX=body.Y+BodyHeight>airborne.SourceY+8?airborne.LandingX:airborne.TakeoffX;
                    body.MoveToward(airX-HalfWidth,body.Y,dt,bounds,Math.Min(speed,125));
                }
                if(navigationElapsed>3){RecoverNavigation("flight-timeout");}
                return;
            }
            if(navigationElapsed>.1)
            {
                petGravity.VX=0;
                if(Math.Abs(body.Y+BodyHeight-airborne.LandingY)<12){route.Dequeue();MovementPhase=NavigationPhase.Landing;landingUntil=.35;navigationProgress.Reset();}
                else RecoverNavigation("unexpected-landing");
                return;
            }
            // Do not re-enter the takeoff branch while waiting for gravity or
            // landing confirmation; doing so resets the flight timer forever.
            return;
        }
        if(!petGravity.Grounded)return;
        if(routeGoal is not {} goal||Math.Abs(goal.X-x)>35||Math.Abs(goal.Y-y)>12)
        {
            route.Clear();routeGoal=(x,y);
            PathPlans++;var planned=RoomNavigation.Plan(platforms,bounds,body.X+HalfWidth,body.Y+BodyHeight,x+HalfWidth,y+BodyHeight,HalfWidth,BodyHeight);
            if(planned is null)
            {UnreachableTargets++;MovementPhase=NavigationPhase.Unreachable;poseAction=BodyAction.ObserveCursor;return;}
            foreach(var waypoint in planned)route.Enqueue(waypoint);
            MovementPhase=NavigationPhase.Approaching;navigationElapsed=0;
        }
        if(!route.TryPeek(out var step))
        {
            if(MovementPhase==NavigationPhase.Unreachable){poseAction=BodyAction.ObserveCursor;return;}
            body.MoveToward(x,body.Y,dt,bounds,speed);
            MovementPhase=Math.Abs(body.X-x)<3?NavigationPhase.Idle:NavigationPhase.Approaching;
            return;
        }
        if(step.Drops)
        {
            body.MoveToward(step.TakeoffX-HalfWidth,body.Y,dt,bounds,speed);
            // The next gravity step detects leaving the edge; no platform tunnelling.
            if(RoomNavigation.ReachedDropExit(step,body.X+HalfWidth)){MovementPhase=NavigationPhase.Airborne;navigationElapsed=0;navigationLaunchPoint=new(body.X+HalfWidth,body.Y+BodyHeight);}
            return;
        }
        if(Math.Abs(body.X+HalfWidth-step.TakeoffX)>.5)
        {MovementPhase=NavigationPhase.Approaching;body.MoveToward(step.TakeoffX-HalfWidth,body.Y,dt,bounds,speed);return;}
        if(MovementPhase is not (NavigationPhase.Orienting or NavigationPhase.Crouching)){MovementPhase=NavigationPhase.Orienting;navigationElapsed=0;}
        if(MovementPhase==NavigationPhase.Orienting)
        {attentionPoint=new(step.LandingX,step.LandingY);poseAction=BodyAction.ObserveCursor;navigationElapsed+=dt;if(navigationElapsed<.3)return;MovementPhase=NavigationPhase.Crouching;navigationElapsed=0;}
        navigationElapsed+=dt;poseAction=BodyAction.Sit;
        if(navigationElapsed<.22)return;
        var rise=body.Y+BodyHeight-step.LandingY;
        var velocity=Math.Sqrt(2*GravityBody.Gravity*(Math.Max(0,rise)+25));
        var flight=(velocity+Math.Sqrt(Math.Max(0,velocity*velocity-2*GravityBody.Gravity*rise)))/GravityBody.Gravity;
        petGravity.VY=-velocity;
        petGravity.VX=(step.LandingX-body.X-HalfWidth)*.18/(1-Math.Exp(-.18*Math.Max(.1,flight)));
        MovementPhase=NavigationPhase.Airborne;navigationElapsed=0;navigationLaunchPoint=new(body.X+HalfWidth,body.Y+BodyHeight);lastJump=clock.Elapsed.TotalSeconds;
    }
}
