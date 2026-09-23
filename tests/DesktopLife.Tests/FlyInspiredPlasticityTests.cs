using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class FlyInspiredPlasticityTests
{
    private static EnvironmentContext Context()=>new(new(),new(),null,LearningContext.QuietDesktop);
    [Fact] public void SparsePopulationStaysBounded(){var b=new FlyInspiredBrain();var o=b.Evaluate(Context());Assert.Equal(256,o.Activity.Length);Assert.InRange(o.Activity.Count(v=>v>0),1,16);Assert.All(o.Outputs,v=>Assert.InRange(v,0,1));}
    [Fact] public void DelayedRewardChangesMatchingOutputAndPunishmentReversesIt()
    {var b=new FlyInspiredBrain();var c=Context();var before=b.Evaluate(c);var credit=new EligibilityCredit(new(0,BodyAction.Wander,ActionCategory.Explore,LearningContext.QuietDesktop,1,before.Activity,before.Outputs),.5);for(var i=0;i<200;i++)b.Reinforce([credit],3,.002);var after=b.Evaluate(c);Assert.True(after.Score(BehaviorDrive.Explore)>before.Score(BehaviorDrive.Explore));for(var i=0;i<400;i++)b.Reinforce([credit],-2,.002);Assert.True(b.Evaluate(c).Score(BehaviorDrive.Explore)<after.Score(BehaviorDrive.Explore));}
    [Fact] public void EvaluationAloneNeverChangesWeights(){var b=new FlyInspiredBrain();var before=b.ExportWeights();for(var i=0;i<20;i++)b.Evaluate(Context());Assert.Equal(before.SelectMany(r=>r),b.ExportWeights().SelectMany(r=>r));}
    [Fact] public void NeuralWeightsRoundTrip(){var b=new FlyInspiredBrain();var output=b.Evaluate(Context());b.Reinforce([new(new(0,BodyAction.Sleep,ActionCategory.Rest,LearningContext.QuietDesktop,1,output.Activity,output.Outputs),1)],3,.002);var restored=new FlyInspiredBrain(b.ExportWeights());Assert.Equal(b.Evaluate(Context()).Outputs,restored.Evaluate(Context()).Outputs);}
    [Fact] public void HybridWithoutConnectomeRenormalizesAvailableSources(){var u=new UtilityBrain().Evaluate(Context());var f=new FlyInspiredBrain().Evaluate(Context());var o=BrainMixer.Mix(u,f,null,.6,.4,.2);Assert.Equal(.6*u.Score(BehaviorDrive.Rest)+.4*f.Score(BehaviorDrive.Rest),o.Score(BehaviorDrive.Rest),9);}
}
