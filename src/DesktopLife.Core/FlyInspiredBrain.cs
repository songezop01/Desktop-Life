namespace DesktopLife.Core;

public static class SensoryEncoder
{
    public static double[] Encode(EnvironmentContext context)
    {
        var s=context.Pet;var e=context.Environment;
        return [s.Energy/100,s.Hunger/100,s.Mood/100,s.Boredom/100,s.Curiosity/100,s.Loneliness/100,s.Excitement/100,s.Fatigue/100,
            Valid(e?.CpuPercent.Value),Valid(e?.GpuPercent.Value),Valid(e?.RamPercent.Value),
            e?.IdleSeconds.Value is { } idle ? Math.Exp(-Math.Max(0,idle)/300) : .5,
            context.Personality.Curiosity,context.Personality.Social,context.Personality.Creativity,1];
    }
    private static double Valid(double? value)=>value is { } v && double.IsFinite(v) ? Math.Clamp(v/100,0,1) : .5;
}
public sealed class FlyInspiredBrain : IBrainController
{
    public const int UnitCount=256;
    public const int ActiveCount=16;
    public const int DriveCount=7;
    private readonly (int Input,double Weight)[][] projections;
    private readonly double[][] weights;
    public double Dopamine { get; private set; }
    public double RecentWeightChange { get; private set; }
    public FlyInspiredBrain(double[][]? savedWeights=null)
    {
        var random=new Random(1729);
        projections=Enumerable.Range(0,UnitCount).Select(_=>Enumerable.Range(0,6).Select(_=>(random.Next(16),random.NextDouble()*2-1)).ToArray()).ToArray();
        if(savedWeights is not null && (savedWeights.Length!=UnitCount || savedWeights.Any(row=>row is null||row.Length!=DriveCount||row.Any(v=>!double.IsFinite(v)||Math.Abs(v)>1))))
            throw new InvalidDataException("無效 FlyInspired 權重。");
        weights=savedWeights?.Select(row=>(double[])row.Clone()).ToArray()
            ?? Enumerable.Range(0,UnitCount).Select(_=>Enumerable.Range(0,DriveCount).Select(_=>(random.NextDouble()-.5)*.1).ToArray()).ToArray();
    }
    public BrainOutput Evaluate(EnvironmentContext context)
    {
        var input=SensoryEncoder.Encode(context);
        var raw=projections.Select(p=>Math.Max(0,p.Sum(pair=>input[pair.Input]*pair.Weight))).ToArray();
        var top=Enumerable.Range(0,UnitCount).OrderByDescending(i=>raw[i]).Take(ActiveCount).ToArray();
        var maximum=top.Select(i=>raw[i]).DefaultIfEmpty(1).Max();
        var activity=new double[UnitCount];foreach(var i in top)activity[i]=maximum>0?raw[i]/maximum:0;
        var outputs=new double[DriveCount];
        for(var d=0;d<DriveCount;d++)
        { double sum=0;foreach(var i in top)sum+=activity[i]*weights[i][d];outputs[d]=1/(1+Math.Exp(-sum/Math.Sqrt(ActiveCount))); }
        Dopamine*=.95;
        return new(Enum.GetValues<BehaviorDrive>().ToDictionary(d=>d,d=>outputs[(int)d]),activity,outputs);
    }
    public void Reinforce(IReadOnlyList<EligibilityCredit> credits,double reward,double learningRate)
    {
        if(!double.IsFinite(reward)||Math.Abs(reward)>10||!double.IsFinite(learningRate)||learningRate<=0||learningRate>.01)
            throw new ArgumentOutOfRangeException(nameof(reward));
        Dopamine=Math.Clamp(reward/3,-1,1);RecentWeightChange=0;
        foreach(var credit in credits)
        {
            var e=credit.Entry;if(e.NeuralActivity.Length!=UnitCount)continue;
            var output=(int)PetAction.Drive(e.Action);
            for(var i=0;i<UnitCount;i++)
            {
                var delta=learningRate*reward*credit.Credit*e.NeuralActivity[i];
                var updated=Math.Clamp(weights[i][output]+delta,-1,1);
                RecentWeightChange+=Math.Abs(updated-weights[i][output]);weights[i][output]=updated;
            }
        }
    }
    public double[][] ExportWeights()=>weights.Select(row=>(double[])row.Clone()).ToArray();
}

public static class BrainMixer
{
    public static BrainOutput Mix(BrainOutput utility,BrainOutput fly,BrainOutput? connectome,double utilityWeight,double flyWeight,double connectomeWeight)
    {
        var weights=new[]{utilityWeight,flyWeight,connectome is null?0:connectomeWeight};
        if(weights.Any(v=>!double.IsFinite(v)||v<0)||weights.Sum()<=0) return utility;
        var scores=Enum.GetValues<BehaviorDrive>().ToDictionary(d=>d,d=>(utility.Score(d)*weights[0]+fly.Score(d)*weights[1]+(connectome?.Score(d)??0)*weights[2])/weights.Sum());
        return new(scores,fly.Activity,fly.Outputs);
    }
}
