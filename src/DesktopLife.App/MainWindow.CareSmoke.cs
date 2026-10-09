using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    public async Task SmokeCareMenus(string root)
    {
        var allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopLifeSmoke"))+Path.DirectorySeparatorChar;
        var output=Path.GetFullPath(root);
        if(!output.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Care menu regression requires an isolated DesktopLifeSmoke temporary profile.");
        var directory=Path.Combine(output,"care-menus-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
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
                throw new Exception("Care regression unexpectedly inherited furniture or toys.");
            foreach(var character in careKinds)
            {
                foreach(var kind in new[]{CareKind.Feed,CareKind.Pet,CareKind.Play,CareKind.Groom,CareKind.Rest})
                {
                    Prepare();var actor=isolated.CharacterWindow(character);
                    actor.PlaceCareToyDiagnostic(isolated.Pet.Ball,210);actor.PlaceCareToyDiagnostic(isolated.Pet.Square,380);
                    var otherStates=careKinds.Where(c=>c!=character).ToDictionary(c=>c,Fingerprint);
                    var before=isolated.CompanionFor(character).CareCount;
                    // Adding furniture leaves this mode on in production. Every real menu option must leave it before care.
                    isolated.EditRoom.IsChecked=true;actor.SeedStaleCareDiagnostic();
                    actor.ClickCareMenuDiagnostic(kind);
                    if(isolated.IsVisible||isolated.EditRoom.IsChecked==true||isolated.characterWindows.Any(w=>w.EditingRoom))
                        throw new Exception($"{character}/{kind} failed to exit layout mode while retaining the hidden console.");
                    if(isolated.CompanionFor(character).CareCount!=before+1)throw new Exception($"{character}/{kind} menu did not reach its own runtime.");
                    Feedback(actor,$"{character}/{kind}");
                    evidence.Add(actor.AdvanceCareMenuDiagnostic(kind));
                    foreach(var other in otherStates)
                        if(Fingerprint(other.Key)!=other.Value)throw new Exception($"{character}/{kind} changed {other.Key}'s needs or companion history.");
                }

                Prepare();var selected=isolated.CharacterWindow(character);
                selected.ClickCareMenuDiagnostic(CareKind.Pet);var accepted=Fingerprint(character);
                selected.ClickCareMenuDiagnostic(CareKind.Pet);
                Rejected(character,CareKind.Pet,"cooldown",accepted);

                Prepare();LifeFor(character).ApplyCare(new(){Hunger=0});
                var full=Fingerprint(character);selected.ClickCareMenuDiagnostic(CareKind.Feed);Rejected(character,CareKind.Feed,"full",full);

                Prepare();LifeFor(character).ApplyCare(new(){Energy=5,Fatigue=95});
                var tired=Fingerprint(character);selected.ClickCareMenuDiagnostic(CareKind.Play);Rejected(character,CareKind.Play,"tired",tired);

                Prepare();selected.RecentDragCareDiagnostic();
                var dragged=Fingerprint(character);selected.ClickCareMenuDiagnostic(CareKind.Groom);Rejected(character,CareKind.Groom,"recent-drag",dragged);

                Prepare();selected.RecoveryCareDiagnostic();
                var recovering=Fingerprint(character);selected.ClickCareMenuDiagnostic(CareKind.Feed);Rejected(character,CareKind.Feed,"recovering",recovering);
                if(selected.MovementPhase!=NavigationPhase.Recovering)throw new Exception("Care rejection cancelled navigation recovery.");
                selected.AssertBusyCareLeavesNoQueueDiagnostic();
            }

            foreach(var runtime in isolated.secondaryCharacters)
            {
                Prepare();runtime.Window.SetRoomEditing(true);var before=Fingerprint(runtime.Kind);
                var message=runtime.Care(CareKind.Groom,out var accepted);
                if(accepted||string.IsNullOrWhiteSpace(message)||Fingerprint(runtime.Kind)!=before)
                    throw new Exception("Direct secondary runtime care accepted layout mode or changed its profile.");
                evidence.Add(new{Character=runtime.Kind.ToString(),Case="runtime-editing-rejected",Unchanged=true});
            }

            Prepare();var bounds=DisplayWorkspace.Bounds;var floor=isolated.Pet.House!.Floors[0];
            isolated.Pet.AddRoomItem(new(Guid.NewGuid(),FurnitureKind.BellBall,bounds.Left+500,floor.Y-40));
            var spare=isolated.Pet.ExtraToys.Single();
            foreach(var character in new[]{PetAppearance.Cat,PetAppearance.BorderCollie})
            {
                Prepare();var actor=isolated.CharacterWindow(character);var other=character==PetAppearance.Cat?PetAppearance.BorderCollie:PetAppearance.Cat;
                actor.PlaceCareToyDiagnostic(isolated.Pet.Ball,110);actor.PlaceCareToyDiagnostic(spare,210);
                var ballId=actor.CareToyIdDiagnostic(isolated.Pet.Ball);var spareId=actor.CareToyIdDiagnostic(spare);
                if(!actor.Occupancy.TryAcquire(ballId,other))throw new Exception("Failed to arrange occupied nearest toy fixture.");
                actor.CoolCareToyDiagnostic(spare);actor.ClickCareMenuDiagnostic(CareKind.Play);
                if(actor.AttentionTarget!=spareId)throw new Exception($"{character} manual play did not choose the free toy over occupied/cooling alternatives.");
                Feedback(actor,"available-spare");evidence.Add(actor.AdvanceCareMenuDiagnostic(CareKind.Play));

                Prepare();actor.Occupancy.TryAcquire(ballId,other);actor.Occupancy.TryAcquire(spareId,other);
                var busy=Fingerprint(character);actor.ClickCareMenuDiagnostic(CareKind.Play);Rejected(character,CareKind.Play,"all-toys-occupied",busy);

                Prepare();isolated.Pet.Ball.Model.Held=spare.Model.Held=true;
                var held=Fingerprint(character);actor.ClickCareMenuDiagnostic(CareKind.Play);Rejected(character,CareKind.Play,"all-round-toys-held-square-free",held);
            }
            File.WriteAllText(Path.Combine(output,"care-menu-check.json"),JsonSerializer.Serialize(new
            {Passed=true,IsolatedProfile=directory,HiddenConsole=true,MenuClickDispatch=true,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));

            HomeostasisSession LifeFor(PetAppearance character)=>isolated.RuntimeFor(character)?.Life??isolated.Life;
            string Fingerprint(PetAppearance character)=>JsonSerializer.Serialize(new{Needs=LifeFor(character).State,Companion=isolated.CompanionFor(character)});
            void Prepare()
            {
                isolated.EditRoom.IsChecked=false;isolated.SetPaused(false);
                for(var index=0;index<careKinds.Length;index++)
                {
                    var kind=careKinds[index];var actor=isolated.CharacterWindow(kind);
                    actor.PrepareCareMenuDiagnostic(index);LifeFor(kind).ApplyCare(new(){Hunger=70,Energy=80,Fatigue=15});
                    actor.EmotionalState=LifeFor(kind).State;isolated.CompanionFor(kind).LastCare.Clear();
                }
                foreach(var toy in isolated.Pet.AllToys)toy.Model.Held=false;
            }
            void Feedback(PetWindow actor,string name)
            {
                if(!actor.CareFeedbackVisible||string.IsNullOrWhiteSpace(actor.CareFeedbackText))
                    throw new Exception($"{name} gave no visible care feedback with the console hidden.");
            }
            void Rejected(PetAppearance character,CareKind kind,string name,string before)
            {
                var actor=isolated.CharacterWindow(character);Feedback(actor,name);
                if(Fingerprint(character)!=before)throw new Exception($"{character}/{kind}/{name} rejected care still changed its profile.");
                evidence.Add(new{Character=character.ToString(),Care=kind.ToString(),Case=name,FeedbackVisible=true,Message=actor.CareFeedbackText,Unchanged=true});
            }
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(output,"care-menu-check.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
            throw;
        }
        finally
        {
            foreach(var actor in isolated.characterWindows){actor.DismissCareFeedback();actor.SetRoomEditing(false);}
            isolated.RequestExit();await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if(Application.Current.Windows.Count!=windowCount)throw new Exception("Care menu regression leaked native windows.");
        }
    }
}
