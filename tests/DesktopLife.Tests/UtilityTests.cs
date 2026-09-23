using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class UtilityTests
{
    private static EnvironmentContext Context(PetState? state=null)=>new(state??new(),new(),null,LearningContext.QuietDesktop);
    [Fact] public void CriticalEnergyOverridesLearnedAndManualPreference()
    { var l=new RewardLearning();l.State.ActionPreference[BodyAction.Walk]=.75;var context=Context(new(){Energy=2});var d=new ActionSelection().Select(context,new UtilityBrain().Evaluate(context),l,BodyAction.Walk,true);Assert.Equal(BodyAction.Sleep,d.Action); }
    [Fact] public void FullscreenSafetyProducesNoAction() { var c=Context() with{Fullscreen=true};Assert.Null(new ActionSelection().Select(c,new UtilityBrain().Evaluate(c),new(),BodyAction.Walk,false).Action); }
    [Fact] public void RewardRaisesFutureProbability()
    { var c=Context();var brain=new UtilityBrain().Evaluate(c);var selector=new ActionSelection();var l=new RewardLearning();var before=selector.Evaluate(c,brain,l).Single(s=>s.Action==BodyAction.Walk).Probability;for(var i=0;i<200;i++){l.Trace.Observe(new(i,BodyAction.Walk,ActionCategory.Explore,LearningContext.QuietDesktop,1,[],[]));l.Reward(RewardButton.Left,i+.1,new());}Assert.True(selector.Evaluate(c,brain,l).Single(s=>s.Action==BodyAction.Walk).Probability>before); }
    [Fact] public void PunishmentLowersFutureProbability()
    { var c=Context();var b=new UtilityBrain().Evaluate(c);var s=new ActionSelection();var l=new RewardLearning();var before=s.Evaluate(c,b,l).Single(x=>x.Action==BodyAction.Wander).Probability;for(var i=0;i<200;i++){l.Trace.Observe(new(i,BodyAction.Wander,ActionCategory.Explore,LearningContext.QuietDesktop,1,[],[]));l.Reward(RewardButton.Middle,i+.1,new());}Assert.True(s.Evaluate(c,b,l).Single(x=>x.Action==BodyAction.Wander).Probability<before); }
    [Fact] public void ProbabilityIsFiniteAndNormalized(){var c=Context();var scores=new ActionSelection().Evaluate(c,new UtilityBrain().Evaluate(c),new());Assert.Equal(1,scores.Sum(s=>s.Probability),9);Assert.All(scores,s=>Assert.InRange(s.Probability,0,1));}
    [Fact] public void ActionLifecycleIsIndependentOfWpf(){var a=new PetAction(BodyAction.Walk);var animation=new Animation();a.Start(animation);a.Update(TimeSpan.FromSeconds(1));a.Stop();Assert.Equal(BodyAction.Walk,animation.Action);Assert.False(a.Running);Assert.Equal(1,a.ElapsedSeconds);}
    private class Animation:IAnimationController{public BodyAction Action;public void Play(BodyAction action)=>Action=action;}
}
