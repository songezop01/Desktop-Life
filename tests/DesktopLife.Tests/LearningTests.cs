using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class LearningTests
{
    private static void Observe(RewardLearning learning, BodyAction action, double now) => learning.Trace.Observe(new(now,action,ActionCatalog.Category(action),LearningContext.QuietDesktop,1,[],[]));
    [Theory] [InlineData(RewardButton.Left,3)] [InlineData(RewardButton.Right,1)] [InlineData(RewardButton.Middle,-2)]
    public void ButtonsUseConfiguredRewards(RewardButton button,double amount)
    { var l=new RewardLearning(); Observe(l,BodyAction.Walk,0); l.Reward(button,.1,new()); Assert.Equal(amount,l.LastReward!.Amount); Assert.Equal(Math.Sign(amount),Math.Sign(l.State.ActionPreference[BodyAction.Walk])); }
    [Fact] public void RewardWithoutTraceDoesNotLearn() { var l=new RewardLearning(); l.Reward(RewardButton.Left,1,new()); Assert.Empty(l.State.ActionPreference); }
    [Fact] public void OlderActionsReceiveLessCredit()
    { var l=new RewardLearning(); Observe(l,BodyAction.Sleep,0); Observe(l,BodyAction.Walk,2); l.Reward(RewardButton.Left,2.1,new()); Assert.True(l.State.ActionPreference[BodyAction.Walk]>l.State.ActionPreference[BodyAction.Sleep]); }
    [Fact] public void ExpiredActionsCannotBeRewarded() { var l=new RewardLearning(); Observe(l,BodyAction.Sleep,0); l.Reward(RewardButton.Left,5.1,new()); Assert.Empty(l.State.ActionPreference); }
    [Fact] public void OneOldSampleIsNotRenormalizedToFullCredit()
    { var trace=new EligibilityTrace(); trace.Observe(new(0,BodyAction.Walk,ActionCategory.Explore,LearningContext.Unknown,1,[],[])); Assert.True(trace.Credits(4)[0].Credit<.02); }
    [Fact] public void AllThreeLearningLayersChangeTogether()
    { var l=new RewardLearning(); Observe(l,BodyAction.Sit,0); l.Reward(RewardButton.Left,.1,new()); Assert.True(l.State.ActionPreference[BodyAction.Sit]>0); Assert.True(l.State.CategoryPreference[ActionCategory.Rest]>0); Assert.True(l.State.ContextAssociation["QuietDesktop:Sit"]>0); }
    [Fact] public void SingleClickIsSmallAndManyClicksStayBounded()
    { var l=new RewardLearning(); Observe(l,BodyAction.Walk,0); l.Reward(RewardButton.Left,.1,new()); Assert.InRange(l.State.ActionPreference[BodyAction.Walk],0,.006); for(var i=1;i<10000;i++){ Observe(l,BodyAction.Walk,i); l.Reward(RewardButton.Left,i+.1,new()); } l.State.Validate(); }
    [Fact] public void PunishmentReversesReinforcement()
    { var l=new RewardLearning(); for(var i=0;i<20;i++){Observe(l,BodyAction.Walk,i);l.Reward(RewardButton.Left,i+.1,new());} var before=l.Bias(BodyAction.Walk,LearningContext.QuietDesktop); for(var i=20;i<40;i++){Observe(l,BodyAction.Walk,i);l.Reward(RewardButton.Middle,i+.1,new());} Assert.True(l.Bias(BodyAction.Walk,LearningContext.QuietDesktop)<before); }
    [Fact] public void DuplicateMouseEventsAreIgnored()
    { var l=new RewardLearning();Observe(l,BodyAction.Walk,0);l.Reward(RewardButton.Left,0,new());var before=l.State.ActionPreference[BodyAction.Walk];l.Reward(RewardButton.Left,.01,new());Assert.Equal(before,l.State.ActionPreference[BodyAction.Walk]); }
    [Fact] public void EnvironmentAndTraceObservationNeverTrainByThemselves()
    { var l=new RewardLearning();for(var i=0;i<100;i++)Observe(l,BodyAction.Sleep,i);Assert.Empty(l.State.ActionPreference);Assert.Empty(l.State.CategoryPreference);Assert.Empty(l.State.ContextAssociation); }
    [Fact] public void NeuralTraceOwnsItsActivationCopy()
    { var trace=new EligibilityTrace();double[] values=[.5];trace.Observe(new(0,BodyAction.Walk,ActionCategory.Explore,LearningContext.Unknown,1,values,[]));values[0]=1;Assert.Equal(.5,trace.Credits(0)[0].Entry.NeuralActivity[0]); }
    [Fact] public void LearningSaveSurvivesNewController()
    { var root=Path.Combine(Path.GetTempPath(),"DesktopLifeLearningTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{var store=new LearningStore(Path.Combine(root,"learning.json"));var l=new RewardLearning();Observe(l,BodyAction.Walk,0);l.Reward(RewardButton.Left,.1,new());store.Save(l.State);var restored=new RewardLearning(store.Load());Assert.Equal(l.Bias(BodyAction.Walk,LearningContext.QuietDesktop),restored.Bias(BodyAction.Walk,LearningContext.QuietDesktop));}finally{Directory.Delete(root,true);} }
}
