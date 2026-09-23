using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class V01ValidationTests
{
    [Theory]
    [InlineData(BodyAction.ChaseCursor,RewardButton.Left,true)]
    [InlineData(BodyAction.ChaseCursor,RewardButton.Middle,false)]
    [InlineData(BodyAction.DrawDoodle,RewardButton.Left,true)]
    [InlineData(BodyAction.Sit,RewardButton.Left,true)]
    public void RepeatedFeedbackChangesActionProbability(BodyAction action,RewardButton button,bool increases)
    {
        var context=new EnvironmentContext(new(),new(),null,LearningContext.QuietDesktop,Affordances:new(true,true,true,true));
        var fly=new FlyInspiredBrain();var utility=new UtilityBrain();var learning=new RewardLearning();var selection=new ActionSelection(17);
        double Probability()=>selection.Evaluate(context,BrainMixer.Mix(utility.Evaluate(context),fly.Evaluate(context),null,.6,.4,0),learning).Single(s=>s.Action==action).Probability;
        var before=Probability();
        for(var i=0;i<300;i++)
        {
            var output=fly.Evaluate(context);learning.Trace.Observe(new(i,action,ActionCatalog.Category(action),LearningContext.QuietDesktop,1,output.Activity,output.Outputs));
            var credits=learning.Reward(button,i+.1,new());fly.Reinforce(credits,learning.LastReward!.Amount,.002);
        }
        var after=Probability();Assert.True(increases?after>before:after<before,$"{action}: {before:F4} -> {after:F4}");
        if(action==BodyAction.DrawDoodle)Assert.True(learning.State.CategoryPreference[ActionCategory.Create]>0);
        if(action==BodyAction.Sit)Assert.True(learning.State.CategoryPreference[ActionCategory.Rest]>0);
    }
    [Fact] public void MalformedTraceIsRejectedBeforePlasticity()
    {var trace=new EligibilityTrace();Assert.Throws<ArgumentOutOfRangeException>(()=>trace.Observe(new(0,BodyAction.Walk,ActionCategory.Explore,LearningContext.Unknown,1,[double.NaN],[])));}
    [Fact] public void PersonalityDifferencesAffectScores()
    {var context=new EnvironmentContext(new(),new(){Creativity=0},null,LearningContext.QuietDesktop,Affordances:new(true,true,true,true));var action=new PetAction(BodyAction.DrawDoodle);var before=action.EvaluateUtility(context);Assert.True(action.EvaluateUtility(context with{Personality=new(){Creativity=1}})>before);}
}
