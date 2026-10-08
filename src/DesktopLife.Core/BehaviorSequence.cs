namespace DesktopLife.Core;

public enum SequenceKind { Play, Sleep, Stroke, SelfGroom, Brush, Box, Scratch, Observe, Activity }
public enum BehaviorPhase { Notice, Orient, Watch, Search, Approach, Inspect, Prepare, Crouch, Paw, Contact, Recover, Evaluate, Sit, LieDown, Curl, Sleep, Wake, Stretch, Hesitate, Accept, React, LickPaw, WashFace, GroomBody, Pause, Relax, Lean, Complete, Enter, Settle, Hide, Peek, Exit, Rest, Scratch, Knead, WatchBall, Open, Work, Check, Close, Stand, SitUp, LickMouth }
public enum BehaviorInterruptReason { Opportunity, Stimulus, Care, CriticalNeed, TargetLost, Safety }
public enum PlayApproachUpdate { Unchanged, Planned, Replanned, WatchingEscapingTarget }
public enum PlayApproachStoppedReason { UnavailableSupport, EscapingTarget }
public readonly record struct SequenceContext(bool TargetExists=true,bool Reached=false,bool Contact=false,bool NavigationFailed=false,bool Grounded=true,bool Rested=false,double TargetSpeed=0)
{public SequenceContext():this(true,false,false,false,true,false,0){}}
public sealed record SequenceStyle(double Reaction,double Watch,double ApproachSpeed,double Interest,double SleepDuration,double StrokeDuration,double Distance,double PurrChance)
{
    public static SequenceStyle From(PersonalityProfile p,double bond)
    {
        p.Validate();bond=Math.Clamp(bond,0,100);
        return new(.25+.4*p.Timidity,.7+.8*p.Curiosity,1-.2*p.Timidity,8+6*p.Playfulness,35+20*p.Laziness,
            bond<25?2:bond<65?3.5:5,8+18*p.Timidity, bond<25?.15:bond<65?.55:.9);
    }
}

