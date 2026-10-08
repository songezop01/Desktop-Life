using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopLife.Core;

namespace DesktopLife.App;

public static class RestartHistoryVerification
{
    public static bool IsPreserved(OrganismSnapshot before,OrganismSnapshot after,DateTimeOffset now)
    {
        // The primary runtime advances personality evidence at startup. Check
        // that exact time transition rather than ignoring adaptation history.
        var minute=after.Learning.Adaptation.LastMinute;
        if(minute<before.Learning.Adaptation.LastMinute||minute>Math.Max(0,now.ToUnixTimeSeconds()/60))return false;
        var expected=History(before);
        var adaptation=JsonSerializer.Deserialize<PersonalityAdaptation>(JsonSerializer.Serialize(before.Learning.Adaptation))!;
        adaptation.Advance(DateTimeOffset.FromUnixTimeSeconds(minute*60));
        expected["Learning"]!["Adaptation"]=JsonSerializer.SerializeToNode(adaptation);
        return JsonNode.DeepEquals(expected,History(after));
    }

    public static JsonNode History(OrganismSnapshot snapshot)
    {
        var node=JsonSerializer.SerializeToNode(snapshot)!;
        void Strip(JsonObject character)
        {
            character.Remove("Pet");character.Remove("Position");character.Remove("PrimaryPosition");
            if(character["Learning"]?["Room"] is JsonObject room)
            {
                room.Remove("Ball");room.Remove("Square");room.Remove("WorkArea");
                if(room["Items"] is JsonArray items)
                    foreach(var item in items.OfType<JsonObject>()){item.Remove("X");item.Remove("Y");}
            }
        }
        Strip(node.AsObject());
        foreach(var key in new[]{"OtherCharacter","AdditionalCharacter"})if(node[key] is JsonObject character)Strip(character);
        return node;
    }
}
