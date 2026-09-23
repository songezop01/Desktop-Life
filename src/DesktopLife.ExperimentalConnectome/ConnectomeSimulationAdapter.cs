using System.Diagnostics;
using DesktopLife.Core;
namespace DesktopLife.ExperimentalConnectome;
public sealed class ConnectomeSimulationAdapter : IBrainController
{
    public ConnectomeGraph Graph { get; }
    private readonly SimulationBudget budget;
    private readonly int[] sources,targets;
    private readonly double[] denominators;
    private double[] activity;
    public double[] LearnedDeltas { get; }
    public bool[] PlasticEdgeMask { get; }
    public double TickMilliseconds { get; private set; }
    public bool BudgetExceeded { get; private set; }
    public double RecentWeightChange { get; private set; }
    public int ActiveNodes=>activity.Count(a=>a>.01);
    public double AverageActivity=>activity.Average();
    public ConnectomeSimulationAdapter(ConnectomeGraph graph,SimulationBudget? limits=null,double[]? savedDeltas=null)
    {
        budget=limits??new();graph.Validate(budget);Graph=graph;
        var indices=graph.Neurons.Select((n,i)=>(n.Id,i)).ToDictionary(p=>p.Id,p=>p.i);
        sources=graph.Edges.Select(e=>indices[e.Source]).ToArray();targets=graph.Edges.Select(e=>indices[e.Target]).ToArray();
        PlasticEdgeMask=graph.Edges.Select(e=>e.Plastic).ToArray();LearnedDeltas=new double[graph.Edges.Length];
        if(savedDeltas is not null)
        {
            if(savedDeltas.Length!=LearnedDeltas.Length||savedDeltas.Where((v,i)=>!double.IsFinite(v)||Math.Abs(v)>graph.Edges[i].OriginalWeight*.5||!PlasticEdgeMask[i]&&v!=0).Any())throw new InvalidDataException("Invalid learned connectome delta.");
            savedDeltas.CopyTo(LearnedDeltas,0);
        }
        activity=new double[graph.Neurons.Length];denominators=new double[activity.Length];
        for(var i=0;i<sources.Length;i++)denominators[targets[i]]+=graph.Edges[i].OriginalWeight;
        for(var i=0;i<denominators.Length;i++)denominators[i]=Math.Max(1,denominators[i]);
    }
    public double EffectiveWeight(int edge)=>Graph.Edges[edge].OriginalWeight+LearnedDeltas[edge];
    public BrainOutput Evaluate(EnvironmentContext context)
    {
        var watch=Stopwatch.StartNew();var input=SensoryEncoder.Encode(context);
        for(var step=0;step<3;step++)
        {
            var next=new double[activity.Length];
            for(var i=0;i<next.Length;i++)
            {
                var type=Graph.Neurons[i].Type;
                // Sensory injection is an engineering interface, not biological neuron input assignment.
                next[i]=activity[i]*.2+(type.StartsWith("KC",StringComparison.OrdinalIgnoreCase)||type=="Input"?input[i%input.Length]*.3:0);
            }
            for(var i=0;i<sources.Length;i++)
            {
                next[targets[i]]+=activity[sources[i]]*EffectiveWeight(i)/denominators[targets[i]]*.65;
                if((i&1023)==0 && watch.Elapsed.TotalMilliseconds>budget.MaxTickMilliseconds){BudgetExceeded=true;TickMilliseconds=watch.Elapsed.TotalMilliseconds;return ConnectomeBehaviorDecoder.Decode(Graph,activity);}
            }
            for(var i=0;i<next.Length;i++)next[i]=Math.Clamp(next[i],0,1);
            activity=next;
        }
        TickMilliseconds=watch.Elapsed.TotalMilliseconds;BudgetExceeded=TickMilliseconds>budget.MaxTickMilliseconds;
        return ConnectomeBehaviorDecoder.Decode(Graph,activity);
    }
    public double[] ActivitySnapshot()=>(double[])activity.Clone();
    public void Reinforce(IReadOnlyList<(double[] Activity,double Credit,BehaviorDrive Drive)> traces,double reward,double rate)
    {
        if(!double.IsFinite(reward)||Math.Abs(reward)>10||!double.IsFinite(rate)||rate<=0||rate>.01)throw new ArgumentOutOfRangeException(nameof(reward));
        RecentWeightChange=0;
        foreach(var trace in traces)
        {
            if(trace.Activity.Length!=activity.Length)continue;
            for(var i=0;i<sources.Length;i++)
            {
                if(!PlasticEdgeMask[i] || Graph.Neurons[targets[i]].Drive!=trace.Drive)continue;
                var original=Graph.Edges[i].OriginalWeight;
                var delta=rate*reward*trace.Credit*trace.Activity[sources[i]]*original;
                var updated=Math.Clamp(LearnedDeltas[i]+delta,-original*.5,original*.5);
                RecentWeightChange+=Math.Abs(updated-LearnedDeltas[i]);LearnedDeltas[i]=updated;
            }
        }
    }
}
public static class ConnectomeBehaviorDecoder
{
    public static BrainOutput Decode(ConnectomeGraph graph,double[] activity)
    {
        var scores=Enum.GetValues<BehaviorDrive>().ToDictionary(d=>d,d=>graph.Neurons.Select((n,i)=>(n,i)).Where(p=>p.n.Drive==d).Select(p=>activity[p.i]).DefaultIfEmpty(0).Average());
        return new(scores,[],scores.OrderBy(p=>p.Key).Select(p=>p.Value).ToArray());
    }
}
