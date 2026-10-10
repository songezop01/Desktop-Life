using System.Diagnostics;
using System.IO;
using System.Windows;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    private enum HomeStress012Input { Hover,Comb,Teaser }
    private sealed class HomeStress012Resident(PetAppearance kind,PetWindow actor)
    {
        internal readonly PetAppearance Kind=kind;
        internal readonly PetWindow Actor=actor;
        internal readonly int InitialFood=actor.FoodContactCount,InitialPet=actor.DirectPetContacts,
            InitialGroom=actor.DirectGroomContacts,InitialTeaser=actor.TeaserContactCount;
        internal Func<FoodContact,bool>? OriginalFoodContact;
        internal int AcceptedBites,AutomaticFoodStarts,HoverAttempts,CombAttempts,HoverCancellations,CombCancellations,
            TeaserRequests,TeaserStarts,InputUnavailable,InputMisses;
        internal int FoodContacts=>Actor.FoodContactCount-InitialFood;
        internal int PetContacts=>Actor.DirectPetContacts-InitialPet;
        internal int GroomContacts=>Actor.DirectGroomContacts-InitialGroom;
        internal int TeaserContacts=>Actor.TeaserContactCount-InitialTeaser;
        internal Guid? FoodReservationSeen;
        internal HomeStress012Input? PendingInput;
        internal int PendingInputDeadline,NextProbeSecond=20,RepeatInputIndex;
        internal int PendingInitialContacts,PendingInitialCancellations;
        internal bool PendingBusyReported,PendingIsRepeat;
        internal readonly Dictionary<string,int> Skips=[];
        internal void Skip(string reason){InputUnavailable++;Skips[reason]=Skips.GetValueOrDefault(reason)+1;}
    }
    private sealed class HomeStress012Evidence
    {
        internal readonly Dictionary<PetAppearance,HomeStress012Resident> Residents=[];
        internal int InputCursor;
        internal readonly List<object> InitialStock=[];
        internal readonly List<string> Violations=[];
        internal int ViolationCount,BowlPortionsConsumed,TablePortionsConsumed,RefillRequests,AcceptedRefills,
            RefillsAfterConsumption,EmptyRefills,CancelledInputChecks,InputProbeCount;
        internal double ElevatedInputSeconds,MaximumElevatedInputSeconds;
        internal string? Failure=>ViolationCount>0?"0.12 contact or finite-stock invariant failed":null;
        internal void Fail(string text){ViolationCount++;if(Violations.Count<24)Violations.Add(text);}
    }

    private HomeStress012Evidence PrepareHomeStress012(string root,IEnumerable<(PetAppearance Kind,PetWindow Window)> characters)
    {
        var profile=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopLifeSmoke"));
        if(!string.Equals(Path.GetDirectoryName(profile),allowed,StringComparison.OrdinalIgnoreCase)||
            !Guid.TryParseExact(Path.GetFileName(profile),"N",out _))
            throw new InvalidOperationException("0.12 stress fixtures require an isolated diagnostic profile.");
        var evidence=new HomeStress012Evidence();
        try
        {
        foreach(var (kind,actor) in characters)
        {
            var resident=new HomeStress012Resident(kind,actor);evidence.Residents.Add(kind,resident);
            // Once-only initial condition, explicitly retained in the report.
            // Subsequent hunger changes come from the real life timer or a bite.
            var life=LifeFor(kind);life.ApplyCare(life.State with{Hunger=75});actor.EmotionalState=life.State;
            resident.OriginalFoodContact=actor.FoodContactRequested;
            actor.FoodContactRequested=contact=>
            {
                var before=food.Find(contact.FurnitureId);var hunger=LifeFor(kind).State.Hunger;
                var accepted=resident.OriginalFoodContact?.Invoke(contact)==true;
                if(!accepted)return false;
                var after=food.Find(contact.FurnitureId);
                if(contact.Resident!=kind||!contact.IsActualContact||before is null||after is null||
                    before.ServingId!=after.ServingId||after.RemainingPortions!=before.RemainingPortions-1||
                    after.Revision!=before.Revision+1||!FoodCatalog.CanEat(kind,before.Kind)||
                    Math.Abs(LifeFor(kind).State.Hunger-Math.Max(0,hunger-FoodCatalog.HungerPerPortion(before.Kind)))>1e-8)
                    evidence.Fail($"Invalid accepted bite for {kind} at {contact.FurnitureId}.");
                resident.AcceptedBites++;
                if(before is not null)
                {if(FoodCatalog.IsPetFood(before.Kind))evidence.BowlPortionsConsumed++;else evidence.TablePortionsConsumed++;}
                return true;
            };
        }
        // Keep the finite initial stock deliberately small so an empty container
        // and a real menu refill are exercised even in the 600-second workload.
        foreach(var surface in Pet.Furniture.Where(f=>f.IsFoodSurface))
        {
            var kind=surface.Item.Kind==FurnitureKind.CatBowl?FoodKind.SharedKibble:FoodKind.Ramen;
            var added=FoodInventory.Replace(food,FoodRoom(),surface.Item.Id,kind,3);
            if(!added.Accepted)throw new InvalidOperationException("Could not create bounded initial stress stock.");
            food=added.Inventory;
            evidence.InitialStock.Add(new{surface.Item.Id,Furniture=surface.Item.Kind.ToString(),Food=kind.ToString(),Portions=3});
        }
        foodReservations.Clear();RefreshFoodSurfaces();QueuePetSave();return evidence;
        }
        catch { EndHomeStress012(evidence);throw; }
    }

    private async Task StepHomeStress012(HomeStress012Evidence evidence,int second,CancellationToken cancellation)
    {
        foreach(var resident in evidence.Residents.Values)
        {
            // A reservation appearing without an explicit stress Feed request is
            // an automatic start from the ordinary life/routine path.
            var reservation=resident.Actor.HomeStressFoodReservationToken;
            if(reservation is {} token&&token!=resident.FoodReservationSeen)
            {resident.AutomaticFoodStarts++;resident.FoodReservationSeen=token;}
            if(resident.Actor.HomeStressDogUsesFurniturePlatform)evidence.Fail("Dog stood on a cat-tree platform during hanging-toy play.");
        }
        var firstFoodCoverageComplete=evidence.Residents.Values.All(r=>r.AcceptedBites>0&&r.AutomaticFoodStarts>0);
        var activeFoodReservation=evidence.Residents.Values.Any(r=>r.Actor.HomeStressFoodReservationToken is not null);
        if(second%10==5)
        {
            foreach(var surface in Pet.Furniture.Where(f=>f.IsFoodSurface).ToArray())
            {
                var before=food.Find(surface.Item.Id);
                // Empty stock still needs the ordinary routed refill. A periodic
                // partial refill must not replace the serving under a resident's
                // first real approach, or the workload would starve its own food
                // coverage before reaching the container.
                if(before is not {RemainingPortions:0}&&!(second%120==115&&before is {Revision:>0}&&
                    firstFoodCoverageComplete&&!activeFoodReservation))continue;
                evidence.RefillRequests++;
                surface.ChooseFoodDiagnostic(before!.Kind);
                var after=food.Find(surface.Item.Id);
                if(after is null||after.ServingId==before.ServingId)continue;
                if(after.RemainingPortions!=FoodCatalog.Capacity(before.Kind)||after.Revision!=0||after.Kind!=before.Kind)
                    evidence.Fail("Routed refill did not create the expected finite full serving.");
                evidence.AcceptedRefills++;if(before.Revision>0)evidence.RefillsAfterConsumption++;
                if(before.RemainingPortions==0)evidence.EmptyRefills++;
            }
        }
        if(second<20||userHidden||aiPaused||Pet.EditingRoom)return;
        var residents=evidence.Residents.Values.OrderBy(r=>r.Kind).ToArray();
        // A pending resident never owns the whole input queue. Poll every safe
        // boundary each second and let another ready resident use this turn.
        // The logical cursor is independent of how long a route or probe took.
        var firstCoveragePending=MissingHomeStress012Coverage(evidence).Length>0;
        var start=evidence.InputCursor%residents.Length;
        for(var offset=0;offset<residents.Length;offset++)
        {
            var index=(start+offset)%residents.Length;
            var resident=residents[index];var actor=resident.Actor;
            // Do not put care or toy requests ahead of this resident's first
            // automatic, physically consumed portion. Needs, navigation and the
            // ordinary food timer continue; no input deadline or busy counter is
            // started while this prerequisite is still pending.
            if(resident.AcceptedBites<1||resident.AutomaticFoodStarts<1)continue;
            if(resident.PendingInput is {} completed&&HomeStress012PendingComplete(resident,completed))
            {
                CompleteHomeStress012Input(evidence,resident,index,residents.Length,second);
            }
            if(resident.PendingInput is not null&&second>=resident.PendingInputDeadline)
            {
                resident.Skip("safe-input-boundary-timeout");resident.PendingInput=null;
                resident.PendingBusyReported=false;resident.NextProbeSecond=second+1;
                evidence.InputCursor=(index+1)%residents.Length;
                continue;
            }
            if(second<resident.NextProbeSecond)continue;
            if(resident.PendingInput is null)
            {
                var missing=MissingHomeStress012Input(resident);
                if(missing is null&&firstCoveragePending)continue;
                resident.PendingInput=missing??(HomeStress012Input)(resident.RepeatInputIndex%
                    (HangingToyInteraction.Allowed(resident.Kind)?3:2));
                resident.PendingInputDeadline=second+30;resident.PendingBusyReported=false;
                resident.PendingIsRepeat=missing is null;
                resident.PendingInitialContacts=HomeStress012Contacts(resident,resident.PendingInput.Value);
                resident.PendingInitialCancellations=HomeStress012Cancellations(resident,resident.PendingInput.Value);
            }
            if(actor.HomeStressTeaserActive||!actor.HomeStressInputReady)
            {
                if(!resident.PendingBusyReported)
                {resident.Skip("busy-hidden-stairs-or-transition");resident.PendingBusyReported=true;}
                if(!actor.HomeStressTeaserActive)actor.RequestHomeStressIdle();
                continue;
            }
            var input=resident.PendingInput.Value;
            if(input is HomeStress012Input.Hover or HomeStress012Input.Comb)
                await ProbeHomeStress012Input(evidence,resident,input==HomeStress012Input.Comb,cancellation);
            else
            {
                // This is a production activity request. Full routed ball input
                // is covered by the direct interaction component separately.
                var surface=Pet.Furniture.Where(f=>f.Teaser is not null).OrderBy(f=>Math.Abs(f.Item.X-actor.Position.X)).FirstOrDefault();
                resident.TeaserRequests++;actor.RequestHomeStressIdle();await Task.Delay(80,cancellation);
                if(surface is not null&&actor.TryStartTeaserActivity(surface))resident.TeaserStarts++;
                else resident.Skip("hanging-toy-unavailable");
            }
            if(HomeStress012PendingComplete(resident,input))
                CompleteHomeStress012Input(evidence,resident,index,residents.Length,second);
            else resident.NextProbeSecond=second+10;
            // At most one ready input probe is run per real scene second. Its
            // existing native contact duration and brief elevation stay bounded.
            return;
        }
    }

    private static HomeStress012Input? MissingHomeStress012Input(HomeStress012Resident resident)
        =>!HomeStress012InputCovered(resident,HomeStress012Input.Hover)?HomeStress012Input.Hover:
            !HomeStress012InputCovered(resident,HomeStress012Input.Comb)?HomeStress012Input.Comb:
            HangingToyInteraction.Allowed(resident.Kind)&&!HomeStress012InputCovered(resident,HomeStress012Input.Teaser)?HomeStress012Input.Teaser:null;
    private static bool HomeStress012InputCovered(HomeStress012Resident resident,HomeStress012Input input)
        =>input switch
        {
            HomeStress012Input.Hover=>resident.PetContacts>0&&resident.HoverCancellations>0,
            HomeStress012Input.Comb=>resident.GroomContacts>0&&resident.CombCancellations>0,
            _=>resident.TeaserContacts>0
        };
    private static int HomeStress012Contacts(HomeStress012Resident resident,HomeStress012Input input)
        =>input switch{HomeStress012Input.Hover=>resident.PetContacts,HomeStress012Input.Comb=>resident.GroomContacts,_=>resident.TeaserContacts};
    private static int HomeStress012Cancellations(HomeStress012Resident resident,HomeStress012Input input)
        =>input switch{HomeStress012Input.Hover=>resident.HoverCancellations,HomeStress012Input.Comb=>resident.CombCancellations,_=>0};
    private static bool HomeStress012PendingComplete(HomeStress012Resident resident,HomeStress012Input input)
        =>HomeStress012InputCovered(resident,input)&&(!resident.PendingIsRepeat||
            HomeStress012Contacts(resident,input)>resident.PendingInitialContacts&&
            (input==HomeStress012Input.Teaser||HomeStress012Cancellations(resident,input)>resident.PendingInitialCancellations));
    private static void CompleteHomeStress012Input(HomeStress012Evidence evidence,HomeStress012Resident resident,int index,int count,int second)
    {
        resident.PendingInput=null;resident.PendingBusyReported=false;
        if(MissingHomeStress012Input(resident) is null)
        {resident.NextProbeSecond=second+120;resident.RepeatInputIndex++;}
        else resident.NextProbeSecond=second+1;
        evidence.InputCursor=(index+1)%count;
    }

    private async Task ProbeHomeStress012Input(HomeStress012Evidence evidence,HomeStress012Resident resident,bool grooming,CancellationToken cancellation)
    {
        var actor=resident.Actor;var comb=grooming?Pet.Furniture.FirstOrDefault(f=>f.IsCombTool&&f.IsVisible):null;
        if(grooming&&comb is null){resident.Skip("missing-visible-comb");return;}
        if(grooming)resident.CombAttempts++;else resident.HoverAttempts++;
        var originalTopmost=actor.Topmost;var combTopmost=comb?.Topmost;
        var visibilityRunning=visibilityTimer.IsEnabled;var elevated=Stopwatch.StartNew();
        var before=grooming?actor.DirectGroomContacts:actor.DirectPetContacts;
        var savedComb=comb?.Item;
        try
        {
            // Brief input-only elevation is authorized for isolated probes.
            // The house's normal desktop placement resumes in finally.
            visibilityTimer.Stop();actor.Topmost=true;actor.SetHomeStressHoverPointer(null);
            await Task.Delay(35,cancellation);
            Point point;
            try{point=grooming?actor.DirectGroomDiagnosticPoint():actor.DirectHeadDiagnosticPoint();}
            catch(InvalidOperationException){resident.Skip("no-exposed-painted-contact-strip");return;}
            catch(Exception ex) when(ex.Message.StartsWith("No exposed illustrated",StringComparison.Ordinal))
            {resident.Skip("no-exposed-painted-contact-strip");return;}
            var pending=actor.PendingDirectContactDiagnostic(grooming?CareKind.Groom:CareKind.Pet);
            if(grooming){comb!.BeginCombDiagnostic();comb.MoveCombDiagnostic(point);}
            else actor.SetHomeStressHoverPointer(point);
            // Real time under both minimum contact durations: cancelling this
            // unfinished stroke must retire its pending callback without a reward.
            await Task.Delay(55,cancellation);
            comb?.ReleaseCombDiagnostic();actor.EndHomeStressHoverInput();
            var rejected=actor.ApplyDirectContact(pending,LifeFor(resident.Kind).State,CompanionFor(resident.Kind));
            if(rejected.Accepted||rejected.Failure!=DirectCareFailure.StaleSession||
                (grooming?actor.DirectGroomContacts:actor.DirectPetContacts)!=before||actor.HoverHandVisible||comb?.CombHeld==true)
                evidence.Fail($"Unfinished {(grooming?"comb":"hover")} contact survived cancellation for {resident.Kind}.");
            else
            {
                evidence.CancelledInputChecks++;
                if(grooming)resident.CombCancellations++;else resident.HoverCancellations++;
            }
            actor.SetHomeStressHoverPointer(null);
            if(grooming)comb!.BeginCombDiagnostic();
            var stroke=Stopwatch.StartNew();
            while(stroke.Elapsed.TotalSeconds<.48&&elevated.Elapsed.TotalSeconds<.78)
            {
                var travel=Math.Min(grooming?12:8,stroke.Elapsed.TotalSeconds*(grooming?40:27));
                var input=new Point(point.X+travel,point.Y);
                if(grooming)comb!.MoveCombDiagnostic(input);else actor.SetHomeStressHoverPointer(input);
                await Task.Delay(20,cancellation);
            }
            if((grooming?actor.DirectGroomContacts:actor.DirectPetContacts)==before)resident.InputMisses++;
            if(comb is not null&&comb.Item!=savedComb)evidence.Fail("A temporary held comb changed its saved stand.");
        }
        finally
        {
            comb?.ReleaseCombDiagnostic();actor.EndHomeStressHoverInput();actor.Topmost=originalTopmost;
            if(comb is not null&&combTopmost is {} topmost)comb.Topmost=topmost;
            var elapsed=elevated.Elapsed.TotalSeconds;
            evidence.InputProbeCount++;evidence.ElevatedInputSeconds+=elapsed;
            evidence.MaximumElevatedInputSeconds=Math.Max(evidence.MaximumElevatedInputSeconds,elapsed);
            UpdatePetVisibility();if(visibilityRunning)visibilityTimer.Start();
        }
    }

    private static string[] MissingHomeStress012Coverage(HomeStress012Evidence evidence)
    {
        var missing=new List<string>();
        foreach(var r in evidence.Residents.Values)
        {
            if(r.AcceptedBites<1||r.AutomaticFoodStarts<1)missing.Add($"{r.Kind}:automatic-finite-food");
            if(r.PetContacts<1||r.HoverCancellations<1)missing.Add($"{r.Kind}:hover-contact-and-cancellation");
            if(r.GroomContacts<1||r.CombCancellations<1)missing.Add($"{r.Kind}:comb-contact-and-cancellation");
            if(HangingToyInteraction.Allowed(r.Kind)&&r.TeaserContacts<1)missing.Add($"{r.Kind}:actual-hanging-toy-contact");
            if(r.AcceptedBites!=r.FoodContacts)missing.Add($"{r.Kind}:food-contact-counter-mismatch");
        }
        if(evidence.BowlPortionsConsumed<1||evidence.TablePortionsConsumed<1||evidence.EmptyRefills<1)
            missing.Add("finite-bowl-and-table-consumption-and-empty-refill");
        return missing.ToArray();
    }
    private static object CaptureHomeStress012(HomeStress012Evidence e)=>new
    {
        Version="0.12",Scope="Participating residents; synthetic routed input with production wall-clock contact gates",
        InputScheduling="Per-resident initial automatic bite precedes diagnostic input; logical round-robin polls ready boundaries once per scene second; first coverage precedes repeats",
        PartialRefillScheduling="After all residents' initial automatic bites and with no active food reservation; empty-container refills remain enabled",
        AllResidentCoverage=e.Residents.Count==Enum.GetValues<PetAppearance>().Length,
        InitialHunger=75,InitialHungerAssignmentsPerResident=1,InitialHungerAssignmentCount=e.Residents.Count,InitialStock=e.InitialStock.ToArray(),
        PeriodicNeedResets=0,SyntheticRewardCalls=0,
        e.BowlPortionsConsumed,e.TablePortionsConsumed,e.RefillRequests,e.AcceptedRefills,e.RefillsAfterConsumption,e.EmptyRefills,
        e.CancelledInputChecks,e.InputProbeCount,e.ElevatedInputSeconds,e.MaximumElevatedInputSeconds,e.ViolationCount,Violations=e.Violations.ToArray(),
        MissingCoverage=MissingHomeStress012Coverage(e),CoverageComplete=MissingHomeStress012Coverage(e).Length==0&&e.ViolationCount==0,
        Residents=e.Residents.Values.ToDictionary(r=>r.Kind.ToString(),r=>new
        {
            r.FoodContacts,r.AcceptedBites,r.AutomaticFoodStarts,r.PetContacts,r.GroomContacts,r.TeaserContacts,
            r.HoverAttempts,r.CombAttempts,r.HoverCancellations,r.CombCancellations,r.TeaserRequests,r.TeaserStarts,
            WaitingForInitialAutomaticFood=r.AcceptedBites<1||r.AutomaticFoodStarts<1,
            r.InputUnavailable,r.InputMisses,Skips=new Dictionary<string,int>(r.Skips)
            ,TeaserState=r.Actor.HomeStressTeaserDiagnostic
        })
    };
    private static void EndHomeStress012(HomeStress012Evidence evidence)
    {
        foreach(var resident in evidence.Residents.Values)
        {resident.Actor.EndHomeStressHoverInput();resident.Actor.FoodContactRequested=resident.OriginalFoodContact;}
    }
}
