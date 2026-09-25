using System.Text.Json.Serialization;
namespace DesktopLife.Core;

public enum ActionCategory { Basic, Explore, Social, Create, Play, Rest, Mischief }
public enum LearningContext { Unknown, QuietDesktop, ActiveDesktop, UserNearby }
public enum RewardButton { Left, Right, Middle }
public sealed record RewardEvent(double Timestamp, RewardButton Button, double Amount);
public sealed record TraceEntry(double Timestamp, BodyAction Action, ActionCategory Category,
    LearningContext Context, double Activation, double[] NeuralActivity, double[] NeuralOutput, double[]? ConnectomeActivity=null,BehaviorParameters? Parameters=null);
public sealed record EligibilityCredit(TraceEntry Entry, double Credit);

public static class ActionCatalog
{
    public static ActionCategory Category(BodyAction action) => action switch
    {
        BodyAction.Walk or BodyAction.Wander or BodyAction.Explore or BodyAction.ObserveDesktopIcon or BodyAction.ObserveShortcut => ActionCategory.Explore,
        BodyAction.Sit or BodyAction.Sleep or BodyAction.RestInCorner => ActionCategory.Rest,
        BodyAction.ObserveCursor or BodyAction.ChaseCursor or BodyAction.AvoidCursor => ActionCategory.Social,
        BodyAction.DrawDoodle or BodyAction.WriteNote=>ActionCategory.Create,
        BodyAction.PlayToy=>ActionCategory.Play,
        BodyAction.PseudoPushIcon or BodyAction.Hide or BodyAction.PushShortcut=>ActionCategory.Mischief,
        _ => ActionCategory.Basic
    };
    public static LearningContext Context(EnvironmentState? environment, DateTimeOffset now)
    {
        var feed = EnvironmentFeedingEncoder.Encode(environment, now);
        return feed.Level is null ? LearningContext.Unknown : feed.Level > .5 ? LearningContext.ActiveDesktop
            : feed.Presence > .7 ? LearningContext.UserNearby : LearningContext.QuietDesktop;
    }
}

public sealed class EligibilityTrace
{
    private readonly Queue<TraceEntry> entries = new();
    public const double WindowSeconds = 5;
    public void Observe(TraceEntry entry)
    {
        entry.Parameters?.Validate();
        if (!double.IsFinite(entry.Timestamp) || !double.IsFinite(entry.Activation) || entry.Activation < 0 || entry.Activation > 1)
            throw new ArgumentOutOfRangeException(nameof(entry));
        if(entry.NeuralActivity.Length>2048||entry.NeuralOutput.Length>16||entry.ConnectomeActivity?.Length>5000
            ||entry.NeuralActivity.Concat(entry.NeuralOutput).Concat(entry.ConnectomeActivity??[]).Any(v=>!double.IsFinite(v)||v<0||v>1))throw new ArgumentOutOfRangeException(nameof(entry));
        if (entries.Count > 0 && entry.Timestamp < entries.Last().Timestamp) Clear();
        entries.Enqueue(entry with { NeuralActivity = (double[])entry.NeuralActivity.Clone(), NeuralOutput = (double[])entry.NeuralOutput.Clone(), ConnectomeActivity=entry.ConnectomeActivity is {} a?(double[])a.Clone():null });
        Prune(entry.Timestamp);
        while (entries.Count > 24) entries.Dequeue(); // 4 Hz, bounded five-second history
    }
    public IReadOnlyList<EligibilityCredit> Credits(double now)
    {
        Prune(now);
        var samples = entries.Where(e => e.Timestamp <= now)
            .Select(e => new EligibilityCredit(e, .25 * e.Activation * Math.Exp(-(now-e.Timestamp)/1.5))).ToArray();
        var denominator = Math.Max(1, samples.Sum(e => e.Credit));
        return samples.Select(e => e with { Credit = e.Credit / denominator }).ToArray();
    }
    private void Prune(double now) { while (entries.TryPeek(out var e) && now-e.Timestamp > WindowSeconds) entries.Dequeue(); }
    public void Clear() => entries.Clear();
}