/// <summary>Runtime-only choreography. Physics and contact remain authoritative.</summary>
public sealed class BehaviorSequence
{
    public SequenceKind Kind {get;}
    public BehaviorPhase Phase {get;private set;}
    public string? TargetId {get;private set;}
    public SequenceStyle Style {get;}
    public double Age {get;private set;}
    public double PhaseAge {get;private set;}
    public BehaviorInterruptReason? InterruptedBy {get;private set;}
    public bool Finished=>Phase==BehaviorPhase.Complete;
    public bool Committed=>!Finished;
    public int Contacts {get;private set;}
    private bool ending;
    private readonly double bond;
    private double stillTime;
    private readonly int wakeVariant;
    public PetAppearance Character {get;}
    public RoomActivity? Activity {get;}
    public FurnitureKind? LocationKind {get;}
    public double InspectionDuration {get;}
    public double ApproachBudgetSeconds {get;private set;}
    public bool ApproachTimedOut {get;private set;}
    public int PlayTargetRevisions {get;private set;}
    public PlayApproachStoppedReason? PlayApproachStopped {get;private set;}
    public const int MaximumPlayTargetRevisions=4;
    private RoomPoint? playApproachTarget;
    private int playApproachFloor,playApproachRevisions;
    private double lastPlayRevisionAge,playPursuitLimit,playApproachSupportFeet;
    public BehaviorSequence(SequenceKind kind,PersonalityProfile personality,double bond,string? targetId=null,FurnitureKind? locationKind=null,double familiarity=0,int wakeVariant=0,PetAppearance character=PetAppearance.Cat,RoomActivity? activity=null,double approachBudgetSeconds=12)
    {
        if(!Enum.IsDefined(kind)||!double.IsFinite(bond))throw new ArgumentOutOfRangeException(nameof(kind));
        if(!double.IsFinite(approachBudgetSeconds)||approachBudgetSeconds<12||approachBudgetSeconds>600)throw new ArgumentOutOfRangeException(nameof(approachBudgetSeconds));
        ApproachBudgetSeconds=approachBudgetSeconds;
        if(!Enum.IsDefined(character))throw new ArgumentOutOfRangeException(nameof(character));
        Character=character;
        if((kind is SequenceKind.Scratch or SequenceKind.Box)&&character!=PetAppearance.Cat)throw new ArgumentException("This sequence requires a cat.",nameof(kind));
        if(kind==SequenceKind.Activity&&(activity is null||!RoomActivityPolicy.Allowed(character,activity.Value)))throw new ArgumentException("Invalid character activity.",nameof(activity));
        Activity=activity;
        this.wakeVariant=Math.Clamp(wakeVariant,0,2);LocationKind=locationKind;InspectionDuration=.45+(.75+personality.Timidity*.6)*(1-Math.Clamp(familiarity,0,1));
        Kind=kind;this.bond=Math.Clamp(bond,0,100);Style=SequenceStyle.From(personality,bond);
        if(character!=PetAppearance.Cat)Style=Style with{PurrChance=0};
        TargetId=targetId;
        Phase=kind==SequenceKind.Sleep?BehaviorPhase.Search:kind==SequenceKind.SelfGroom?BehaviorPhase.Inspect:BehaviorPhase.Notice;
    }
    public bool Interrupt(BehaviorInterruptReason reason)
    {
        if(Finished)return true;
        if(ending)
        {
            if(reason==BehaviorInterruptReason.Opportunity||InterruptedBy is {} previous&&reason<previous)return false;
            InterruptedBy=reason;return true;
        }
        if(reason==BehaviorInterruptReason.Opportunity)return false;
        if(reason==BehaviorInterruptReason.Stimulus&&(Kind==SequenceKind.Sleep||Kind is SequenceKind.Stroke or SequenceKind.Brush or SequenceKind.Box or SequenceKind.Scratch or SequenceKind.Observe||Age<3))return false;
        InterruptedBy=reason;ending=true;Set(Kind==SequenceKind.Sleep&&Phase==BehaviorPhase.Sleep?BehaviorPhase.Wake:BehaviorPhase.Recover);return true;
    }
    public void UseFallback(){TargetId=null;Set(BehaviorPhase.Inspect);}
    public bool NeedsPlayTargetRevision(RoomPoint target,int floor,double supportFeet)
    {
        if(Kind!=SequenceKind.Play||Phase!=BehaviorPhase.Approach||ending)return false;
        return playApproachTarget is null||PhaseAge-lastPlayRevisionAge>=1&&
            (floor!=playApproachFloor||Math.Abs(target.X-playApproachTarget.X)>35||
                Math.Abs(target.Y-playApproachTarget.Y)>12&&Math.Abs(supportFeet-playApproachSupportFeet)>12);
    }
    public PlayApproachUpdate RevisePlayApproach(RoomPoint target,int floor,double supportFeet,double remainingBudgetSeconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        if(!double.IsFinite(target.X)||!double.IsFinite(target.Y)||floor<0||!double.IsFinite(supportFeet)||!double.IsFinite(remainingBudgetSeconds)||remainingBudgetSeconds<12||remainingBudgetSeconds>600)
            throw new ArgumentOutOfRangeException(nameof(target));
        if(!NeedsPlayTargetRevision(target,floor,supportFeet))return PlayApproachUpdate.Unchanged;
        if(playApproachTarget is null)
        {
            playPursuitLimit=Math.Min(600,remainingBudgetSeconds*2+12);
            ApproachBudgetSeconds=remainingBudgetSeconds;
            playApproachTarget=target;playApproachFloor=floor;playApproachSupportFeet=supportFeet;lastPlayRevisionAge=PhaseAge;
            return PlayApproachUpdate.Planned;
        }
        // Keep elapsed pursuit time. Only a materially moved target gets a new
        // physical remaining-route estimate, and repeated escapes end in watching.
        if(playApproachRevisions>=MaximumPlayTargetRevisions||PhaseAge+remainingBudgetSeconds>playPursuitLimit)
        {PlayApproachStopped=PlayApproachStoppedReason.EscapingTarget;Set(BehaviorPhase.WatchBall);return PlayApproachUpdate.WatchingEscapingTarget;}
        playApproachTarget=target;playApproachFloor=floor;playApproachSupportFeet=supportFeet;lastPlayRevisionAge=PhaseAge;
        playApproachRevisions++;PlayTargetRevisions++;
        ApproachBudgetSeconds=Math.Max(ApproachBudgetSeconds,Math.Min(playPursuitLimit,PhaseAge+remainingBudgetSeconds));
        return PlayApproachUpdate.Replanned;
    }
    public bool WatchUnavailableToy()
    {
        if(Kind!=SequenceKind.Play||Phase!=BehaviorPhase.Approach||ending)return false;
        PlayApproachStopped=PlayApproachStoppedReason.UnavailableSupport;Set(BehaviorPhase.WatchBall);return true;
    }
    public void Step(double dt,SequenceContext c)
    {
        if(!double.IsFinite(dt)||dt<0)throw new ArgumentOutOfRangeException(nameof(dt));
        if(Finished)return;dt=Math.Min(dt,.1);Age+=dt;PhaseAge+=dt;
        if(!ending&&TargetId is not null&&(!c.TargetExists||c.NavigationFailed))
        {if(Kind==SequenceKind.Sleep){UseFallback();}else Interrupt(c.TargetExists?BehaviorInterruptReason.Safety:BehaviorInterruptReason.TargetLost);return;}
        if(Kind==SequenceKind.Play)stillTime=c.TargetSpeed<4?stillTime+dt:Math.Max(0,stillTime-dt*2);
        if(Phase==BehaviorPhase.Recover){if(c.Grounded&&PhaseAge>=.65)Set(ending?BehaviorPhase.Complete:BehaviorPhase.Evaluate);return;}
        if(Phase==BehaviorPhase.Approach&&PhaseAge>ApproachBudgetSeconds)
        {ApproachTimedOut=true;if(Kind==SequenceKind.Sleep)UseFallback();else Interrupt(BehaviorInterruptReason.Safety);return;}
        switch(Kind)
        {
            case SequenceKind.Activity:
                switch(Phase)
                {
                    case BehaviorPhase.Notice:After(.4,BehaviorPhase.Approach);break;
                    case BehaviorPhase.Approach:if(c.Reached&&c.Grounded)Set(BehaviorPhase.Inspect);break;
                    case BehaviorPhase.Inspect:After(.7,Character!=PetAppearance.Girl?BehaviorPhase.Open:BehaviorPhase.Sit);break;
                    case BehaviorPhase.Sit:After(.6,BehaviorPhase.Open);break;
                    case BehaviorPhase.Open:After(.8,BehaviorPhase.Work);break;
                    case BehaviorPhase.Work:After(Activity==RoomActivity.Rest?12:Activity==RoomActivity.Lego?9:6,BehaviorPhase.Pause);break;
                    case BehaviorPhase.Pause:After(.8,Character!=PetAppearance.Girl&&Activity==RoomActivity.Feed?BehaviorPhase.LickMouth:BehaviorPhase.Check);break;
                    case BehaviorPhase.LickMouth:After(.9,BehaviorPhase.Close);break;
                    case BehaviorPhase.Check:After(1.2,BehaviorPhase.Close);break;
                    case BehaviorPhase.Close:After(.7,BehaviorPhase.Stand);break;
                    case BehaviorPhase.Stand:After(.7,BehaviorPhase.Complete);break;
                }break;
            case SequenceKind.Play:
                switch(Phase)
                {
                    case BehaviorPhase.Notice:After(Style.Reaction,BehaviorPhase.Orient);break;
                    case BehaviorPhase.Orient:After(.4,BehaviorPhase.Watch);break;
                    case BehaviorPhase.Watch:After(Style.Watch,BehaviorPhase.Approach);break;
                    case BehaviorPhase.Approach:if(c.Reached&&c.Grounded)Set(BehaviorPhase.Prepare);break;
                    case BehaviorPhase.Prepare:After(.3,Character==PetAppearance.Cat?BehaviorPhase.Crouch:BehaviorPhase.Paw);break;
                    case BehaviorPhase.Crouch:After(.3,BehaviorPhase.Paw);break;
                    case BehaviorPhase.Paw:After(.25,BehaviorPhase.Contact);break;
                    case BehaviorPhase.Contact:
                        if(c.Contact){Contacts++;Set(BehaviorPhase.Recover);}else if(PhaseAge>.45)Set(BehaviorPhase.Recover);break;
                    case BehaviorPhase.Evaluate:
                        if(PhaseAge>=.8)Set(Age>=Style.Interest||stillTime>7||Contacts>=3?BehaviorPhase.WatchBall:BehaviorPhase.Watch);break;
                    case BehaviorPhase.WatchBall:After(4+Style.Watch,BehaviorPhase.Complete);break;
                }break;
            case SequenceKind.Sleep:
                switch(Phase)
                {
                    case BehaviorPhase.Search:After(.4,TargetId is null?BehaviorPhase.Inspect:BehaviorPhase.Approach);break;
                    case BehaviorPhase.Approach:if(c.Reached&&c.Grounded)Set(BehaviorPhase.Inspect);break;
                    case BehaviorPhase.Inspect:if(c.Grounded)After(InspectionDuration,Character==PetAppearance.Cat&&LocationKind==FurnitureKind.PetBed?BehaviorPhase.Settle:BehaviorPhase.Sit);break;
                    case BehaviorPhase.Settle:After(.9,BehaviorPhase.Knead);break;
                    case BehaviorPhase.Knead:After(1.8,BehaviorPhase.Sit);break;
                    case BehaviorPhase.Sit:After(.7,BehaviorPhase.LieDown);break;
                    case BehaviorPhase.LieDown:After(.9,Character==PetAppearance.Cat?BehaviorPhase.Curl:BehaviorPhase.Sleep);break;
                    case BehaviorPhase.Curl:After(.8,BehaviorPhase.Sleep);break;
                    case BehaviorPhase.Sleep:if(c.Rested&&PhaseAge>=Style.SleepDuration)Set(BehaviorPhase.Wake);break;
                    case BehaviorPhase.Wake:After(.9,Character==PetAppearance.Girl?BehaviorPhase.SitUp:BehaviorPhase.Stretch);break;
                    case BehaviorPhase.SitUp:After(.8,BehaviorPhase.Stretch);break;
                    case BehaviorPhase.Stretch:After(6,Character==PetAppearance.Girl?BehaviorPhase.Stand:ending||wakeVariant==0?BehaviorPhase.Complete:Character==PetAppearance.Cat&&wakeVariant==1?BehaviorPhase.LickPaw:BehaviorPhase.Watch);break;
                    case BehaviorPhase.Stand:After(.7,BehaviorPhase.Complete);break;
                    case BehaviorPhase.LickPaw:After(1.3,BehaviorPhase.WashFace);break;
                    case BehaviorPhase.WashFace:After(1.8,BehaviorPhase.Complete);break;
                    case BehaviorPhase.Watch:After(2.5,BehaviorPhase.Complete);break;
                }break;
            case SequenceKind.Observe:
                switch(Phase)
                {
                    case BehaviorPhase.Notice:After(Style.Reaction,BehaviorPhase.Approach);break;
                    case BehaviorPhase.Approach:if(c.Reached&&c.Grounded)Set(BehaviorPhase.Inspect);break;
                    case BehaviorPhase.Inspect:After(InspectionDuration,BehaviorPhase.Watch);break;
                    case BehaviorPhase.Watch:After(7+Style.Watch,BehaviorPhase.Complete);break;
                }break;
            case SequenceKind.Scratch:
                switch(Phase)
                {
                    case BehaviorPhase.Notice:After(Style.Reaction,BehaviorPhase.Approach);break;
                    case BehaviorPhase.Approach:if(c.Reached&&c.Grounded)Set(BehaviorPhase.Inspect);break;
                    case BehaviorPhase.Inspect:After(InspectionDuration,BehaviorPhase.Prepare);break;
                    case BehaviorPhase.Prepare:After(.5,BehaviorPhase.Scratch);break;
                    case BehaviorPhase.Scratch:if(!c.Grounded){Interrupt(BehaviorInterruptReason.Safety);break;}After(2.5,BehaviorPhase.Stretch);break;
                    case BehaviorPhase.Stretch:After(2,BehaviorPhase.Pause);break;
                    case BehaviorPhase.Pause:if(PhaseAge>1){if(Contacts++==0)Set(BehaviorPhase.Scratch);else{ending=true;Set(BehaviorPhase.Recover);}}break;
                }break;
            case SequenceKind.Box:
                switch(Phase)
                {
                    case BehaviorPhase.Notice:After(Style.Reaction,BehaviorPhase.Approach);break;
                    case BehaviorPhase.Approach:if(c.Reached&&c.Grounded)Set(BehaviorPhase.Inspect);break;
                    case BehaviorPhase.Inspect:After(InspectionDuration,BehaviorPhase.Enter);break;
                    case BehaviorPhase.Enter:After(.8,BehaviorPhase.Settle);break;
                    case BehaviorPhase.Settle:After(.8,BehaviorPhase.Hide);break;
                    case BehaviorPhase.Hide:After(3,BehaviorPhase.Peek);break;
                    case BehaviorPhase.Peek:After(2,BehaviorPhase.Rest);break;
                    case BehaviorPhase.Rest:After(5,BehaviorPhase.Exit);break;
                    case BehaviorPhase.Exit:if(c.Reached&&c.Grounded||PhaseAge>8){ending=true;Set(BehaviorPhase.Recover);}break;
                }break;
            case SequenceKind.Stroke:
            case SequenceKind.Brush:
                switch(Phase)
                {
                    case BehaviorPhase.Notice:After(Style.Reaction,Kind==SequenceKind.Stroke&&bond<25?BehaviorPhase.Hesitate:BehaviorPhase.Accept);break;
                    case BehaviorPhase.Hesitate:After(.65,BehaviorPhase.Accept);break;
                    case BehaviorPhase.Accept:After(.4,Kind==SequenceKind.Brush?BehaviorPhase.Relax:BehaviorPhase.React);break;
                    case BehaviorPhase.Relax:After(.8,BehaviorPhase.Lean);break;
                    case BehaviorPhase.Lean:case BehaviorPhase.React:if(PhaseAge>=Style.StrokeDuration){ending=true;Set(BehaviorPhase.Recover);}break;
                }break;
            case SequenceKind.SelfGroom:
                switch(Phase)
                {
                    case BehaviorPhase.Inspect:After(.6,Character==PetAppearance.Cat?BehaviorPhase.LickPaw:BehaviorPhase.GroomBody);break;
                    case BehaviorPhase.LickPaw:After(1.3,BehaviorPhase.WashFace);break;
                    case BehaviorPhase.WashFace:After(1.8,BehaviorPhase.GroomBody);break;
                    case BehaviorPhase.GroomBody:After(1.4,BehaviorPhase.Pause);break;
                    case BehaviorPhase.Pause:if(PhaseAge>.8){if(Age<9)Set(Character==PetAppearance.Cat?BehaviorPhase.LickPaw:BehaviorPhase.GroomBody);else{ending=true;Set(BehaviorPhase.Recover);}}break;
                }break;
        }
    }
    private void After(double seconds,BehaviorPhase next){if(PhaseAge>=seconds)Set(next);}
    private void Set(BehaviorPhase next)
    {
        Phase=next;PhaseAge=0;
        if(Kind==SequenceKind.Play&&next==BehaviorPhase.Approach)
        {playApproachTarget=null;playApproachRevisions=0;lastPlayRevisionAge=0;}
    }
}

public sealed class ToyInterest
{
    private readonly Dictionary<string,double> cooldown=[];
    public void Step(double dt){foreach(var key in cooldown.Keys.ToArray()){cooldown[key]-=dt;if(cooldown[key]<=0)cooldown.Remove(key);}}
    public double Attraction(string id,double speed)=>cooldown.ContainsKey(id)?0:Math.Clamp(.15+Math.Abs(speed)/400,.15,1);
    public void Finish(string id){if(cooldown.Count>=32)cooldown.Remove(cooldown.Keys.First());cooldown[id]=25;}
}
