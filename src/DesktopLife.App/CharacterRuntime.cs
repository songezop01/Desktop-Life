using DesktopLife.Core;
namespace DesktopLife.App;

// The existing main-window runtime owns the migrated character. This runtime owns
// one additional character; all residents use the same production selector and world.
public sealed class CharacterRuntime
{
    public PetWindow Window {get;}
    public PetAppearance Kind {get;}
    public HomeostasisSession Life {get;}
    public RewardLearning Learning {get;}
    public PersonalityProfile Personality {get;}
    public bool Paused {get;private set;}
    private readonly ActionSelection selector=new(Random.Shared.Next());
    private readonly UtilityBrain brain=new();
    private PetAction? action;
    private double clock,careUntil;
    internal double CareHoldRemainingSeconds=>Math.Max(0,careUntil-clock);
    internal bool HasActiveDecision=>action is {Running:true};
    public CharacterRuntime(CharacterProfile profile,PetWindow world,double spawnOffset=-180)
    {
        profile.Validate();Kind=profile.Kind;Personality=profile.Personality;
        Life=new(profile.Pet.State,profile.Pet.TotalRuntimeSeconds);
        Life.ApplyCompanionOffline(DateTimeOffset.UtcNow-profile.Pet.LastSaveTime);
        Learning=new(profile.Learning);Window=new(world);Window.SetAppearance(Kind);
        Window.Title=profile.Learning.Companion.Name;
        Window.AttachHabits(Learning.State.LocationHabits);
        var bounds=DisplayWorkspace.Bounds;
        var x=Math.Clamp(world.Position.X+spawnOffset,bounds.Left,bounds.Left+bounds.Width-Window.Width);
        if(Math.Abs(x-world.Position.X)<90)x=Math.Clamp(world.Position.X-spawnOffset,bounds.Left,bounds.Left+bounds.Width-Window.Width);
        Window.RestorePosition(profile.Position??new(x,world.Position.Y));
        Window.ParameterFactory=a=>BehaviorDecoder.Decode(a,Context(null),brain.Evaluate(Context(null)),Learning.State,Learning.LastReward,clock,Random.Shared.Next());
        Window.CreativeAction+=(a,x,y)=>Window.HasCreativeOutput=Window.Art.Create(a,Window.Parameters,Learning.State.Variation,x,y,Kind);
        Window.BehaviorCompleted+=completed=>{
            Learning.State.Transitions.Complete(completed.Behavior,DateTimeOffset.UtcNow,completed.Successful);
            if(completed.Successful&&completed.Autonomous&&completed.NearUser&&completed.Behavior==LifeBehavior.Social&&Learning.State.Companion.Bond>=65)
                Learning.State.Milestones.Record(MilestoneKind.AutonomousNuzzle,Learning.State.Companion,DateTimeOffset.UtcNow);
        };
        Window.FurnitureUseCompleted+=use=>{
            var now=DateTimeOffset.UtcNow;
            if(use.Novel)Learning.State.Adaptation.Observe(AdaptiveTrait.Curiosity,now);
            if(use.Use==FurnitureUse.Sleep)Learning.State.Milestones.Record(use.Item.Kind==FurnitureKind.Box?MilestoneKind.BoxSleep:MilestoneKind.FurnitureSleep,Learning.State.Companion,now);
            if(use.Use==FurnitureUse.Scratch)Learning.State.Milestones.Record(MilestoneKind.ScratcherUse,Learning.State.Companion,now);
            if(use.Familiarity>=.8)Learning.State.Milestones.Record(MilestoneKind.FamiliarFurniture,Learning.State.Companion,now);
        };
        Window.InteractionRequested+=a=>{if(Window.RequestAction(a,BehaviorInterruptReason.Stimulus))action=null;};
    }
    private EnvironmentContext Context(EnvironmentState? environment)=>new(Life.State,Learning.State.Adaptation.Effective(Personality),environment,ActionCatalog.Context(environment,DateTimeOffset.UtcNow),Affordances:Window.SenseAffordances(),Appearance:Kind,Bond:Learning.State.Companion.Bond);
    public void SetPaused(bool paused)
    {
        Paused=paused;Window.SetPaused(paused);Learning.Trace.Clear();
        action?.Stop();action=null;
    }
    public void SuspendPresence()
    {
        action?.Stop();action=null;careUntil=0;Learning.Trace.Clear();Window.SuspendPresence();
    }
    public void Tick(double dt,EnvironmentState? environment,bool paused,bool quiet)
    {
        if(Paused!=paused)SetPaused(paused);
        clock+=dt;Life.AdvanceCompanion(TimeSpan.FromSeconds(dt),Window.PhysiologicalAction,environment?.IdleSeconds.Value<300);
        if(!Window.IsVisible)return;
        Window.AllowCursorAttraction=!quiet;
        Window.EmotionalState=Life.State;Window.Bond=Learning.State.Companion.Bond;
        Learning.State.Adaptation.Advance(DateTimeOffset.UtcNow);Window.BehaviorPersonality=Learning.State.Adaptation.Effective(Personality);
        if(paused||Window.Interacting||Window.EditingRoom)return;
        Window.Routine.Advance(dt,Window.PhysiologicalAction);
        if(Life.State.Energy<=10||Life.State.Fatigue>=95){Window.RequestAction(BodyAction.Sleep,BehaviorInterruptReason.CriticalNeed);return;}
        if(clock<careUntil||Window.SequenceCommitted||Window.FinishingMotion)return;
        action?.Update(TimeSpan.FromSeconds(dt));
        if(action is {Running:true,ElapsedSeconds:<20})return;
        var context=Context(environment);
        var chosen=Window.Routine.Opportunity(Life.State,context.Personality,Window.AvailableHomeUses,quiet,false,Learning.State.Transitions,DateTimeOffset.UtcNow,Window.Bond)
            ??selector.Select(context,brain.Evaluate(context),Learning,Window.CurrentAction,false).Action??BodyAction.Idle;
        chosen=CharacterCapability.Resolve(Kind,chosen);
        if(quiet&&chosen is not (BodyAction.Sleep or BodyAction.Sit or BodyAction.Groom or BodyAction.Stretch))chosen=BodyAction.Sit;
        action?.Stop();action=new(chosen);Window.AutonomousIntent=true;action.Start(Window);
    }
    public string Care(CareKind kind)=>Care(kind,out _);
    public string Care(CareKind kind,out bool accepted)
    {
        accepted=false;
        if(Window.Interacting)return "請先把角色放下。";
        var now=DateTimeOffset.UtcNow;var result=CompanionCare.Apply(Life.State,Learning.State.Companion,kind,now,false);
        if(!result.Accepted)return result.Message;
        accepted=true;
        Life.ApplyCare(result.State);Window.Bond=Learning.State.Companion.Bond;Window.AutonomousIntent=false;
        Window.BeginCare(kind,result.Action);careUntil=clock+(kind==CareKind.Rest?90:kind==CareKind.Play?20:8);action=null;
        if(kind==CareKind.Pet)Learning.State.Adaptation.Observe(AdaptiveTrait.Social,now);
        if(kind==CareKind.Play)Learning.State.Adaptation.Observe(AdaptiveTrait.Playfulness,now);
        if(kind==CareKind.Feed)Learning.State.Transitions.Complete(LifeBehavior.Eat,now);
        if(!Learning.State.Companion.Memories.Any(memory=>memory.Kind?.StartsWith("milestone:",StringComparison.Ordinal)!=true&&now-memory.At<TimeSpan.FromMinutes(30)))
            Learning.State.Companion.Remember(now,CharacterCapability.CareDescription(kind,Kind));
        if(Kind==PetAppearance.Girl)Window.Say("謝謝你陪著我。",4);
        return CharacterCapability.CareDescription(kind,Kind);
    }
    public CharacterProfile Capture()=>new(){Kind=Kind,Pet=new(){State=Life.State,LastSaveTime=DateTimeOffset.UtcNow,TotalRuntimeSeconds=Life.TotalRuntimeSeconds},Personality=Personality,Learning=Learning.State,Position=Window.Position};
}
