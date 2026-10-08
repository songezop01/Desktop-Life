using DesktopLife.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
namespace DesktopLife.Tests;
public class PersonalityAdaptationTests
{
    private static readonly DateTimeOffset Epoch=new(2026,1,1,0,0,0,TimeSpan.Zero);
    [Fact] public void ThreeTouchesDoNotChangePersonality()
    {var a=new PersonalityAdaptation();for(var i=0;i<3;i++)a.Observe(AdaptiveTrait.Social,Epoch.AddMinutes(i));Assert.Equal(0,a.Offset(AdaptiveTrait.Social));}
    [Fact] public void BurstInputCannotFarmEvidenceOrOffset()
    {var a=new PersonalityAdaptation();for(var i=0;i<100000;i++)a.Observe(AdaptiveTrait.Playfulness,Epoch);Assert.Equal(1,a.Traits[AdaptiveTrait.Playfulness].Evidence);Assert.Equal(0,a.Offset(AdaptiveTrait.Playfulness));}
    [Fact] public void MultiWeekDriftIsBoundedAndBaseIsUnchanged()
    {
        var basis=new PersonalityProfile{Playfulness=.1};var a=new PersonalityAdaptation();
        for(var i=0;i<35*1440;i++)a.Observe(AdaptiveTrait.Playfulness,Epoch.AddMinutes(i));
        Assert.InRange(a.Offset(AdaptiveTrait.Playfulness),.1,.12);Assert.Equal(.1,basis.Playfulness);Assert.InRange(a.Effective(basis).Playfulness,.2,.22);
        var copy=JsonSerializer.Deserialize<PersonalityAdaptation>(JsonSerializer.Serialize(a))!;Assert.Equal(a.Effective(basis),copy.Effective(basis));
    }
    [Fact] public void OfflineGapOnlyDecaysEvidence()
    {var a=new PersonalityAdaptation();for(var i=0;i<30;i++)a.Observe(AdaptiveTrait.Social,Epoch.AddMinutes(i));var offset=a.Offset(AdaptiveTrait.Social);a.Advance(Epoch.AddDays(30));Assert.Equal(offset,a.Offset(AdaptiveTrait.Social));Assert.True(a.Traits[AdaptiveTrait.Social].Evidence<1);}
    [Theory][InlineData("null")][InlineData("42")][InlineData("{\"Version\":999}")][InlineData("{\"Traits\":{\"Social\":{\"Offset\":999}}}")]
    public void DamagedOptionalStateFallsBack(string json)
    {var node=JsonNode.Parse(JsonSerializer.Serialize(new LearningState()))!;node["Adaptation"]=JsonNode.Parse(json);var state=JsonSerializer.Deserialize<LearningState>(node.ToJsonString())!;state.Validate();Assert.Equal(0,state.Adaptation.Offset(AdaptiveTrait.Social));}
    [Fact] public void OldV3PreservesAllExistingPayload()
    {
        var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(directory);
        try{var original=new OrganismSnapshot{SchemaVersion=3,Pet=new(){LastSaveTime=Epoch},Personality=new(){Curiosity=.23}};original.Learning.Companion.Name="小橘";original.Learning.Companion.Bond=82;original.Learning.Companion.Remember(Epoch,"真實的舊回憶");original.Learning.Artworks.Add(new(DoodlePattern.Heart,null,3,5,7,Epoch,true));original.Learning.Room.Items.Add(new(Guid.NewGuid(),FurnitureKind.Box,100,200));var store=new OrganismStore(directory);File.WriteAllText(store.PathName,JsonSerializer.Serialize(original));var loaded=store.Load();Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,loaded.SchemaVersion);Assert.Equal(JsonSerializer.Serialize(original.Learning),JsonSerializer.Serialize(loaded.Learning));Assert.Equal(original.Personality,loaded.Personality);}
        finally{Directory.Delete(directory,true);}
    }
}
