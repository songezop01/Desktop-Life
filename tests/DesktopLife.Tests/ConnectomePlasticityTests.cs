using DesktopLife.Core;
using DesktopLife.ExperimentalConnectome;
using Xunit;
namespace DesktopLife.Tests;
public class ConnectomePlasticityTests
{
    private static EnvironmentContext Context()=>new(new(),new(),null,LearningContext.QuietDesktop);
    [Fact] public void RewardChangesOutputButPreservesOriginalAndMaskedEdges()
    {
        var graph=ConnectomeTests.Tiny();var sim=new ConnectomeSimulationAdapter(graph);
        for(var i=0;i<30;i++)sim.Evaluate(Context());var before=sim.Evaluate(Context()).Score(BehaviorDrive.Explore);
        var a=sim.ActivitySnapshot();for(var i=0;i<100;i++)sim.Reinforce([(a,.5,BehaviorDrive.Explore)],3,.002);
        for(var i=0;i<30;i++)sim.Evaluate(Context());
        Assert.True(sim.Evaluate(Context()).Score(BehaviorDrive.Explore)>before);
        Assert.Equal(10,graph.Edges[0].OriginalWeight);Assert.True(sim.LearnedDeltas[0]>0);Assert.Equal(0,sim.LearnedDeltas[1]);
    }
    [Fact] public void PunishmentReducesEffectiveWeight()
    {var sim=new ConnectomeSimulationAdapter(ConnectomeTests.Tiny());sim.Evaluate(Context());var before=sim.EffectiveWeight(0);sim.Reinforce([(sim.ActivitySnapshot(),1,BehaviorDrive.Explore)],-2,.002);Assert.True(sim.EffectiveWeight(0)<before);}
    [Fact] public void WrongBehaviorDoesNotTrainUnrelatedMbon()
    {var sim=new ConnectomeSimulationAdapter(ConnectomeTests.Tiny());sim.Evaluate(Context());sim.Reinforce([(sim.ActivitySnapshot(),1,BehaviorDrive.Create)],3,.002);Assert.All(sim.LearnedDeltas,d=>Assert.Equal(0,d));}
    [Fact] public void SavedDeltasReproduceOutput()
    {var g=ConnectomeTests.Tiny();var a=new ConnectomeSimulationAdapter(g);a.Evaluate(Context());a.Reinforce([(a.ActivitySnapshot(),1,BehaviorDrive.Explore)],3,.002);var b=new ConnectomeSimulationAdapter(g,savedDeltas:a.LearnedDeltas);for(var i=0;i<100;i++){a.Evaluate(Context());b.Evaluate(Context());}Assert.Equal(a.Evaluate(Context()).Score(BehaviorDrive.Explore),b.Evaluate(Context()).Score(BehaviorDrive.Explore),10);}
    [Fact] public void InvalidNonPlasticDeltaIsRejected(){Assert.Throws<InvalidDataException>(()=>new ConnectomeSimulationAdapter(ConnectomeTests.Tiny(),savedDeltas:[0,1,0]));}
}
