namespace DesktopLife.Core;

public enum SequenceKind { Play, Sleep, Stroke, SelfGroom, Brush }
public enum BehaviorPhase { Notice, Orient, Watch, Search, Approach, Inspect, Prepare, Crouch, Paw, Contact, Recover, Evaluate, Sit, LieDown, Curl, Sleep, Wake, Stretch, Hesitate, Accept, React, LickPaw, WashFace, GroomBody, Pause, Relax, Lean, Complete }
public enum BehaviorInterruptReason { Opportunity, Stimulus, Care, CriticalNeed, TargetLost, Safety }
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
    public BehaviorSequence(SequenceKind kind,PersonalityProfile personality,double bond,string? targetId=null)
    {
        if(!Enum.IsDefined(kind)||!double.IsFinite(bond))throw new ArgumentOutOfRangeException(nameof(kind));
        Kind=kind;this.bond=Math.Clamp(bond,0,100);Style=SequenceStyle.From(personality,bond);TargetId=targetId;
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
        if(reason==BehaviorInterruptReason.Stimulus&&(Kind==SequenceKind.Sleep||Kind is SequenceKind.Stroke or SequenceKind.Brush||Age<3))return false;
        InterruptedBy=reason;ending=true;Set(Kind==SequenceKind.Sleep&&Phase==BehaviorPhase.Sleep?BehaviorPhase.Wake:BehaviorPhase.Recover);return true;
    }
    public void UseFallback(){TargetId=null;Set(BehaviorPhase.Inspect);}
    public void Step(double dt,SequenceContext c)
    {
        if(!double.IsFinite(dt)||dt<0)throw new ArgumentOutOfRangeException(nameof(dt));
        if(Finished)return;dt=Math.Min(dt,.1);Age+=dt;PhaseAge+=dt;
        if(!ending&&TargetId is not null&&(!c.TargetExists||c.NavigationFailed))
        {if(Kind==SequenceKind.Sleep){UseFallback();}else Interrupt(c.TargetExists?BehaviorInterruptReason.Safety:BehaviorInterruptReason.TargetLost);return;}
        if(Kind==SequenceKind.Play)stillTime=c.TargetSpeed<4?stillTime+dt:Math.Max(0,stillTime-dt*2);
        if(Phase==BehaviorPhase.Recover){if(c.Grounded&&PhaseAge>=.65)Set(ending?BehaviorPhase.Complete:BehaviorPhase.Evaluate);return;}
        if(Phase==BehaviorPhase.Approach&&PhaseAge>12)
        {if(Kind==SequenceKind.Sleep)UseFallback();else Interrupt(BehaviorInterruptReason.Safety);return;}
        switch(Kind)
        {
            case SequenceKind.Play:
                switch(Phase)
                {
                    case BehaviorPhase.Notice:After(Style.Reaction,BehaviorPhase.Orient);break;
                    case BehaviorPhase.Orient:After(.4,BehaviorPhase.Watch);break;
                    case BehaviorPhase.Watch:After(Style.Watch,BehaviorPhase.Approach);break;
                    case BehaviorPhase.Approach:if(c.Reached&&c.Grounded)Set(BehaviorPhase.Prepare);break;
                    case BehaviorPhase.Prepare:After(.3,BehaviorPhase.Crouch);break;
                    case BehaviorPhase.Crouch:After(.3,BehaviorPhase.Paw);break;
                    case BehaviorPhase.Paw:After(.25,BehaviorPhase.Contact);break;
                    case BehaviorPhase.Contact:
                        if(c.Contact){Contacts++;Set(BehaviorPhase.Recover);}else if(PhaseAge>.45)Set(BehaviorPhase.Recover);break;
                    case BehaviorPhase.Evaluate:
                        if(PhaseAge>=.8)Set(Age>=Style.Interest||stillTime>7||Contacts>=3?BehaviorPhase.Complete:BehaviorPhase.Watch);break;
                }break;
            case SequenceKind.Sleep:
                switch(Phase)
                {
                    case BehaviorPhase.Search:After(.4,TargetId is null?BehaviorPhase.Inspect:BehaviorPhase.Approach);break;
                    case BehaviorPhase.Approach:if(c.Reached&&c.Grounded)Set(BehaviorPhase.Inspect);break;
                    case BehaviorPhase.Inspect:After(.8,BehaviorPhase.Sit);break;
                    case BehaviorPhase.Sit:After(.7,BehaviorPhase.LieDown);break;
                    case BehaviorPhase.LieDown:After(.9,BehaviorPhase.Curl);break;
                    case BehaviorPhase.Curl:After(.8,BehaviorPhase.Sleep);break;
                    case BehaviorPhase.Sleep:if(c.Rested&&PhaseAge>=Style.SleepDuration)Set(BehaviorPhase.Wake);break;
                    case BehaviorPhase.Wake:After(.9,BehaviorPhase.Stretch);break;
                    case BehaviorPhase.Stretch:After(6,BehaviorPhase.Complete);break;
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
                    case BehaviorPhase.Inspect:After(.6,BehaviorPhase.LickPaw);break;
                    case BehaviorPhase.LickPaw:After(1.3,BehaviorPhase.WashFace);break;
                    case BehaviorPhase.WashFace:After(1.8,BehaviorPhase.GroomBody);break;
                    case BehaviorPhase.GroomBody:After(1.4,BehaviorPhase.Pause);break;
                    case BehaviorPhase.Pause:if(PhaseAge>.8){if(Age<9)Set(BehaviorPhase.LickPaw);else{ending=true;Set(BehaviorPhase.Recover);}}break;
                }break;
        }
    }
    private void After(double seconds,BehaviorPhase next){if(PhaseAge>=seconds)Set(next);}
    private void Set(BehaviorPhase next){Phase=next;PhaseAge=0;}
}

public sealed class ToyInterest
{
    private readonly Dictionary<string,double> cooldown=[];
    public void Step(double dt){foreach(var key in cooldown.Keys.ToArray()){cooldown[key]-=dt;if(cooldown[key]<=0)cooldown.Remove(key);}}
    public double Attraction(string id,double speed)=>cooldown.ContainsKey(id)?0:Math.Clamp(.15+Math.Abs(speed)/400,.15,1);
    public void Finish(string id){if(cooldown.Count>=32)cooldown.Remove(cooldown.Keys.First());cooldown[id]=25;}
}
