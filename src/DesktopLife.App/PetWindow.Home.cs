using System.Windows;
using System.Windows.Media;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    public HomeRoutine Routine {get;private set;}=new(Random.Shared.Next());
    public FurnitureUse AvailableHomeUses=>EditingRoom?FurnitureUse.None:Furniture.Aggregate(FurnitureUse.None,(uses,f)=>uses|FurnitureCompatibility.AvailableUses(appearance,f.Item.Kind));
    private RoomItem? homeTarget;
    private bool homeUseRecorded;
    public event Action<FurnitureUsed>? FurnitureUseCompleted;
    public void AttachHabits(List<LocationHabit> habits)=>restPreference.Attach(habits);
    public void ForgetHabits()=>restPreference.Forget();
    private void LearnHome(FurnitureUse use)
    {
        if(homeTarget is not {} item||homeUseRecorded||!petGravity.Grounded)return;
        if(!FurnitureCompatibility.CanUse(appearance,item.Kind,use)){InvalidFurnitureInteractions++;return;}
        var novel=restPreference.Familiarity(item.Id)==0;
        restPreference.Learn(item.Id,use,DateTimeOffset.UtcNow);homeUseRecorded=true;
        FurnitureUseCompleted?.Invoke(new(item,use,novel,restPreference.Familiarity(item.Id),sequenceAutonomous));
    }
    private double homeX,homeFeet;
    private void StartSleepAt(RestSpot? spot)
    {
        restSpot=spot;homeTarget=Furniture.FirstOrDefault(f=>spot?.Id.StartsWith(f.Item.Id.ToString(),StringComparison.Ordinal)==true)?.Item;homeUseRecorded=false;
        if(spot is not null&&!Occupancy.TryAcquire(homeTarget is {} item?FurnitureCompatibility.Reservation(item,appearance):spot.Id,appearance)){homeTarget=null;restSpot=null;spot=null;}
        sequence=new(SequenceKind.Sleep,BehaviorPersonality,Bond,spot?.Id,spot?.Kind,homeTarget is {} h?restPreference.Familiarity(h.Id):0,HomeRoutine.WakeVariant(BehaviorPersonality,movementRandom),appearance,approachBudgetSeconds:spot is null?12:EstimateApproachBudget(spot.X,spot.Feet,Parameters.Movement.Speed*.8));
    }
    private bool TryBeginHome(SequenceKind kind,FurnitureUse use)
    {
        if(EditingRoom||appearance!=PetAppearance.Cat&&kind is SequenceKind.Box or SequenceKind.Scratch)return false;
        var platforms=RoomPlatforms();var b=Bounds();
        var candidates=Furniture.Where(f=>Occupancy.Available(FurnitureCompatibility.Reservation(f.Item,appearance),appearance)).Select(f=>f.Context).Where(c=>c.Offers(use)&&FurnitureCompatibility.CanUse(appearance,c.Item.Kind,use))
            .SelectMany(c=>c.Platforms.Where(p=>Math.Abs(p.Y-p.EndY)<1).Select((p,i)=>new RestSpot(c.Item.Id+":"+i,c.Item.Kind,p.X+p.Width/2,p.Y)))
            .Where(c=>CanReach(c.X,c.Feet)).ToArray();
        var selected=restPreference.ChooseHome(candidates,BehaviorPersonality,Bond,body.X+HalfWidth,cursor?.X,DateTimeOffset.UtcNow,EmotionalState.Fatigue,movementRandom);
        if(selected is null)return false;
        homeTarget=Furniture.First(f=>selected.Id.StartsWith(f.Item.Id.ToString(),StringComparison.Ordinal)).Item;homeX=selected.X;homeFeet=selected.Feet;
        if(!Occupancy.TryAcquire(FurnitureCompatibility.Reservation(homeTarget,appearance),appearance))return false;
        homeUseRecorded=false;sequence=new(kind,BehaviorPersonality,Bond,homeTarget.Id.ToString(),homeTarget.Kind,restPreference.Familiarity(homeTarget.Id),character:appearance,approachBudgetSeconds:EstimateApproachBudget(homeX,homeFeet,Parameters.Movement.Speed*SequenceStyle.From(BehaviorPersonality,Bond).ApproachSpeed));return true;
    }
    private bool TickHomeSequence(BehaviorSequence s,double dt)
    {
        var exists=homeTarget is {} target&&!EditingRoom&&Furniture.Any(f=>f.Item==target);
        var exit=s.Phase==BehaviorPhase.Exit;
        var x=exit?Math.Clamp(homeX+(homeX<Bounds().Left+Bounds().Width/2?145:-145),Bounds().Left+HalfWidth,Bounds().Left+Bounds().Width-HalfWidth):homeX;
        var feet=exit?FloorFeetAt(homeFeet):homeFeet;
        s.Step(dt,new(TargetExists:exists,Reached:Math.Abs(body.X+HalfWidth-x)<3&&Math.Abs(body.Y+BodyHeight-feet)<4,Grounded:petGravity.Grounded,NavigationFailed:MovementPhase==NavigationPhase.Unreachable));
        poseAction=s.Phase switch
        {
            BehaviorPhase.Scratch or BehaviorPhase.Stretch=>appearance==PetAppearance.Cat?BodyAction.Stretch:BodyAction.Sit,
            BehaviorPhase.Approach or BehaviorPhase.Exit=>BodyAction.Walk,
            BehaviorPhase.Enter or BehaviorPhase.Hide or BehaviorPhase.Rest=>appearance==PetAppearance.Cat?BodyAction.Sleep:BodyAction.Sit,
            BehaviorPhase.Settle or BehaviorPhase.Peek=>BodyAction.Sit,
            _=>BodyAction.ObserveCursor
        };
        if(s.Phase is BehaviorPhase.Rest or BehaviorPhase.Scratch)LearnHome(s.Kind==SequenceKind.Scratch?FurnitureUse.Scratch:FurnitureUse.Hide);
        sequencePoseAge=s.PhaseAge;attentionPoint=s.Kind==SequenceKind.Observe&&cursor is {} point?new(point.X,point.Y):new(homeX,homeFeet-65);
        if(s.Kind==SequenceKind.Observe&&s.Phase==BehaviorPhase.Watch)LearnHome(FurnitureUse.Observe);
        if(s.Phase is BehaviorPhase.Approach or BehaviorPhase.Exit)MovePetToward(x-HalfWidth,feet-BodyHeight,dt,Bounds(),Parameters.Movement.Speed*s.Style.ApproachSpeed);
        if(s.Phase==BehaviorPhase.Complete)EndSequence();return true;
    }
    private void UpdateHomeExpression()
    {
        feline.Clip=null;
        if(homeTarget?.Kind==FurnitureKind.Box&&sequence?.Phase is BehaviorPhase.Enter or BehaviorPhase.Settle or BehaviorPhase.Hide or BehaviorPhase.Peek or BehaviorPhase.Rest or BehaviorPhase.LieDown or BehaviorPhase.Curl or BehaviorPhase.Sleep)
        {boxClip.Rect=new Rect(0,0,116,Math.Clamp((homeTarget.Y+60*sceneScale-body.Y)/(BodyHeight/144),0,144));feline.Clip=boxClip;}
    }
    private readonly RectangleGeometry boxClip=new();
}
