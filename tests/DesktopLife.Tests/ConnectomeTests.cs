using DesktopLife.Core;
using DesktopLife.ExperimentalConnectome;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public class ConnectomeTests
{
    public static ConnectomeGraph Tiny()=>new(new("synthetic","tiny","test fixture","CC0",true),
        [new("1","KC","MB"),new("2","MBON","MB",BehaviorDrive.Explore),new("3","DAN","MB"),new("4","Other","AL")],
        [new("1","2",10,true),new("3","1",2),new("4","3",1)]);
    [Fact] public void RejectsDanglingEdgesAndOversize(){Assert.Throws<InvalidDataException>(()=>(Tiny() with{Edges=[new("missing","2",1)]}).Validate(new()));Assert.Throws<InvalidDataException>(()=>Tiny().Validate(new(MaxNeuronCount:2)));}
    [Fact] public void SubgraphHonorsHopsAndThreshold(){var graph=SubgraphExtractor.Extract(Tiny(),["KC"],[],[],1,2,new()).Graph;Assert.Equal(3,graph.Neurons.Length);Assert.Equal(2,graph.Edges.Length);}
    [Fact] public void SimulationPropagatesAlongRealGraphEdges(){var simulator=new ConnectomeSimulationAdapter(Tiny());var context=new EnvironmentContext(new(),new(),null,LearningContext.QuietDesktop);var output=simulator.Evaluate(context);Assert.True(output.Score(BehaviorDrive.Explore)>0);Assert.False(simulator.BudgetExceeded);}
    [Fact] public void CacheAndParserRoundtrip()
    {var root=Path.Combine(Path.GetTempPath(),"DesktopLifeConnectomeTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{var path=Path.Combine(root,"graph.json");File.WriteAllText(path,JsonSerializer.Serialize(Tiny()));var cache=new ConnectomeCache(Path.Combine(root,"cache"));Assert.Equal(4,cache.Load(new LocalConnectomeSource(path),new()).Neurons.Length);Assert.False(cache.LastHit);cache.Load(new LocalConnectomeSource(path),new());Assert.True(cache.LastHit);File.WriteAllText(path,JsonSerializer.Serialize(Tiny() with{Edges=[new("1","2",12)]}));cache.Load(new LocalConnectomeSource(path),new());Assert.False(cache.LastHit);}finally{Directory.Delete(root,true);}}
    [Fact] public void EmptyGraphCannotSimulate(){Assert.Throws<InvalidDataException>(()=>new ConnectomeSimulationAdapter(Tiny() with{Neurons=[],Edges=[]}));}
}