public sealed class LearningState
{
    [JsonRequired] public int SchemaVersion { get; set; } = 1;
    [JsonRequired] public Dictionary<BodyAction,double> ActionPreference { get; set; } = new();
    [JsonRequired] public Dictionary<ActionCategory,double> CategoryPreference { get; set; } = new();
    [JsonRequired] public Dictionary<string,double> ContextAssociation { get; set; } = new();
    public double[][]? FlyWeights { get; set; }
    public List<CreativeWork> Artworks { get; set; } = new();
    public string? ConnectomeFingerprint { get; set; }
    public double[]? ConnectomeDeltas { get; set; }
    public long PositiveRewards { get; set; }
    public long Punishments { get; set; }
    [JsonConverter(typeof(LocationHabitListConverter))] public List<LocationHabit> LocationHabits {get;set;}=[];
    public RoomState Room {get;set;}=new();
    public CompanionState Companion {get;set;}=new();
    public BehaviorVariationState Variation {get;set;}=new();
    public void Validate()
    {
        if(Variation is null)throw new InvalidDataException("缺少行為風格狀態。");
        Variation.Validate();
        LocationHabits??=[];var habits=new RestSpotPreference();habits.Attach(LocationHabits);
        if(Companion is null)throw new InvalidDataException("缺少陪伴資料。");
        Companion.Validate();
        if(Room is null)throw new InvalidDataException("缺少房間資料。");Room.Validate();
        if(Artworks is null || Artworks.Count>32 || Artworks.Any(w=>w is null||!Enum.IsDefined(w.Pattern)||!double.IsFinite(w.X)||!double.IsFinite(w.Y)||w.X<0||w.Y<0||w.Text?.Length>100))throw new InvalidDataException("無效的創作資料。");
        foreach(var work in Artworks)work.Drawing?.Validate();
        if(ConnectomeDeltas is not null && (ConnectomeFingerprint is null||ConnectomeDeltas.Length>100000||ConnectomeDeltas.Any(d=>!double.IsFinite(d))))throw new InvalidDataException("無效connectome deltas。");
        // Validate legacy data without constructing an obsolete controller.
        if(FlyWeights is not null && (FlyWeights.Length!=256||FlyWeights.Any(row=>row is null||row.Length!=7||row.Any(v=>!double.IsFinite(v)||Math.Abs(v)>1))))
            throw new InvalidDataException("舊版權重資料格式無效。");
        if (SchemaVersion != 1 || ActionPreference is null || CategoryPreference is null || ContextAssociation is null
            || PositiveRewards < 0 || Punishments < 0 || ContextAssociation.Count > Enum.GetValues<BodyAction>().Length*4
            || ActionPreference.Keys.Any(k => !Enum.IsDefined(k)) || CategoryPreference.Keys.Any(k => !Enum.IsDefined(k))
            || ContextAssociation.Keys.Any(k => !Enum.GetValues<LearningContext>().Any(c => Enum.GetValues<BodyAction>().Any(a => k==$"{c}:{a}")))
            || ActionPreference.Values.Concat(CategoryPreference.Values).Concat(ContextAssociation.Values).Any(v => !double.IsFinite(v) || Math.Abs(v) > .75))
            throw new InvalidDataException("無效的學習存檔或不支援的 schema。");
    }
}

public sealed class RewardLearning
{
    public LearningState State { get; }
    public EligibilityTrace Trace { get; } = new();
    public RewardEvent? LastReward { get; private set; }
    public IReadOnlyList<EligibilityCredit> LastCredits { get; private set; } = [];
    public RewardLearning(LearningState? state = null) { State = state ?? new(); State.Validate(); }
    public IReadOnlyList<EligibilityCredit> Reward(RewardButton button, double now, AppSettings settings)
    {
        if (!double.IsFinite(now) || !Enum.IsDefined(button)) throw new ArgumentOutOfRangeException(nameof(now));
        settings.Validate();
        if (LastReward is { } last && now-last.Timestamp < .15) return []; // reject accidental duplicate events
        var amount = button switch { RewardButton.Left => settings.StrongReward, RewardButton.Right => settings.PositiveReward, _ => settings.Punishment };
        LastReward = new(now,button,amount);
        LastCredits = Trace.Credits(now);
        foreach (var item in LastCredits)
        {
            var e=item.Entry; var change=settings.LearningRate*amount*item.Credit;
            Adjust(State.ActionPreference,e.Action,change);
            Adjust(State.CategoryPreference,e.Category,change*.4);
            if (e.Context != LearningContext.Unknown) Adjust(State.ContextAssociation,$"{e.Context}:{e.Action}",change*.6);
            if(e.Parameters is {} parameters)State.Variation.Reinforce(e.Action,parameters,change);
        }
        if (LastCredits.Count > 0) { if (amount > 0) State.PositiveRewards++; else State.Punishments++; }
        return LastCredits;
    }
    private static void Adjust<T>(Dictionary<T,double> weights,T key,double delta) where T:notnull =>
        weights[key]=Math.Clamp(weights.GetValueOrDefault(key)+delta,-.75,.75);
    public double Bias(BodyAction action, LearningContext context) => State.ActionPreference.GetValueOrDefault(action)
        + State.CategoryPreference.GetValueOrDefault(ActionCatalog.Category(action)) + State.ContextAssociation.GetValueOrDefault($"{context}:{action}");
    public void Decay(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var factor=Math.Exp(-.01*Math.Min(seconds,86400)/86400);
        foreach(var key in State.ActionPreference.Keys) State.ActionPreference[key]*=factor;
        foreach(var key in State.CategoryPreference.Keys) State.CategoryPreference[key]*=factor;
        foreach(var key in State.ContextAssociation.Keys) State.ContextAssociation[key]*=factor;
    }
}
