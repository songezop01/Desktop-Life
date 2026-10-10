using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    // Retain the release harness entry point, but test the 0.12 contact route.
    // Comb, native occlusion and teaser choreography have separate component checks.
    public async Task SmokeCareMenus(string root)
    {
        var allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopLifeSmoke"))+Path.DirectorySeparatorChar;
        var output=Path.GetFullPath(root);
        if(!output.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Direct care regression requires an isolated DesktopLifeSmoke temporary profile.");
        var directory=Path.Combine(output,"direct-care-subset-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var windowCount=Application.Current.Windows.Count;
        var evidence=new List<object>();
        var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var isolated=new MainWindow(directory,new()
        {
            PetAppearance=PetAppearance.Cat,Presence=PresenceMode.All,DisplayPriority=DisplayPriority.Highest,
            ActiveDisplay=settings.ActiveDisplay,CloseBehavior=CloseBehavior.Exit,MuteAudio=true,DesktopIconsEnabled=false
        });
        isolated.Closed+=(_,_)=>closed.TrySetResult();
        try
        {
            isolated.FreezeRestartVerification();isolated.Show();
            await isolated.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            isolated.FreezeRestartVerification();isolated.SetPaused(false);isolated.Hide();
            isolated.PresenceOptions.SelectedIndex=(int)PresenceMode.All;isolated.UpdatePetVisibility();
            isolated.Pet.ConfigureHouse(1,false);
            if(isolated.Pet.Furniture.Count!=0||isolated.Pet.ExtraToys.Count!=0)
                throw new Exception("Direct care regression inherited furniture or toys.");
            Prepare();await Task.Delay(100);
            if(isolated.FindName("CareTargetOptions") is not null||isolated.characterWindows.Any(actor=>actor.HasLegacyCareMenu))
                throw new Exception("Legacy care selection/menu is still present.");
            Case("legacy-care-entry-points-absent");

            foreach(var character in careKinds)
            {
                Prepare();var actor=isolated.CharacterWindow(character);var point=actor.DirectHeadDiagnosticPoint();
                // A different data card is intentionally selected: contact carries identity.
                isolated.SelectCare(careKinds.First(kind=>kind!=character));
                var others=careKinds.Where(kind=>kind!=character).ToDictionary(kind=>kind,isolated.DirectCareFingerprint);
                var before=isolated.CompanionFor(character).CareCount;var contacts=actor.DirectPetContacts;
                var stationary=isolated.DirectCareFingerprint(character);
                for(var i=0;i<20;i++)actor.SampleDirectCareDiagnostic(point,.033);
                if(!actor.HoverHandVisible||isolated.DirectCareFingerprint(character)!=stationary)
                    throw new Exception($"{character} stationary hover granted care or did not display the hand.");
                actor.SampleDirectCareDiagnostic(null,.033);
                isolated.SampleDirectCareStroke(character,point);
                if(isolated.CompanionFor(character).CareCount!=before+1||actor.DirectPetContacts!=contacts+1
                    ||isolated.LifeFor(character).State.Loneliness!=48||!isolated.CompanionFor(character).LastCare.ContainsKey(CareKind.Pet))
                    throw new Exception($"{character} physical stroke did not settle exactly once.");
                foreach(var other in others)
                    if(isolated.DirectCareFingerprint(other.Key)!=other.Value)throw new Exception($"{character} stroke changed {other.Key}'s profile.");
                actor.RenderDirectCareDiagnostic(Path.Combine(output,$"direct-care-subset-{character}.png"));
                var accepted=isolated.DirectCareFingerprint(character);
                actor.SampleDirectCareDiagnostic(null,.033);isolated.SampleDirectCareStroke(character,point);
                if(isolated.DirectCareFingerprint(character)!=accepted||actor.DirectPetContacts!=contacts+1)
                    throw new Exception($"{character} cooldown allowed a second settlement.");
                Case("stationary-intent-single-contact-cooldown-and-identity",character);

                foreach(var block in new[]{"editing","paused","recent-drag","recovering"})
                {
                    Prepare();point=actor.DirectHeadDiagnosticPoint();
                    switch(block)
                    {
                        case "editing":isolated.EditRoom.IsChecked=true;break;
                        case "paused":isolated.SetPaused(true);break;
                        case "recent-drag":actor.RecentDragCareDiagnostic();break;
                        case "recovering":actor.RecoveryCareDiagnostic();break;
                    }
                    var blocked=isolated.DirectCareFingerprint(character);contacts=actor.DirectPetContacts;
                    isolated.SampleDirectCareStroke(character,point);
                    if(isolated.DirectCareFingerprint(character)!=blocked||actor.DirectPetContacts!=contacts||actor.HoverHandVisible)
                        throw new Exception($"{character}/{block} retained a hand or granted contact benefit.");
                    if(block=="recovering"&&actor.MovementPhase!=NavigationPhase.Recovering)
                        throw new Exception("Blocked care discarded safety recovery.");
                    Case(block+"-rejects-without-benefit",character);
                }

                Prepare();var stale=actor.PendingDirectContactDiagnostic(CareKind.Pet);
                var hidden=isolated.DirectCareFingerprint(character);actor.SuspendPresence();actor.Show();
                if(actor.ApplyDirectContact(stale,isolated.LifeFor(character).State,isolated.CompanionFor(character)).Failure!=DirectCareFailure.StaleSession
                    ||isolated.DirectCareFingerprint(character)!=hidden||actor.HoverHandVisible)
                    throw new Exception($"{character} hide restored a cancelled contact or changed history.");
                Case("hide-retires-pending-contact",character);
            }

            Prepare();isolated.EditRoom.IsChecked=true;var profiles=careKinds.ToDictionary(kind=>kind,isolated.DirectCareFingerprint);
            foreach(var character in careKinds)foreach(var kind in Enum.GetValues<CareKind>())
            {
                isolated.CharacterWindow(character).GuideLegacyCareDiagnostic(kind);
                if(string.IsNullOrWhiteSpace(isolated.CareStatus.Text)||isolated.EditRoom.IsChecked!=true||isolated.IsVisible
                    ||careKinds.Any(role=>isolated.DirectCareFingerprint(role)!=profiles[role]))
                    throw new Exception("Legacy guidance changed a profile, left editing or opened the console.");
            }
            Case("legacy-guidance-has-no-effects-and-preserves-editing");
            isolated.EditRoom.IsChecked=false;
            Prepare();foreach(var character in careKinds)isolated.SampleDirectCareStroke(character,isolated.CharacterWindow(character).DirectHeadDiagnosticPoint());
            if(!isolated.SavePetState())throw new Exception("Contact subset persistence failed.");
            var saved=isolated.organismStore.Load();
            foreach(var character in careKinds)
            {
                var profile=saved.GetCharacter(character)??throw new Exception("Saved resident identity missing.");
                if(profile.Pet.State!=isolated.LifeFor(character).State
                    ||JsonSerializer.Serialize(profile.Learning)!=JsonSerializer.Serialize(character==isolated.settings.PetAppearance?isolated.Learning.State:isolated.RuntimeFor(character)!.Learning.State))
                    throw new Exception("Contact needs, learning and memories did not reload together.");
            }
            Case("atomic-needs-learning-and-memory-reload");

            void Prepare()
            {
                isolated.EditRoom.IsChecked=false;isolated.SetPaused(false);
                foreach(var character in careKinds)
                {
                    isolated.PrepareDirectCareSubset(character);
                    isolated.LifeFor(character).ApplyCare(new(){Hunger=70,Energy=80,Fatigue=15,Loneliness=60,Mood=30});
                    isolated.CharacterWindow(character).EmotionalState=isolated.LifeFor(character).State;
                    isolated.CompanionFor(character).LastCare.Clear();
                }
            }
        }
        catch(Exception ex){WriteReport(false,ex.ToString());throw;}
        finally
        {
            try
            {
                foreach(var actor in isolated.characterWindows){actor.DismissCareFeedback();actor.SetRoomEditing(false);}
                isolated.RequestExit();await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                if(Application.Current.Windows.Count!=windowCount)throw new Exception("Direct care subset leaked native windows.");
            }
            catch(Exception ex){WriteReport(false,ex.ToString());throw;}
        }
        WriteReport(true,null);
        void Case(string name,PetAppearance? character=null)=>evidence.Add(new{Case=name,Character=character?.ToString(),Passed=true});
        void WriteReport(bool passed,string? error)=>File.WriteAllText(Path.Combine(output,"care-menu-check.json"),JsonSerializer.Serialize(new
        {Passed=passed,Scope="0.12 direct-care subset, not menu dispatch or Full verification",IsolatedProfile=directory,
            HiddenConsole=true,LegacyMenusAbsent=true,Excluded="Physical comb, native occlusion, teaser and elapsed-time stress have separate checks",Error=error,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
    }

    // These helpers feed samples into the production contact path. They never
    // call the settlement callback or apply care/reward themselves.
    private void PrepareDirectCareSubset(PetAppearance kind)
    {
        var actor=CharacterWindow(kind);actor.PrepareCareMenuDiagnostic(Array.IndexOf(careKinds,kind));
        actor.CancelDirectCare();actor.Topmost=true;actor.Show();
    }
    private void SampleDirectCareStroke(PetAppearance kind,Point point)
    {
        var actor=CharacterWindow(kind);
        for(var i=0;i<12;i++)actor.SampleDirectCareDiagnostic(new(point.X+Math.Min(8,i),point.Y),.033);
    }
    private string DirectCareFingerprint(PetAppearance kind)
    {
        var learning=kind==settings.PetAppearance?Learning.State:RuntimeFor(kind)!.Learning.State;
        // Room and artworks are shared-world snapshots refreshed by any save.
        // Identity, needs and personal learning must remain resident-specific.
        return JsonSerializer.Serialize(new{Needs=LifeFor(kind).State,learning.Companion,learning.PositiveRewards,learning.Punishments,
            learning.ActionPreference,learning.CategoryPreference,learning.ContextAssociation,learning.Variation,learning.Adaptation,
            learning.Transitions,learning.Milestones});
    }
}
