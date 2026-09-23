using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    public NavigationPhase MovementPhase {get;private set;}
    private readonly Queue<RoomWaypoint> route=[];
    private double navigationElapsed,landingUntil;
    private (double X,double Y)? routeGoal;
    private int roomGeometry;
    private BodyAction? queuedAction;
    public bool FinishingMotion=>MovementPhase is NavigationPhase.Crouching or NavigationPhase.Airborne or NavigationPhase.Landing;
    private void CancelRoute()
    {route.Clear();routeGoal=null;navigationElapsed=0;MovementPhase=NavigationPhase.Idle;}
    private void Navigate(double x,double y,double dt,BodyBounds bounds,double speed)
    {
        var platforms=RoomPlatforms();var hash=new HashCode();foreach(var p in platforms)hash.Add(p);var geometry=hash.ToHashCode();
        x=Math.Clamp(x,bounds.Left,bounds.Left+Math.Max(0,bounds.Width-116));
        var intendedFeet=y+144;
        var support=platforms.Where(p=>x+58>=p.X&&x+58<=p.X+p.Width&&Math.Abs(p.HeightAt(x+58)-intendedFeet)<35).OrderBy(p=>Math.Abs(p.HeightAt(x+58)-intendedFeet)).ToArray();
        y=(support.Length>0?support[0].HeightAt(x+58):bounds.Top+bounds.Height)-144;
        if(geometry!=roomGeometry){CancelRoute();roomGeometry=geometry;}
        if(MovementPhase==NavigationPhase.Landing)
        {poseAction=BodyAction.Sit;if(clock.Elapsed.TotalSeconds<landingUntil)return;MovementPhase=NavigationPhase.Idle;}
        if(MovementPhase==NavigationPhase.Airborne&&route.TryPeek(out var airborne))
        {
            navigationElapsed+=dt;
            if(!petGravity.Grounded)
            {
                if(airborne.Drops)
                {
                    var airX=body.Y+144>airborne.SourceY+8?airborne.LandingX:airborne.TakeoffX;
                    body.MoveToward(airX-58,body.Y,dt,bounds,Math.Min(speed,125));
                }
                if(navigationElapsed>3){CancelRoute();}
                return;
            }
            if(navigationElapsed>.1)
            {
                petGravity.VX=0;
                if(Math.Abs(body.Y+144-airborne.LandingY)<12){route.Dequeue();MovementPhase=NavigationPhase.Landing;landingUntil=clock.Elapsed.TotalSeconds+.22;}
                else CancelRoute();
                return;
            }
        }
        if(!petGravity.Grounded)return;
        if(routeGoal is not {} goal||Math.Abs(goal.X-x)>35||Math.Abs(goal.Y-y)>12)
        {
            route.Clear();routeGoal=(x,y);
            var planned=RoomNavigation.Plan(platforms,bounds,body.X+58,body.Y+144,x+58,y+144);
            if(planned is null)
            {MovementPhase=NavigationPhase.Unreachable;poseAction=BodyAction.ObserveCursor;return;}
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
            body.MoveToward(step.TakeoffX-58,body.Y,dt,bounds,speed);
            // The next gravity step detects leaving the edge; no platform tunnelling.
            if(Math.Abs(body.X+58-step.TakeoffX)<14){MovementPhase=NavigationPhase.Airborne;navigationElapsed=0;}
            return;
        }
        if(Math.Abs(body.X+58-step.TakeoffX)>6)
        {MovementPhase=NavigationPhase.Approaching;body.MoveToward(step.TakeoffX-58,body.Y,dt,bounds,speed);return;}
        if(MovementPhase!=NavigationPhase.Crouching){MovementPhase=NavigationPhase.Crouching;navigationElapsed=0;}
        navigationElapsed+=dt;poseAction=BodyAction.Sit;
        if(navigationElapsed<.22)return;
        var rise=body.Y+144-step.LandingY;
        var velocity=Math.Sqrt(2*GravityBody.Gravity*(Math.Max(0,rise)+25));
        var flight=(velocity+Math.Sqrt(Math.Max(0,velocity*velocity-2*GravityBody.Gravity*rise)))/GravityBody.Gravity;
        petGravity.VY=-velocity;
        petGravity.VX=(step.LandingX-body.X-58)*.18/(1-Math.Exp(-.18*Math.Max(.1,flight)));
        MovementPhase=NavigationPhase.Airborne;navigationElapsed=0;lastJump=clock.Elapsed.TotalSeconds;
    }
}
