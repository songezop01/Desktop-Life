using DesktopLife.Core;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public class GenerativeBehaviorTests
{
    private static readonly EnvironmentContext Context=new(new(),new(),null,LearningContext.UserNearby);
    private static BrainOutput Brain(double create)=>new(new Dictionary<BehaviorDrive,double>{[BehaviorDrive.Create]=create,[BehaviorDrive.Interact]=.5,[BehaviorDrive.Explore]=.5},[],[]);
    private static BehaviorParameters Decode(int seed,LearningState? state=null)=>BehaviorDecoder.Decode(BodyAction.DrawDoodle,Context,Brain(.5),state??new(),null,0,seed);
    [Fact] public void NeuralOutputChangesHowNotOnlyWhichAction()
    {
        var quiet=BehaviorDecoder.Decode(BodyAction.DrawDoodle,Context,Brain(0),new(),null,0,5);
        var creative=BehaviorDecoder.Decode(BodyAction.DrawDoodle,Context,Brain(1),new(),null,0,5);
        Assert.True(creative.Draw.Complexity>quiet.Draw.Complexity+.3);
        Assert.True(creative.Draw.StrokeCount>quiet.Draw.StrokeCount);
        Assert.NotEqual(JsonSerializer.Serialize(ProceduralDrawing.Generate(quiet)),JsonSerializer.Serialize(ProceduralDrawing.Generate(creative)));
    }
    [Fact] public void DrawingsAreDeterministicVariedAndBounded()
    {
        var outputs=new HashSet<string>();
        for(var seed=0;seed<300;seed++)
        {
            var p=Decode(seed);p.Validate();var drawing=ProceduralDrawing.Generate(p);drawing.Validate();
            var json=JsonSerializer.Serialize(drawing);Assert.Equal(json,JsonSerializer.Serialize(ProceduralDrawing.Generate(p)));outputs.Add(json);
        }
        Assert.Equal(300,outputs.Count);
        foreach(var kind in Enum.GetValues<PrimitiveKind>())
        {var p=Decode(1);ProceduralDrawing.Generate(p with{Draw=p.Draw with{Primary=kind}}).Validate();}
    }
    [Fact] public void ComposableTextHasThousandsOfCombinationsAndRemembersOutputs()
    {
        var memory=new BehaviorVariationState();var outputs=new HashSet<string>();
        for(var seed=0;seed<2000;seed++)
        {
            var p=Decode(seed);p=p with{Write=p.Write with{Length=.85,Emotional=.9,Playful=.8}};
            var note=ComposableText.Generate(p,memory);Assert.InRange(note.Text.Length,1,100);
            Assert.DoesNotContain(memory.RecentOutputHistory,e=>e.Text==note.Text);
            outputs.Add(note.Text);memory.Remember(new(BodyAction.WriteNote,note.Signature,note.Text,DateTimeOffset.UtcNow));
        }
        Assert.True(outputs.Count>1500,$"Only {outputs.Count} unique notes.");Assert.Equal(64,memory.RecentOutputHistory.Count);memory.Validate();
    }
    [Fact] public void OppositeTrainingProducesDifferentPersistedStyles()
    {
        var a=new RewardLearning();var b=new RewardLearning();var settings=new AppSettings();
        for(var i=0;i<600;i++)
        {
            var high=i%2==0;var level=high?.9:.1;var p=Decode(i);
            p=p with{Draw=p.Draw with{Complexity=level,Corner=1-level},Write=p.Write with{Length=level,Direct=level},Movement=p.Movement with{Speed=20+130*level,PreferredRegion=1-level},Social=p.Social with{Affinity=level}};
            foreach(var (action,offset) in new[]{(BodyAction.DrawDoodle,0d),(BodyAction.WriteNote,6d),(BodyAction.Walk,12d)})
            {
                var time=i*18+offset;
                foreach(var learner in new[]{a,b})learner.Trace.Observe(new(time,action,ActionCatalog.Category(action),LearningContext.UserNearby,1,[],[],Parameters:p));
                a.Reward(high?RewardButton.Left:RewardButton.Middle,time,settings);
                b.Reward(high?RewardButton.Middle:RewardButton.Left,time,settings);
            }
        }
        var restored=JsonSerializer.Deserialize<LearningState>(JsonSerializer.Serialize(a.State))!;restored.Validate();
        var complex=Decode(19,restored);var simple=Decode(19,b.State);
        Assert.True(complex.Draw.Complexity>simple.Draw.Complexity+.4);
        Assert.True(complex.Write.Length>simple.Write.Length+.4);
        Assert.True(complex.Movement.Speed>simple.Movement.Speed+40);
        Assert.True(complex.Social.Affinity>simple.Social.Affinity+.4);
        Assert.True(simple.Movement.PreferredRegion>complex.Movement.PreferredRegion+.4);
        Assert.Equal(Decode(19,a.State),complex);
        Assert.True(ProceduralDrawing.Generate(complex).Strokes.Sum(s=>s.Length)>ProceduralDrawing.Generate(simple).Strokes.Sum(s=>s.Length));
    }
    [Fact] public void StaleOrClearedTraceCannotTrainCurrentStyle()
    {
        var learning=new RewardLearning();learning.Trace.Observe(new(0,BodyAction.DrawDoodle,ActionCategory.Create,LearningContext.UserNearby,1,[],[],Parameters:Decode(1)));
        learning.Reward(RewardButton.Left,6,new());Assert.Empty(learning.State.Variation.DrawStylePreference);
        learning.Trace.Observe(new(7,BodyAction.WriteNote,ActionCategory.Create,LearningContext.UserNearby,1,[],[],Parameters:Decode(1)));
        learning.Trace.Clear();learning.Reward(RewardButton.Left,8,new());Assert.Empty(learning.State.Variation.TextStylePreference);
    }
    [Fact] public void LegacyLearningAndArtRemainReadableAndCorruptionIsRejected()
    {
        var legacyNode=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new LearningState()))!.AsObject();
        Assert.True(legacyNode.Remove("Variation"));var json=legacyNode.ToJsonString();
        var state=JsonSerializer.Deserialize<LearningState>(json)!;state.Validate();Assert.Empty(state.Variation.ShapePreference);
        var legacy=new CreativeWork(DoodlePattern.Heart,null,5,6,1,DateTimeOffset.UtcNow);
        state.Artworks.Add(legacy);state.Validate();
        state.Variation.DrawStylePreference[StyleFeature.Size]=double.NaN;Assert.Throws<InvalidDataException>(state.Validate);
        Assert.Throws<InvalidDataException>(()=>new GeneratedDrawing([[new(double.NaN,0)]],100,100,2,0).Validate());
    }
    [Fact] public void NoveltyIsBoundedAndTemporary()
    {
        var state=new BehaviorVariationState();var start=DateTimeOffset.UtcNow;
        for(var i=0;i<100;i++)state.Remember(new(BodyAction.Walk,"move:repeat",null,start.AddSeconds(i)));
        Assert.Equal(64,state.RecentOutputHistory.Count);
        Assert.True(state.Repetition("move:repeat",start.AddSeconds(100))>state.Repetition("move:repeat",start.AddMinutes(10)));
        state.Remember(new(BodyAction.Walk,"move:new",null,start.AddHours(1)));Assert.Single(state.RecentOutputHistory);
    }
    [Fact] public void SocialParametersChangeDistanceAndPauseWithoutNewActions()
    {
        var p=Decode(1);p=p with{Social=new(.8,SocialPattern.Follow,false)};
        Assert.True(BehaviorMotion.Social(p,4,500,500,200,200).Waiting);
        var near=BehaviorMotion.Social(p with{Movement=p.Movement with{CursorDistance=70}},1,500,500,200,200);
        var far=BehaviorMotion.Social(p with{Movement=p.Movement with{CursorDistance=240}},1,500,500,200,200);
        Assert.NotEqual(near,far);
        var avoid=BehaviorMotion.Social(p with{Social=p.Social with{AvoidBriefly=true}},1,500,500,200,200);
        Assert.NotEqual(avoid,near);
    }
}
