using System.Text.Json;
using System.Text.Json.Serialization;
namespace DesktopLife.Core;
public enum MilestoneKind { BoxSleep, ScratcherUse, FurnitureSleep, FamiliarFurniture, AutonomousNuzzle, TransitionHabit }
public sealed class MilestoneMemory
{
    public List<MilestoneKind> Seen {get;set;}=[];
    public long LastMinute {get;set;}
    public string? LastReason {get;set;}
    public void Sanitize()
    {
        Seen=(Seen??[]).Where(k=>Enum.IsDefined(k)).Distinct().Take(6).ToList();
        if(LastMinute is <0 or >4223371680)LastMinute=0;
        if(LastReason?.Length>120)LastReason=null;
    }
    // Call only from a confirmed use/completion event. Selection itself is never a memory.
    public bool Record(MilestoneKind kind,CompanionState companion,DateTimeOffset now)
    {
        if(!Enum.IsDefined(kind)||Seen.Contains(kind))return false;
        var minute=Math.Max(0,now.ToUnixTimeSeconds()/60);
        if(LastMinute>0&&minute-LastMinute<360)return false;
        var text=kind switch
        {
            MilestoneKind.BoxSleep=>"第一次記錄到牠在紙箱裡睡著了。",
            MilestoneKind.ScratcherUse=>"第一次記錄到牠用前爪抓抓貓抓板。",
            MilestoneKind.FurnitureSleep=>"第一次記錄到牠自己挑了一件家具睡覺。",
            MilestoneKind.FamiliarFurniture=>"牠已經多次使用同一件家具，慢慢熟悉它了。",
            MilestoneKind.AutonomousNuzzle=>"牠主動在你附近蹭了蹭，享受熟悉的陪伴。",
            _=>"牠反覆完成相同的活動銜接，漸漸形成了一個小習慣。"
        };
        Seen.Add(kind);LastMinute=minute;LastReason=kind.ToString();companion.Remember(now,text,"milestone:"+kind);return true;
    }
}
public sealed class MilestoneMemoryConverter:JsonConverter<MilestoneMemory>
{
    public override bool HandleNull=>true;
    public override MilestoneMemory Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {using var doc=JsonDocument.ParseValue(ref reader);try{var value=doc.RootElement.Deserialize<MilestoneMemory>(options)??new();value.Sanitize();return value;}catch(JsonException){return new();}}
    public override void Write(Utf8JsonWriter writer,MilestoneMemory value,JsonSerializerOptions options)=>JsonSerializer.Serialize(writer,value,options);
}
public sealed record FurnitureUsed(RoomItem Item,FurnitureUse Use,bool Novel,double Familiarity,bool Autonomous);
