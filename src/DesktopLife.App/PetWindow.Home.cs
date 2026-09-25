using System.Windows;
using System.Windows.Media;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    public HomeRoutine Routine {get;private set;}=new(Random.Shared.Next());
    public FurnitureUse AvailableHomeUses=>EditingRoom?FurnitureUse.None:Furniture.Aggregate(FurnitureUse.None,(uses,f)=>uses|FurnitureAffordance.For(f.Item.Kind));
    private RoomItem? homeTarget;
    private bool homeUseRecorded;
    public void AttachHabits(List<LocationHabit> habits)=>restPreference.Attach(habits);
    public void ForgetHabits()=>restPreference.Forget();
    private void LearnHome(FurnitureUse use)
    {
        if(homeTarget is not {} item||homeUseRecorded||!petGravity.Grounded)return;
        restPreference.Learn(item.Id,use,DateTimeOffset.UtcNow);homeUseRecorded=true;
    }
    private double homeX,homeFeet;
    private void StartSleepAt(RestSpot? spot)
    {
        restSpot=spot;homeTarget=Furniture.FirstOrDefault(f=>spot?.Id.StartsWith(f.Item.Id.ToString(),StringComparison.Ordinal)==true)?.Item;homeUseRecorded=false;
        sequence=new(SequenceKind.Sleep,BehaviorPersonality,Bond,spot?.Id,spot?.Kind,homeTarget is {} h?restPreference.Familiarity(h.Id):0,movementRandom.Next(3));
    }
    private bool TryBeginHome(SequenceKind kind,FurnitureUse use)
    {
        if(EditingRoom||appearance!=PetAppearance.Cat&&kind==SequenceKind.Box)return false;
        var platforms=RoomPlatforms();var b=Bounds();
        var candidates=Furniture.Select(f=>f.Context).Where(c=>c.Offers(use))
            .SelectMany(c=>c.Platforms.Where(p=>Math.Abs(p.Y-p.EndY)<1).Select((p,i)=>new RestSpot(c.Item.Id+":"+i,c.Item.Kind,p.X+p.Width/2,p.Y)))
            .Where(c=>RoomNavigation.Plan(platforms,b,body.X+58,body.Y+144,c.X,c.Feet) is not null).ToArray();
        var selected=restPreference.ChooseHome(candidates,BehaviorPersonality,Bond,body.X+58,cursor?.X,DateTimeOffset.UtcNow,EmotionalState.Fatigue,movementRandom);
        if(selected is null)return false;
        homeTarget=Furniture.First(f=>selected.Id.StartsWith(f.Item.Id.ToString(),StringComparison.Ordinal)).Item;homeX=selected.X;homeFeet=selected.Feet;
        homeUseRecorded=false;sequence=new(kind,BehaviorPersonality,Bond,homeTarget.Id.ToString(),homeTarget.Kind,restPreference.Familiarity(homeTarget.Id));return true;
    }
    private bool TickHomeSequence(BehaviorSequence s,double dt)
    {
        var exists=homeTarget is {} target&&!EditingRoom&&Furniture.Any(f=>f.Item==target);
        var exit=s.Phase==BehaviorPhase.Exit;
        var x=exit?Math.Clamp(homeX+(homeX<Bounds().Left+Bounds().Width/2?145:-145),Bounds().Left+58,Bounds().Left+Bounds().Width-58):homeX;
        var feet=exit?Bounds().Top+Bounds().Height:homeFeet;
        s.Step(dt,new(TargetExists:exists,Reached:Math.Abs(body.X+58-x)<3&&Math.Abs(body.Y+144-feet)<4,Grounded:petGravity.Grounded,NavigationFailed:MovementPhase==NavigationPhase.Unreachable));
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
        if(s.Phase is BehaviorPhase.Approach or BehaviorPhase.Exit)MovePetToward(x-58,feet-144,dt,Bounds(),Parameters.Movement.Speed*s.Style.ApproachSpeed);
        if(s.Phase==BehaviorPhase.Complete)EndSequence();return true;
    }
    private void UpdateHomeExpression()
    {
        feline.Clip=null;
        if(homeTarget?.Kind==FurnitureKind.Box&&sequence?.Phase is BehaviorPhase.Enter or BehaviorPhase.Settle or BehaviorPhase.Hide or BehaviorPhase.Peek or BehaviorPhase.Rest or BehaviorPhase.LieDown or BehaviorPhase.Curl or BehaviorPhase.Sleep)
            feline.Clip=new RectangleGeometry(new Rect(0,0,116,Math.Clamp(homeTarget.Y+60-body.Y,0,144)));
    }
}
