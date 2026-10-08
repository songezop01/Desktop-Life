using System.Text.Json;
using System.Text.Json.Serialization;
namespace DesktopLife.Core;
public enum AdaptiveTrait { Curiosity, Playfulness, Social, Independence }
public sealed class TraitEvidence
{
    public double Offset {get;set;}
    public double Evidence {get;set;}
    public long LastEventMinute {get;set;}
    [JsonIgnore] public bool Valid=>double.IsFinite(Offset)&&Math.Abs(Offset)<=.12&&double.IsFinite(Evidence)&&Evidence is >=0 and <=40&&LastEventMinute is >=0 and <=4223371680;
}
public sealed class PersonalityAdaptation
{
    public int Version {get;set;}=1;
    public long LastMinute {get;set;}
    public Dictionary<AdaptiveTrait,TraitEvidence> Traits {get;set;}=[];
    public void Sanitize()
    {
        if(Version!=1||LastMinute is <0 or >4223371680){Version=1;LastMinute=0;Traits=[];return;}
        Traits??=[];foreach(var key in Traits.Keys.ToArray())if(!Enum.IsDefined(key)||Traits[key] is not {Valid:true})Traits.Remove(key);
    }
    public void Advance(DateTimeOffset now)
    {
        var minute=Math.Max(0,now.ToUnixTimeSeconds()/60);if(LastMinute==0){LastMinute=minute;return;}
        var elapsed=minute-LastMinute;if(elapsed<=0)return;LastMinute=minute;
        foreach(var t in Traits.Values)
        {
            t.Evidence*=Math.Exp(-elapsed/(7*1440d));
            // Offline gaps decay evidence but never manufacture days of active companionship.
            var days=elapsed<=5?elapsed/1440d:0;
            var rate=Math.Clamp((t.Evidence-8)/16,0,1)*.006;
            t.Offset=Math.Clamp(t.Offset+days*rate,-.12,.12);
        }
    }
    public void Observe(AdaptiveTrait trait,DateTimeOffset now)
    {
        if(!Enum.IsDefined(trait))return;Advance(now);var minute=Math.Max(0,now.ToUnixTimeSeconds()/60);
        if(!Traits.TryGetValue(trait,out var t)){t=new();Traits.Add(trait,t);}
        if(minute<=t.LastEventMinute)return;
        t.Evidence=Math.Min(40,t.Evidence+1);t.LastEventMinute=minute;
    }
    public double Offset(AdaptiveTrait trait)=>Traits.TryGetValue(trait,out var t)?t.Offset:0;
    public PersonalityProfile Effective(PersonalityProfile basis)=>basis with
    {Curiosity=Math.Clamp(basis.Curiosity+Offset(AdaptiveTrait.Curiosity),0,1),Playfulness=Math.Clamp(basis.Playfulness+Offset(AdaptiveTrait.Playfulness),0,1),Social=Math.Clamp(basis.Social+Offset(AdaptiveTrait.Social),0,1),Independence=Math.Clamp(basis.Independence+Offset(AdaptiveTrait.Independence),0,1)};
}
public sealed class PersonalityAdaptationConverter:JsonConverter<PersonalityAdaptation>
{
    public override bool HandleNull=>true;
    public override PersonalityAdaptation Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {
        using var doc=JsonDocument.ParseValue(ref reader);
        try{var value=doc.RootElement.Deserialize<PersonalityAdaptation>(options)??new();value.Sanitize();return value;}
        catch(JsonException){return new();}
    }
    public override void Write(Utf8JsonWriter writer,PersonalityAdaptation value,JsonSerializerOptions options)=>JsonSerializer.Serialize(writer,value,options);
}
