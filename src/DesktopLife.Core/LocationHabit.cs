using System.Text.Json;
using System.Text.Json.Serialization;
namespace DesktopLife.Core;

public sealed record LocationHabit(Guid FurnitureId,FurnitureUse Use,double Familiarity,double Preference,int Uses,long LastUsedMinute)
{
    public bool Valid=>FurnitureId!=Guid.Empty&&Enum.IsDefined(Use)&&Use!=FurnitureUse.None&&double.IsFinite(Familiarity)&&Familiarity is >=0 and <=1
        &&double.IsFinite(Preference)&&Preference is >=0 and <=1&&Uses is >=0 and <=10000&&LastUsedMinute is >=0 and <=4223371680;
}
/// <summary>Only optional habit corruption can fall back; malformed enclosing JSON remains fatal.</summary>
public sealed class LocationHabitListConverter:JsonConverter<List<LocationHabit>>
{
    public override List<LocationHabit> Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {
        using var document=JsonDocument.ParseValue(ref reader);var result=new List<LocationHabit>();
        if(document.RootElement.ValueKind!=JsonValueKind.Array)return result;
        foreach(var entry in document.RootElement.EnumerateArray())
        {
            if(result.Count>=32)break;
            try
            {
                var value=entry.Deserialize<LocationHabit>(options);
                if(value is {Valid:true}&&!result.Any(h=>h.FurnitureId==value.FurnitureId&&h.Use==value.Use))result.Add(value);
            }
            catch(JsonException){ }
        }
        return result;
    }
    public override void Write(Utf8JsonWriter writer,List<LocationHabit> value,JsonSerializerOptions options)
    {writer.WriteStartArray();foreach(var habit in value.Take(32))JsonSerializer.Serialize(writer,habit,options);writer.WriteEndArray();}
}
