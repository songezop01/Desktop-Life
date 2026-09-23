using DesktopLife.Core;
using DesktopLife.ExperimentalConnectome;
using System.Diagnostics;
using System.Text.Json;

if(args.Length<1){Console.Error.WriteLine("Usage: ConnectomeTool graph.json [--extract output.json --types KC,MBON --regions MB --ids id1,id2 --hops 1 --threshold 5]");return 1;}
try
{
    var budget=new SimulationBudget();var watch=Stopwatch.StartNew();
    var cache=new ConnectomeCache(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!,"cache"));
    var graph=cache.Load(new LocalConnectomeSource(args[0]),budget);var loadMs=watch.Elapsed.TotalMilliseconds;
    string Option(string name,string fallback=""){var index=Array.IndexOf(args,name);return index>=0&&index+1<args.Length?args[index+1]:fallback;}
    if(args.Contains("--extract"))
    {
        graph=SubgraphExtractor.Extract(graph,Option("--types").Split(',',StringSplitOptions.RemoveEmptyEntries),Option("--regions").Split(',',StringSplitOptions.RemoveEmptyEntries),Option("--ids").Split(',',StringSplitOptions.RemoveEmptyEntries),int.Parse(Option("--hops","0")),double.Parse(Option("--threshold","0"),System.Globalization.CultureInfo.InvariantCulture),budget).Graph;
        File.WriteAllText(Option("--extract"),JsonSerializer.Serialize(graph));
    }
    var adapter=new ConnectomeSimulationAdapter(graph,budget);var context=new EnvironmentContext(new(),new(),null,LearningContext.QuietDesktop);
    BrainOutput output=new(new Dictionary<BehaviorDrive,double>(),[],[]);double total=0,max=0;
    for(var i=0;i<100;i++){output=adapter.Evaluate(context);total+=adapter.TickMilliseconds;max=Math.Max(max,adapter.TickMilliseconds);}
    var before=output.Score(BehaviorDrive.Explore);var trace=adapter.ActivitySnapshot();
    for(var i=0;i<100;i++)adapter.Reinforce([(trace,.5,BehaviorDrive.Explore)],3,.002);
    for(var i=0;i<50;i++)output=adapter.Evaluate(context);
    var after=output.Score(BehaviorDrive.Explore);
    var fly=new FlyInspiredBrain();var flyWatch=Stopwatch.StartNew();for(var i=0;i<1000;i++)fly.Evaluate(context);
    var flyMeanMs=flyWatch.Elapsed.TotalMilliseconds/1000;
    var validation=new Dictionary<string,object>();var validationPassed=true;
    if(args.Contains("--validate-all"))foreach(var drive in Enum.GetValues<BehaviorDrive>())
    {
        var sim=new ConnectomeSimulationAdapter(graph);for(var i=0;i<50;i++)sim.Evaluate(context);
        var initial=sim.Evaluate(context).Score(drive);var a=sim.ActivitySnapshot();
        for(var i=0;i<100;i++)sim.Reinforce([(a,.5,drive)],3,.002);
        for(var i=0;i<50;i++)sim.Evaluate(context);var positive=sim.Evaluate(context).Score(drive);
        for(var i=0;i<200;i++)sim.Reinforce([(a,.5,drive)],-2,.002);
        for(var i=0;i<50;i++)sim.Evaluate(context);var negative=sim.Evaluate(context).Score(drive);
        var ok=positive>initial&&negative<positive;validationPassed&=ok;validation[drive.ToString()]=new{Before=initial,Rewarded=positive,Punished=negative,Pass=ok};
    }
    Console.WriteLine(JsonSerializer.Serialize(new{graph.Metadata,Neurons=graph.Neurons.Length,Edges=graph.Edges.Length,PlasticEdges=adapter.PlasticEdgeMask.Count(v=>v),adapter.ActiveNodes,adapter.AverageActivity,
        LoadMilliseconds=loadMs,CacheHit=cache.LastHit,MeanTickMilliseconds=total/100,MaxTickMilliseconds=max,ManagedMemoryMB=GC.GetTotalMemory(false)/1048576.0,
        PlasticityExploreBefore=before,PlasticityExploreAfter=after,ChangedEdges=adapter.LearnedDeltas.Count(d=>d!=0),
        FlyMeanTickMilliseconds=flyMeanMs,
        Validation=validation,
        Outputs=output.Scores.ToDictionary(p=>p.Key.ToString(),p=>p.Value)},new JsonSerializerOptions{WriteIndented=true}));
    return adapter.ActiveNodes>0 && after>before && validationPassed?0:2;
}
catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
