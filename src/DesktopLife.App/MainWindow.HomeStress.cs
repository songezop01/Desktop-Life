using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("user32.dll")] private static extern bool EnumWindows(StressWindowCallback callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    private delegate bool StressWindowCallback(IntPtr window, IntPtr data);
    private static int NativeWindowCount(int processId)
    {var count=0;EnumWindows((window,_)=>{GetWindowThreadProcessId(window,out var owner);if(owner==processId)count++;return true;},IntPtr.Zero);return count;}
    private static Dictionary<string,int> MergeNavigationReasons(IEnumerable<Dictionary<string,int>> sources)
        =>sources.SelectMany(source=>source).GroupBy(reason=>reason.Key).ToDictionary(group=>group.Key,group=>group.Sum(reason=>reason.Value));
    public async Task RunHomeStress(string root,int seconds,PresenceMode presence=PresenceMode.Both,int floors=1,CancellationToken cancellation=default,Action<double,string>? progress=null,bool exitWhenFinished=true)
    {
        if(presence is not (PresenceMode.Both or PresenceMode.All))throw new ArgumentOutOfRangeException(nameof(presence));
        Priorities.SelectedItem=DisplayPriority.Desktop;Hide();automaticActions=true;QuietMode.IsChecked=false;
        selector=new(70);var random=new Random(70);
        Pet.ParameterFactory=action=>{var context=new EnvironmentContext(Life.State,effectivePersonality,null,LearningContext.QuietDesktop);return BehaviorDecoder.Decode(action,context,utilityBrain.Evaluate(context),Learning.State,null,lifeClock.Elapsed.TotalSeconds,random.Next());};
        Pet.ConfigureHomeStress();Pet.ConfigureHouse(floors,true);PresenceOptions.SelectedIndex=(int)presence;
        var participatingCharacters=new[]{(Kind:settings.PetAppearance,Window:Pet)}
            .Concat(secondaryCharacters.Select(character=>(Kind:character.Kind,Window:character.Window)))
            .Where(character=>PresencePolicy.Includes(presence,character.Kind)).ToArray();
        var participants=participatingCharacters.Select(character=>character.Window).ToArray();
        var secondaryParticipants=participants.Where(window=>window!=Pet).ToArray();
        foreach(var character in secondaryParticipants)character.EnableStressRecording();
        UpdatePetVisibility();
        var process=Process.GetCurrentProcess();var watch=Stopwatch.StartNew();var samples=new List<object>();
        var firstCpu=process.TotalProcessorTime.TotalSeconds;var allocated=GC.GetTotalAllocatedBytes();var exceptions=0;
        object? setupPerformance=null,durableReloadVerification=null,workloadPerformance=null;double steadyMeasurementStartedAtSeconds=0;
        double? workloadSeconds=null,workloadCpuSeconds=null,workloadAllocatedMB=null;
        var startedUtc=DateTimeOffset.UtcNow;
        var mutationRandom=new Random(701);var randomMutations=0;var artworkAttempts=0;var artworkCreated=0;var artworkClears=0;
        File.WriteAllText(Path.Combine(root,"stress-process.json"),JsonSerializer.Serialize(new{ProcessId=process.Id,StartedUtc=DateTimeOffset.UtcNow,RequestedSeconds=seconds,Presence=presence.ToString(),Floors=floors,Workspace=DisplayWorkspace.Bounds}));
        try
        {
            for(var t=0;t<seconds;t++)
            {
                await Task.Delay(1000,cancellation);Pet.StressSceneStep(t);
                if(seconds>=1200&&t>=600)
                {
                    // The long workload adds bounded, reproducible geometry edits and creation/retention/cleanup cycles.
                    if(t%181==180&&Pet.Furniture.Count>0)
                    {
                        var furniture=Pet.Furniture[mutationRandom.Next(Pet.Furniture.Count)];Pet.SetRoomEditing(true);
                        furniture.Relocate(furniture.Item.X+mutationRandom.Next(-60,61),furniture.Item.Y+mutationRandom.Next(-12,13));
                        Pet.SetRoomEditing(false);randomMutations++;QueuePetSave();
                    }
                    if(t%67==66)
                    {
                        var girl=CharacterWindow(PetAppearance.Girl);
                        var girlLearning=settings.PetAppearance==PetAppearance.Girl?Learning.State:secondaryCharacters.Single(character=>character.Kind==PetAppearance.Girl).Learning.State;
                        artworkAttempts++;
                        if(Pet.Art.Create(BodyAction.DrawDoodle,girl.Parameters,girlLearning.Variation,12,12,PetAppearance.Girl))artworkCreated++;
                        if(t%601<67)Pet.Art.KeepWorks();QueuePetSave();
                    }
                    if(t%1801==1800){Pet.Art.ClearWorks();artworkClears++;QueuePetSave();}
                }
                // Exercise only this isolated instance; never activate above the user's application.
                if(t%300==289){ShowActivated=false;Show();userHidden=true;UpdatePetVisibility();}
                if(t%300==299){Hide();userHidden=false;UpdatePetVisibility();}
                if(t%75==0)
                {
                    Life.ApplyCare(Life.State with{Energy=80,Fatigue=20,Hunger=20});Pet.EmotionalState=Life.State;
                    var actions=new[]{BodyAction.Hide,BodyAction.Stretch,BodyAction.Sleep,BodyAction.PlayToy,BodyAction.ObserveCursor,BodyAction.Groom};
                    runningAction=null;Pet.SetAction(actions[(t/75)%actions.Length]);
                    var girl=CharacterWindow(PetAppearance.Girl);
                    girl.StartRoomActivity((RoomActivity)((t/75)%Enum.GetValues<RoomActivity>().Length));
                }
                process.Refresh();var sample=new {Second=t+1,WallSeconds=watch.Elapsed.TotalSeconds,CpuSeconds=process.TotalProcessorTime.TotalSeconds-firstCpu,WorkingSetMB=process.WorkingSet64/1048576d,PrivateMB=process.PrivateMemorySize64/1048576d,AllocatedBytes=GC.GetTotalAllocatedBytes()-allocated,
                    NavigationFailures=participants.Sum(window=>window.NavigationFailures),ApproachTimeouts=participants.Sum(window=>window.ApproachTimeouts),
                    CharacterApproachTimeouts=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.ApproachTimeouts),
                    CharacterApproachTimeoutDetails=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.ApproachTimeoutDetails.ToArray()),
                    CharacterPlayTargetRevisions=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.PlayTargetRevisionCount),
                    CharacterPlayApproachStops=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.PlayApproachStops.ToDictionary(stop=>stop.Key.ToString(),stop=>stop.Value)),
                    HouseRouteFailures=participants.Sum(window=>window.HouseRouteFailures),
                    NavigationRecoveriesCompleted=participants.Sum(window=>window.NavigationRecoveriesCompleted),NavigationRecoveryTimeouts=participants.Sum(window=>window.NavigationRecoveryTimeouts),
                    NavigationRecoveryCompletionMaxSeconds=participants.Max(window=>window.NavigationRecoveryCompletionMaxSeconds),
                    NavigationFailureReasons=MergeNavigationReasons(participants.Select(window=>window.NavigationFailureReasons)),
                    NavigationRecoveryCompletionReasons=MergeNavigationReasons(participants.Select(window=>window.NavigationRecoveryCompletionReasons)),
                    NavigationRecoveryTimeoutReasons=MergeNavigationReasons(participants.Select(window=>window.NavigationRecoveryTimeoutReasons)),
                    CharacterNavigationReasons=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>new Dictionary<string,int>(character.Window.NavigationFailureReasons)),
                    CharacterNavigationRecoveriesCompleted=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveriesCompleted),
                    CharacterNavigationRecoveryTimeouts=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveryTimeouts),
                    CharacterNavigationRecoveryCompletionMaxSeconds=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveryCompletionMaxSeconds),
                    CharacterNavigationRecoveryCompletionReasons=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>new Dictionary<string,int>(character.Window.NavigationRecoveryCompletionReasons)),
                    CharacterNavigationRecoveryTimeoutReasons=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>new Dictionary<string,int>(character.Window.NavigationRecoveryTimeoutReasons)),
                    PathPlans=participants.Sum(window=>window.PathPlans),
                    PlatformRebuilds=participants.Sum(window=>window.PlatformRebuilds),StuckSequences=participants.Sum(window=>window.StuckSequences),
                    UnreachableTargets=participants.Sum(window=>window.UnreachableTargets),InvalidFurnitureInteractions=participants.Sum(window=>window.InvalidFurnitureInteractions),
                    Gen0=GC.CollectionCount(0),Gen1=GC.CollectionCount(1),Gen2=GC.CollectionCount(2),ManagedBytes=GC.GetTotalMemory(false),
                    process.HandleCount,GdiObjects=GetGuiResources(process.Handle,0),UserObjects=GetGuiResources(process.Handle,1),
                    NativeWindows=NativeWindowCount(process.Id),Windows=Application.Current.Windows.Count,
                    RoomWindows=Application.Current.Windows.OfType<RoomWindow>().Count(),ToyWindows=Application.Current.Windows.OfType<ToyWindow>().Count(),
                    ArtWindows=Application.Current.Windows.OfType<ArtWindow>().Count(),ArtworkCount=Pet.Art.Works.Count,
                    KeptArtworkCount=Pet.Art.Works.Count(work=>work.Keep),ArtworkPoints=Pet.Art.Works.Sum(work=>work.Drawing?.Strokes.Sum(stroke=>stroke.Length)??0),
                    DecodedCompanionAtlases=CompanionSpriteVisual.DecodedAtlasCount,RandomMutations=randomMutations,ArtworkAttempts=artworkAttempts,ArtworkCreated=artworkCreated,ArtworkClears=artworkClears,
                    FurnitureContextRebuilds=Pet.Furniture.Sum(f=>f.ContextRebuilds),PerformancePhase=setupPerformance is null?"Setup":"Steady",Performance=CapturePerformance()};
                var failure=sample.InvalidFurnitureInteractions>0?"Invalid character-furniture interaction":
                    sample.ApproachTimeouts>0?"Character approach timed out":
                    sample.HouseRouteFailures>0?"House route failed":
                    sample.NavigationRecoveryTimeouts>0?"Navigation recovery timed out":
                    sample.StuckSequences>0?"Character sequence stuck":null;
                // Retain the first failing state immediately. The final report in finally
                // captures per-character evidence before the isolated process exits.
                if(t==0||t%30==29||t==seconds-1||failure is not null)
                {
                    samples.Add(sample);
                    File.AppendAllText(Path.Combine(root,"stress-samples.jsonl"),JsonSerializer.Serialize(sample)+Environment.NewLine);
                }
                if(t%30==29||failure is not null)File.WriteAllText(Path.Combine(root,"stress-progress.json"),JsonSerializer.Serialize(sample));
                progress?.Invoke(watch.Elapsed.TotalSeconds,string.Join("、",participatingCharacters.Select(character=>$"{UiText.Label(character.Kind)}：{character.Window.NavigationFailures} 次導航恢復／{character.Window.StuckSequences} 次卡住")));
                if(failure is not null)throw new InvalidOperationException(failure+"; see retained stress-report.json and stress-progress.json.");
                if(t==2)
                {
                    // Preserve all workload/resource/error counters; split only UI latency and save timings after startup settles.
                    setupPerformance=BeginSteadyMeasurement();steadyMeasurementStartedAtSeconds=watch.Elapsed.TotalSeconds;
                }
            }
            workloadSeconds=watch.Elapsed.TotalSeconds;workloadCpuSeconds=process.TotalProcessorTime.TotalSeconds-firstCpu;
            workloadAllocatedMB=(GC.GetTotalAllocatedBytes()-allocated)/1048576d;workloadPerformance=CapturePerformance();
            cancellation.ThrowIfCancellationRequested();
            progress?.Invoke(watch.Elapsed.TotalSeconds,"場景完成；正在保存並核對隔離資料");
            // Freeze the isolated instance, await a real durable write, then validate the exact captured graph from disk.
            lifeTimer.Stop();learningTimer.Stop();visibilityTimer.Stop();SetPaused(true);
            foreach(var window in characterWindows)window.SetSimulationEnabled(false);
            var expected=CapturePetSave();var receipt=await saveQueue.Enqueue(expected);
            var expectedPath=Path.Combine(root,"stress-expected-organism.json");expected.ExportTo(expectedPath);
            var exact=File.ReadAllBytes(expectedPath).SequenceEqual(File.ReadAllBytes(organismStore.PathName));
            var reloaded=organismStore.Load();
            var kinds=Enum.GetValues<PetAppearance>().Where(kind=>reloaded.GetCharacter(kind) is not null).Select(kind=>kind.ToString()).ToArray();
            durableReloadVerification=new{Succeeded=exact,receipt.Revision,receipt.WriteMilliseconds,receipt.DurableMilliseconds,CharacterKinds=kinds};
            if(!exact)throw new IOException("Final durable stress snapshot differs from the captured three-role state.");
            cancellation.ThrowIfCancellationRequested();
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested){throw;}
        catch{exceptions++;throw;}
        finally
        {
            process.Refresh();
            var navigationFailures=participants.Sum(window=>window.NavigationFailures);
            var expectedGeometryRecoveries=participants.Sum(window=>window.NavigationFailureReasons.GetValueOrDefault("geometry-changed-in-flight"));
            var navigationFailureReasons=MergeNavigationReasons(participants.Select(window=>window.NavigationFailureReasons));
            var secondaryNavigationReasons=MergeNavigationReasons(secondaryParticipants.Select(window=>window.NavigationFailureReasons));
            var recoveryCompletionReasons=MergeNavigationReasons(participants.Select(window=>window.NavigationRecoveryCompletionReasons));
            var recoveryTimeoutReasons=MergeNavigationReasons(participants.Select(window=>window.NavigationRecoveryTimeoutReasons));
            var result=new{StairTrips=participants.Sum(w=>w.StairTrips),HouseRouteFailures=participants.Sum(w=>w.HouseRouteFailures),Seed=70,Workload=seconds>=1200?"native-home-v2-resource-cycles":"native-home-v1",StartedUtc=startedUtc,
                SteadyStartedUtc=startedUtc.AddSeconds(steadyMeasurementStartedAtSeconds),DurableReloadVerification=durableReloadVerification,
                RandomMutationSeed=701,RandomMutations=randomMutations,ArtworkAttempts=artworkAttempts,ArtworkCreated=artworkCreated,ArtworkClears=artworkClears,
                Presence=presence.ToString(),Floors=floors,Workspace=DisplayWorkspace.Bounds,Displays=DisplayWorkspace.Enumerate().Select(display=>new{display.Scale,display.PixelBounds,display.PixelWorkArea,display.Primary,display.Portrait}).ToArray(),Characters=participatingCharacters.Select(character=>character.Kind.ToString()).ToArray(),
                Stopped=cancellation.IsCancellationRequested,CompletedWorkload=workloadSeconds is not null&&!cancellation.IsCancellationRequested,
                InvalidFurnitureInteractions=participants.Sum(window=>window.InvalidFurnitureInteractions),SecondaryStuckSequences=secondaryParticipants.Sum(window=>window.StuckSequences),
                Seconds=workloadSeconds??watch.Elapsed.TotalSeconds,RequestedSeconds=seconds,RoomItems=Pet.Furniture.Count+Pet.ExtraToys.Count,Toys=Pet.AllToys.Count(),Exceptions=exceptions,
                StuckSequences=participants.Sum(window=>window.StuckSequences),NavigationFailures=navigationFailures,ApproachTimeouts=participants.Sum(window=>window.ApproachTimeouts),
                NavigationRecoveryAttempts=navigationFailures,ExpectedGeometryChangeRecoveryAttempts=expectedGeometryRecoveries,UnexpectedNavigationRecoveryAttempts=navigationFailures-expectedGeometryRecoveries,
                NavigationRecoveriesCompleted=participants.Sum(window=>window.NavigationRecoveriesCompleted),NavigationRecoveryTimeouts=participants.Sum(window=>window.NavigationRecoveryTimeouts),
                NavigationRecoveryCompletionMaxSeconds=participants.Max(window=>window.NavigationRecoveryCompletionMaxSeconds),
                NavigationRecoveryTiming="Active visible simulation seconds; hidden, paused, editing and held intervals do not advance safety recovery.",
                NavigationFailureReasons=navigationFailureReasons,
                NavigationRecoveryCompletionReasons=recoveryCompletionReasons,NavigationRecoveryTimeoutReasons=recoveryTimeoutReasons,
                UnreachableTargets=participants.Sum(window=>window.UnreachableTargets),PrimaryNavigationReasons=Pet.NavigationFailureReasons,SecondaryNavigationReasons=secondaryNavigationReasons,
                CharacterNavigationReasons=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationFailureReasons),
                CharacterNavigationRecoveriesCompleted=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveriesCompleted),
                CharacterNavigationRecoveryTimeouts=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveryTimeouts),
                CharacterNavigationRecoveryCompletionMaxSeconds=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveryCompletionMaxSeconds),
                CharacterNavigationRecoveryCompletionReasons=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveryCompletionReasons),
                CharacterNavigationRecoveryTimeoutReasons=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveryTimeoutReasons),
                CharacterNavigationFailureDetails=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationFailureDetails),
                CharacterApproachTimeouts=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.ApproachTimeouts),
                CharacterApproachTimeoutDetails=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.ApproachTimeoutDetails),
                CharacterPlayTargetRevisions=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.PlayTargetRevisionCount),
                CharacterPlayApproachStops=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.PlayApproachStops.ToDictionary(stop=>stop.Key.ToString(),stop=>stop.Value)),
                CharacterNavigationRecoveryDetails=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.NavigationRecoveryDetails),
                EndNavigationPhases=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.MovementPhase.ToString()),
                PathPlans=participants.Sum(window=>window.PathPlans),PlatformRebuilds=participants.Sum(window=>window.PlatformRebuilds),
                MachineCpuPercent=100*(workloadCpuSeconds??process.TotalProcessorTime.TotalSeconds-firstCpu)/(workloadSeconds??watch.Elapsed.TotalSeconds)/Environment.ProcessorCount,
                OneCoreCpuPercent=100*(workloadCpuSeconds??process.TotalProcessorTime.TotalSeconds-firstCpu)/(workloadSeconds??watch.Elapsed.TotalSeconds),AllocatedMB=workloadAllocatedMB??(GC.GetTotalAllocatedBytes()-allocated)/1048576d,
                FurnitureContextRebuilds=Pet.Furniture.Sum(f=>f.ContextRebuilds),SetupPerformance=setupPerformance,
                SteadyMeasurementStartedAtSeconds=steadyMeasurementStartedAtSeconds,Performance=workloadPerformance??CapturePerformance(),DurableReloadPerformance=CapturePerformance(),
                PhaseFrames=Pet.StressPhases.ToDictionary(p=>p.Key.ToString(),p=>p.Value),
                CharacterPhaseFrames=participatingCharacters.ToDictionary(character=>character.Kind.ToString(),character=>character.Window.StressPhases.ToDictionary(phase=>phase.Key.ToString(),phase=>phase.Value)),Samples=samples};
            File.WriteAllText(Path.Combine(root,"stress-report.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        }
        if(participants.Sum(window=>window.StuckSequences)>0)throw new Exception("Stress found stuck sequences; see stress-report.json.");
        if(exitWhenFinished)RequestExit();
    }
}
