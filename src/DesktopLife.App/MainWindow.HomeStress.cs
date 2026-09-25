using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    public async Task RunHomeStress(string root,int seconds)
    {
        Priorities.SelectedItem=DisplayPriority.Desktop;Hide();automaticActions=true;QuietMode.IsChecked=false;
        selector=new(70);var random=new Random(70);
        Pet.ParameterFactory=action=>{var context=new EnvironmentContext(Life.State,personality,null,LearningContext.QuietDesktop);return BehaviorDecoder.Decode(action,context,utilityBrain.Evaluate(context),Learning.State,null,lifeClock.Elapsed.TotalSeconds,random.Next());};
        Pet.ConfigureHomeStress();UpdatePetVisibility();
        var process=Process.GetCurrentProcess();var watch=Stopwatch.StartNew();var samples=new List<object>();
        var firstCpu=process.TotalProcessorTime.TotalSeconds;var allocated=GC.GetTotalAllocatedBytes();var exceptions=0;
        try
        {
            for(var t=0;t<seconds;t++)
            {
                await Task.Delay(1000);Pet.StressSceneStep(t);
                if(t%75==0)
                {
                    Life.ApplyCare(Life.State with{Energy=80,Fatigue=20,Hunger=20});Pet.EmotionalState=Life.State;
                    var actions=new[]{BodyAction.Hide,BodyAction.Stretch,BodyAction.Sleep,BodyAction.PlayToy,BodyAction.ObserveCursor,BodyAction.Groom};
                    runningAction=null;Pet.SetAction(actions[(t/75)%actions.Length]);
                }
                process.Refresh();samples.Add(new {Second=t+1,WallSeconds=watch.Elapsed.TotalSeconds,CpuSeconds=process.TotalProcessorTime.TotalSeconds-firstCpu,WorkingSetMB=process.WorkingSet64/1048576d,PrivateMB=process.PrivateMemorySize64/1048576d,AllocatedBytes=GC.GetTotalAllocatedBytes()-allocated,Pet.NavigationFailures,Pet.PathPlans,Pet.PlatformRebuilds,Pet.StuckSequences});
                if(t%30==29)File.WriteAllText(Path.Combine(root,"stress-progress.json"),JsonSerializer.Serialize(samples[^1]));
            }
        }
        catch{exceptions++;throw;}
        finally
        {
            process.Refresh();
            var result=new{Seed=70,Seconds=watch.Elapsed.TotalSeconds,RequestedSeconds=seconds,RoomItems=Pet.Furniture.Count+Pet.ExtraToys.Count,Toys=Pet.AllToys.Count(),Exceptions=exceptions,Pet.StuckSequences,Pet.NavigationFailures,Pet.PathPlans,Pet.PlatformRebuilds,
                MachineCpuPercent=100*(process.TotalProcessorTime.TotalSeconds-firstCpu)/watch.Elapsed.TotalSeconds/Environment.ProcessorCount,
                OneCoreCpuPercent=100*(process.TotalProcessorTime.TotalSeconds-firstCpu)/watch.Elapsed.TotalSeconds,AllocatedMB=(GC.GetTotalAllocatedBytes()-allocated)/1048576d,
                PhaseFrames=Pet.StressPhases.ToDictionary(p=>p.Key.ToString(),p=>p.Value),Samples=samples};
            File.WriteAllText(Path.Combine(root,"stress-report.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        }
        if(Pet.StuckSequences>0)throw new Exception("Stress found stuck sequences; see stress-report.json.");
        SavePetState();RequestExit();
    }
}
