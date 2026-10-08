using DesktopLife.Core;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public class GirlDoodleTests
{
    [Fact] public void TemplatesAreBoundedAndDeterministicAcrossSeeds()
    {foreach(var template in Enum.GetValues<DoodleTemplate>())for(var seed=0;seed<80;seed++){var d=GirlDoodleGenerator.Generate(template,seed,.9);d.Validate();Assert.InRange(d.Strokes.Length,1,20);Assert.True(d.Strokes.Sum(s=>s.Length)<=650);Assert.Equal(JsonSerializer.Serialize(d),JsonSerializer.Serialize(GirlDoodleGenerator.Generate(template,seed,.9)));}}
    [Fact] public void TemplateSelectionCoversAllAndSuppressesRecentRepeat()
    {
        var seen=new HashSet<DoodleTemplate>();var repeated=0;var same=0;
        for(var i=0;i<1000;i++){var p=BehaviorParameters.Default with{Seed=i};var initial=GirlDoodleGenerator.Select(p,new());seen.Add(initial);var memory=new BehaviorVariationState();memory.Remember(new(BodyAction.DrawDoodle,"doodle:"+initial,null,DateTimeOffset.UtcNow));if(GirlDoodleGenerator.Select(p,memory)==initial)repeated++;if(initial==DoodleTemplate.Heart)same++;}
        Assert.Equal(16,seen.Count);Assert.InRange(repeated,0,500);Assert.InRange(same,20,120);
    }
    [Fact] public void LegacyDrawingAndPatternRoundTripWithoutRegeneration()
    {var old=new CreativeWork(DoodlePattern.Heart,null,0,0,9,DateTimeOffset.UtcNow,true,ProceduralDrawing.Generate(BehaviorParameters.Default));var restored=JsonSerializer.Deserialize<CreativeWork>(JsonSerializer.Serialize(old))!;Assert.Null(restored.DoodleTemplateId);Assert.Equal(JsonSerializer.Serialize(old.Drawing),JsonSerializer.Serialize(restored.Drawing));}
    [Fact] public void SameTemplateVariationIsSmall()
    {var a=GirlDoodleGenerator.Generate(DoodleTemplate.Heart,1);var b=GirlDoodleGenerator.Generate(DoodleTemplate.Heart,2);Assert.Equal(a.Strokes[0].Length,b.Strokes[0].Length);Assert.All(a.Strokes[0].Zip(b.Strokes[0]),v=>Assert.InRange(Math.Abs(v.First.X-v.Second.X)+Math.Abs(v.First.Y-v.Second.Y),0,16));}
}
