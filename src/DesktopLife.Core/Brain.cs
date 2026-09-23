namespace DesktopLife.Core;

public enum BehaviorDrive { Approach, Explore, Avoid, Rest, Create, Play, Interact }
public sealed record PersonalityProfile
{
    public double Curiosity { get; init; } = .6;
    public double Playfulness { get; init; } = .5;
    public double Social { get; init; } = .6;
    public double Creativity { get; init; } = .5;
    public double Mischief { get; init; } = .3;
    public double Independence { get; init; } = .5;
    public double Timidity { get; init; } = .3;
    public double Laziness { get; init; } = .4;
    public void Validate()
    {
        if (new[]{Curiosity,Playfulness,Social,Creativity,Mischief,Independence,Timidity,Laziness}.Any(v=>!double.IsFinite(v)||v<0||v>1))
            throw new InvalidDataException("Personality values must be 0–1.");
    }
}
public sealed record EnvironmentContext(PetState Pet, PersonalityProfile Personality, EnvironmentState? Environment,
    LearningContext LearningContext, bool Fullscreen = false, Affordances? Affordances = null);
public sealed record BrainOutput(IReadOnlyDictionary<BehaviorDrive,double> Scores, double[] Activity, double[] Outputs)
{
    public double Score(BehaviorDrive drive) => Scores.TryGetValue(drive,out var score) && double.IsFinite(score) ? Math.Clamp(score,0,1) : 0;
}
public interface IBrainController { BrainOutput Evaluate(EnvironmentContext context); }
public interface IAnimationController { void Play(BodyAction action); }
public interface IPetAction
{
    BodyAction Id { get; }
    ActionCategory Category { get; }
    double BaseUtility { get; }
    double NeuralBias { get; }
    double LearnedBias { get; }
    double FinalScore { get; }
    bool CanExecute(EnvironmentContext context);
    double EvaluateUtility(EnvironmentContext context);
    void Start(IAnimationController animation);
    void Update(TimeSpan elapsed);
    void Stop();
}

public sealed class UtilityBrain : IBrainController
{
    public BrainOutput Evaluate(EnvironmentContext context)
    {
        var p=context.Pet;
        return new(new Dictionary<BehaviorDrive,double>
        {
            [BehaviorDrive.Rest]=Math.Clamp((100-p.Energy+p.Fatigue)/140,0,1),
            [BehaviorDrive.Explore]=Math.Clamp((p.Curiosity+p.Boredom)/200,0,1),
            [BehaviorDrive.Approach]=p.Loneliness/100,
            [BehaviorDrive.Interact]=context.Personality.Social,
            [BehaviorDrive.Play]=p.Excitement/100,
            [BehaviorDrive.Create]=context.Personality.Creativity,
            [BehaviorDrive.Avoid]=context.Personality.Timidity
        },[],[]);
    }
}

