using DesktopLife.Core;
namespace DesktopLife.App;

public partial class PetWindow
{
    private string PresenceDiagnostic()=>$"{appearance}: position={body.X:F2},{body.Y:F2}; action={body.Action}; phase={sequence?.Phase}; navigation={MovementPhase}; grounded={petGravity.Grounded}; paused={paused}; visible={IsVisible}; interacting={Interacting}; editing={EditingRoom}; target={target.X:F2},{target.Y:F2}; speed={Parameters.Movement.Speed:F2}; pauseFrequency={Parameters.Movement.PauseFrequency:F2}; failures={NavigationFailures}";

    // Native animation and the production NoticeOther/navigation remain active,
    // without a MainWindow brain, learned profile, furniture or cursor stimuli.
    internal async Task SmokeAvoidOverlap(PetWindow other)
    {
        if(worldOwner is not null||other.worldOwner!=this||Furniture.Count!=0)throw new Exception("Presence smoke requires a fresh empty shared world");
        var parameters=BehaviorParameters.Default;
        parameters=parameters with{Seed=701,Movement=parameters.Movement with{Speed=80,PauseFrequency=0,DirectionChange=0,ObjectInterest=0,PathCurvature=0}};
        parameters.Validate();ParameterFactory=_=>parameters;other.ParameterFactory=_=>parameters;
        SetAppearance(PetAppearance.Cat);other.SetAppearance(PetAppearance.Girl);
        AllowCursorAttraction=false;other.AllowCursorAttraction=false;SetPaused(false);other.SetPaused(false);
        ShowActivated=false;other.ShowActivated=false;Show();other.Show();
        var bounds=Bounds();var floor=NavigationFloor;
        Ball.Model.Place(bounds.Left+bounds.Width-160,floor-40,bounds);Square.Model.Place(bounds.Left+bounds.Width-80,floor-40,bounds);
        ResetPosition();other.ResetPosition();SetAction(BodyAction.Sit);other.SetAction(BodyAction.Sit);
        body.Place(bounds.Left+300,floor-BodyHeight,bounds);other.body.Place(bounds.Left+320,floor-other.BodyHeight,bounds);
        StepPetGravity(.02);other.StepPetGravity(.02);ApplyPosition();other.ApplyPosition();
        var start=body.X;var otherStart=other.body.X;
        NoticeOther(other);
        if(body.Action!=BodyAction.Walk||target.X>=start)
            throw new Exception("Overlap notice did not request avoidance: "+PresenceDiagnostic()+" | "+other.PresenceDiagnostic());
        await Task.Delay(2000);
        if(Math.Abs(body.X-other.body.X)<30||start-body.X<30||Math.Abs(other.body.X-otherStart)>.1)
            throw new Exception("Characters did not avoid overlap within 2 s: "+PresenceDiagnostic()+" | "+other.PresenceDiagnostic());
    }
}
