using System.Diagnostics;
using DesktopLife.Core;

namespace DesktopLife.App;

// Compile the production navigation partial against a deterministic host. Movement,
// waypoint planning and gravity are the real implementations; only WPF is omitted.
public partial class PetWindow
{
    private readonly DesktopBody body=new();
    private readonly GravityBody petGravity=new();
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly BodyBounds testBounds;
    private IReadOnlyList<RoomPlatform> testPlatforms;
    private BodyAction? poseAction {get;set;}
    private BehaviorSequence? sequence {get;set;}
    private RoomPoint? attentionPoint {get;set;}
    private double lastJump {get;set;}
    private double BodyWidth=>body.BodyWidth;
    private double BodyHeight=>body.BodyHeight;
    private double HalfWidth=>BodyWidth/2;
    private bool HouseMotionActive=>false;
    private double NavigationFloor=>Bounds().Top+Bounds().Height;
    private IReadOnlyList<RoomPlatform> RecoveryObstacles(IReadOnlyList<RoomPlatform> platforms)=>platforms;
    private bool TryHouseNavigation(double x,double y,double dt,BodyBounds bounds,double speed)=>false;
    private void ClearHouseRoute(bool preserveRecovery=false) { }

    public PetWindow(BodyBounds bounds,IReadOnlyList<RoomPlatform> platforms,double x,double feet)
    {
        testBounds=bounds;testPlatforms=platforms;
        body.Place(x,feet-DesktopBody.Height,bounds);
        StepGravity(0);
    }

    public bool EditingRoom {get;set;}
    public RoomPoint Position=>new(body.X,body.Y);
    public bool Grounded=>petGravity.Grounded;
    public bool NavigationStepped=>navigationStepped;
    public BodyAction? QueuedAction=>queuedAction;
    private BodyBounds Bounds()=>testBounds;
    private IReadOnlyList<RoomPlatform> RoomPlatforms()=>testPlatforms;
    private void ChooseTarget()=>queuedAction=null;

    public void ReplacePlatforms(IReadOnlyList<RoomPlatform> platforms)=>testPlatforms=platforms;
    public void SuspendNavigationPresence()=>CancelRoute(preserveRecovery:true);
    public void MoveTo(double x,double feet,double elapsed=.016,double speed=85)
    {
        navigationStepped=false;
        Navigate(x,feet-DesktopBody.Height,elapsed,testBounds,speed);
        ObserveNavigationProgress(elapsed);
        StepGravity(elapsed);
    }

    public void WaitWithoutMovement(double elapsed=.1)
    {
        navigationStepped=false;
        ObserveNavigationProgress(elapsed);
        StepGravity(elapsed);
    }

    private void StepGravity(double elapsed)
    {
        petGravity.X=body.X;petGravity.Y=body.Y;
        petGravity.Step(elapsed,testBounds,DesktopBody.Width,DesktopBody.Height,0,testPlatforms);
        body.Place(petGravity.X,petGravity.Y,testBounds);
    }
}