public sealed class PetAction(BodyAction id) : IPetAction
{
    public BodyAction Id => id;
    public ActionCategory Category => ActionCatalog.Category(Id);
    public double BaseUtility { get; private set; }
    public double NeuralBias { get; private set; }
    public double LearnedBias { get; private set; }
    public double FinalScore => BaseUtility+NeuralBias+LearnedBias;
    public double ElapsedSeconds { get; private set; }
    public bool Running { get; private set; }
    public bool CanExecute(EnvironmentContext context) => Id is not (BodyAction.Eat or BodyAction.Fall or BodyAction.BatToy) && !context.Fullscreen && AffordanceProvider.Allows(Id,context.Affordances??new(false,false,false,false));
    public double EvaluateUtility(EnvironmentContext context)
    {
        var p=context.Pet;
        BaseUtility=Id switch
        {
            BodyAction.Groom=>.18+context.Personality.Independence*.15,
            BodyAction.Nuzzle or BodyAction.Greet=>.08+p.Loneliness/100*.35,
            BodyAction.Sleep=>.10+(100-p.Energy+p.Fatigue)/200*.65,
            BodyAction.Sit=>.20+p.Fatigue/100*.3+context.Personality.Laziness*.1,
            BodyAction.Walk=>.15+p.Energy/100*.3,
            BodyAction.Wander=>.15+(p.Curiosity+p.Boredom)/200*.3+context.Personality.Curiosity*.1,
            BodyAction.Explore or BodyAction.ObserveDesktopIcon or BodyAction.ObserveShortcut=>.15+(p.Curiosity+p.Boredom)/200*.3,
            BodyAction.ObserveCursor or BodyAction.ChaseCursor=>.12+p.Loneliness/100*.25+context.Personality.Social*.15+(context.Affordances?.CursorNearby==true?.20:0),
            BodyAction.AvoidCursor=>.08+context.Personality.Timidity*.2,
            BodyAction.PseudoPushIcon or BodyAction.Hide or BodyAction.PushShortcut=>.08+context.Personality.Mischief*.3,
            BodyAction.PlayToy=>.1+p.Boredom/100*.2+context.Personality.Playfulness*.2,
            BodyAction.DrawDoodle or BodyAction.WriteNote=>.1+p.Boredom/100*.15+context.Personality.Creativity*.25,
            BodyAction.RestInCorner=>.1+p.Fatigue/100*.4,
            _=>.25
        };
        return BaseUtility;
    }
    public void Score(EnvironmentContext context, BrainOutput brain, RewardLearning learning)
    {
        EvaluateUtility(context);
        NeuralBias=.3*brain.Score(Drive(Id));
        LearnedBias=.35*learning.Bias(Id,context.LearningContext);
    }
    public static BehaviorDrive Drive(BodyAction action) => action switch
    { BodyAction.Sleep or BodyAction.Sit or BodyAction.RestInCorner=>BehaviorDrive.Rest,
        BodyAction.Walk or BodyAction.Wander or BodyAction.Explore or BodyAction.ObserveDesktopIcon or BodyAction.ObserveShortcut=>BehaviorDrive.Explore,
        BodyAction.ChaseCursor=>BehaviorDrive.Approach,BodyAction.AvoidCursor or BodyAction.Hide=>BehaviorDrive.Avoid,
        BodyAction.DrawDoodle or BodyAction.WriteNote=>BehaviorDrive.Create,BodyAction.PlayToy or BodyAction.PseudoPushIcon or BodyAction.PushShortcut=>BehaviorDrive.Play,
        _=>BehaviorDrive.Interact };
    public void Start(IAnimationController animation) { Running=true; ElapsedSeconds=0; animation.Play(Id); }
    public void Update(TimeSpan elapsed) { if(elapsed<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(elapsed)); if(Running)ElapsedSeconds+=elapsed.TotalSeconds; }
    public void Stop() { Running=false; }
}

public sealed record ActionScore(BodyAction Action,double BaseUtility,double NeuralBias,double LearnedBias,double FinalScore,double Probability);
public sealed record ActionDecision(BodyAction? Action,string Reason,IReadOnlyList<ActionScore> Scores);
public sealed class ActionSelection(int seed = 42)
{
    private readonly Random random = new(seed);
    public IReadOnlyList<ActionScore> Evaluate(EnvironmentContext context,BrainOutput brain,RewardLearning learning)
    {
        var actions=Enum.GetValues<BodyAction>().Select(id=>new PetAction(id)).Where(a=>a.CanExecute(context)).ToArray();
        foreach(var a in actions)a.Score(context,brain,learning);
        if(actions.Length==0)return [];
        var maximum=actions.Max(a=>a.FinalScore);
        var weights=actions.Select(a=>Math.Exp((a.FinalScore-maximum)/.25)/(1+.25*learning.State.Variation.RecentOutputHistory.TakeLast(8).Count(e=>e.Action==a.Id && DateTimeOffset.UtcNow-e.CreatedAt<TimeSpan.FromMinutes(5)))).ToArray();var sum=weights.Sum();
        return actions.Select((a,i)=>new ActionScore(a.Id,a.BaseUtility,a.NeuralBias,a.LearnedBias,a.FinalScore,weights[i]/sum)).ToArray();
    }
    public ActionDecision Select(EnvironmentContext context,BrainOutput brain,RewardLearning learning,BodyAction current,bool hold)
    {
        if(context.Fullscreen)return new(null,"全螢幕安全隱藏",[]);
        var scores=Evaluate(context,brain,learning);
        if(context.Pet.Energy<=10 || context.Pet.Fatigue>=95)return new(BodyAction.Sleep,"生理安全：優先休息",scores);
        if(current==BodyAction.Sleep && (context.Pet.Energy<30 || context.Pet.Fatigue>75))return new(BodyAction.Sleep,"恢復中：避免反覆切換",scores);
        if(hold)return new(current,"完成目前動作",scores);
        var sample=random.NextDouble();
        foreach(var score in scores){sample-=score.Probability;if(sample<=0)return new(score.Action,"效用與偏好取樣",scores);}
        return new(BodyAction.Idle,"安全備援",scores);
    }
}
