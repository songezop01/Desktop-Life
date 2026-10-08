using System.Text.Json;
using System.Text.Json.Serialization;
namespace DesktopLife.Core;
public enum LifeBehavior { Wake, Play, Groom, Explore, Social, Scratch, Rest, Observe, Eat }
public sealed record TransitionHabit(LifeBehavior Previous,LifeBehavior Next,double Weight,int Count,long LastMinute)
{
    [JsonIgnore] public bool Valid=>Enum.IsDefined(Previous)&&Enum.IsDefined(Next)&&Previous!=Next&&double.IsFinite(Weight)&&Weight is >=0 and <=1&&Count is >=1 and <=10000&&LastMinute is >=0 and <=4223371680;
}
public sealed class BehaviorTransitionPreference
{
    public List<TransitionHabit> Pairs {get;set;}=[];
    [JsonIgnore] public LifeBehavior? Previous {get;private set;}
    private DateTimeOffset previousAt;
    private readonly Queue<LifeBehavior> recent=new();
    public void Sanitize(){Pairs=(Pairs??[]).Where(p=>p is {Valid:true}).DistinctBy(p=>(p.Previous,p.Next)).Take(32).ToList();}
    public void Complete(LifeBehavior behavior,DateTimeOffset now,bool successful=true)
    {
        if(!successful||!Enum.IsDefined(behavior)){Previous=null;return;}
        if(Previous is {} previous&&previous!=behavior&&now>=previousAt&&now-previousAt<TimeSpan.FromMinutes(10))
        {
            var minute=Math.Max(0,now.ToUnixTimeSeconds()/60);var old=Pairs.FirstOrDefault(p=>p.Previous==previous&&p.Next==behavior);
            if(old is null||minute>old.LastMinute)
            {
                if(old is not null)Pairs.Remove(old);if(Pairs.Count>=32)Pairs.Remove(Pairs.MinBy(p=>p.LastMinute)!);
                var decayed=old is null?0:old.Weight*Math.Exp(-Math.Max(0,minute-old.LastMinute)/(14*1440d));
                Pairs.Add(new(previous,behavior,Math.Min(1,decayed+.025),Math.Min(10000,(old?.Count??0)+1),minute));
            }
        }
        Previous=behavior;previousAt=now;recent.Enqueue(behavior);while(recent.Count>3)recent.Dequeue();
    }
    public double Bias(BodyAction action,DateTimeOffset now)
    {
        if(Previous is not {} previous||now<previousAt||now-previousAt>TimeSpan.FromMinutes(10))return 0;
        var next=Classify(action);var habit=Pairs.FirstOrDefault(p=>p.Previous==previous&&p.Next==next);
        var value=habit is null?0:habit.Weight*Math.Exp(-Math.Max(0,now.ToUnixTimeSeconds()/60d-habit.LastMinute)/(14*1440d))*.22;
        return value-(recent.Contains(next)&&now-previousAt<TimeSpan.FromMinutes(2)?.16:0);
    }
    public static LifeBehavior Classify(BodyAction action)=>action switch
    {BodyAction.PlayToy or BodyAction.PseudoPushIcon or BodyAction.BatToy=>LifeBehavior.Play,BodyAction.Groom=>LifeBehavior.Groom,BodyAction.Nuzzle or BodyAction.Greet or BodyAction.ChaseCursor=>LifeBehavior.Social,BodyAction.Walk or BodyAction.Wander or BodyAction.Explore or BodyAction.Hide=>LifeBehavior.Explore,BodyAction.Stretch=>LifeBehavior.Scratch,BodyAction.Sleep or BodyAction.Sit or BodyAction.RestInCorner=>LifeBehavior.Rest,BodyAction.Eat=>LifeBehavior.Eat,_=>LifeBehavior.Observe};
}
public sealed class TransitionPreferenceConverter:JsonConverter<BehaviorTransitionPreference>
{
    public override bool HandleNull=>true;
    public override BehaviorTransitionPreference Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {using var doc=JsonDocument.ParseValue(ref reader);try{var state=doc.RootElement.Deserialize<BehaviorTransitionPreference>(options)??new();state.Sanitize();return state;}catch(JsonException){return new();}}
    public override void Write(Utf8JsonWriter writer,BehaviorTransitionPreference value,JsonSerializerOptions options)=>JsonSerializer.Serialize(writer,value,options);
}
public sealed record CompletedBehavior(LifeBehavior Behavior,bool Successful,bool Autonomous,double Seconds,bool NearUser);
