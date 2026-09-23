using DesktopLife.Core;
namespace DesktopLife.ExperimentalConnectome;
public sealed record ConnectomeMetadata(string Dataset,string SubgraphName,string Source,string License,bool Synthetic,int SchemaVersion=1);
public sealed record ConnectomeNeuron(string Id,string Type,string Region,BehaviorDrive? Drive=null);
public sealed record ConnectomeEdge(string Source,string Target,double OriginalWeight,bool Plastic=false);
public sealed record ConnectomeGraph(ConnectomeMetadata Metadata,ConnectomeNeuron[] Neurons,ConnectomeEdge[] Edges)
{
    public void Validate(SimulationBudget budget)
    {
        if(Metadata is null||Neurons is null||Edges is null||Metadata.SchemaVersion!=1||string.IsNullOrWhiteSpace(Metadata.Dataset))throw new InvalidDataException("Invalid connectome schema.");
        budget.Check(Neurons.Length,Edges.Length);
        if(Neurons.Length==0||Neurons.Any(n=>n is null||string.IsNullOrWhiteSpace(n.Id)||string.IsNullOrWhiteSpace(n.Type)||n.Drive is {} d&&!Enum.IsDefined(d)))throw new InvalidDataException("Invalid neurons.");
        var ids=Neurons.Select(n=>n.Id).ToHashSet();
        if(ids.Count!=Neurons.Length||Edges.Any(e=>e is null||!ids.Contains(e.Source)||!ids.Contains(e.Target)||!double.IsFinite(e.OriginalWeight)||e.OriginalWeight<=0))throw new InvalidDataException("Duplicate neurons, dangling edges or invalid weights.");
    }
}
public sealed record ConnectomeSubgraph(ConnectomeGraph Graph,string[] SeedIds,int Hops);
public sealed record SimulationBudget(int MaxNeuronCount=5000,int MaxEdgeCount=100000,int MaxMemoryMB=128,double MaxTickMilliseconds=20)
{
    public void Check(int neurons,int edges)
    {
        if(neurons>MaxNeuronCount||edges>MaxEdgeCount||(neurons*512L+edges*256L)>MaxMemoryMB*1024L*1024L)
            throw new InvalidDataException("Subgraph too large：請縮小子網路。");
    }
}
public static class SubgraphExtractor
{
    public static ConnectomeSubgraph Extract(ConnectomeGraph graph,string[] types,string[] regions,string[] ids,int hops,double minimumWeight,SimulationBudget budget)
    {
        if(hops<0||hops>5||!double.IsFinite(minimumWeight)||minimumWeight<0)throw new ArgumentOutOfRangeException(nameof(hops));
        var selected=graph.Neurons.Where(n=>ids.Contains(n.Id)||types.Any(t=>n.Type.Contains(t,StringComparison.OrdinalIgnoreCase))||regions.Contains(n.Region)).Select(n=>n.Id).ToHashSet();
        var seeds=selected.ToArray();var edges=graph.Edges.Where(e=>e.OriginalWeight>=minimumWeight).ToArray();
        for(var i=0;i<hops;i++)
        {var next=edges.Where(e=>selected.Contains(e.Source)||selected.Contains(e.Target)).SelectMany(e=>new[]{e.Source,e.Target}).ToArray();selected.UnionWith(next);budget.Check(selected.Count,0);}
        var subset=new ConnectomeGraph(graph.Metadata with{SubgraphName=$"subset-{hops}hop"},graph.Neurons.Where(n=>selected.Contains(n.Id)).ToArray(),edges.Where(e=>selected.Contains(e.Source)&&selected.Contains(e.Target)).ToArray());
        subset.Validate(budget);return new(subset,seeds,hops);
    }
}
