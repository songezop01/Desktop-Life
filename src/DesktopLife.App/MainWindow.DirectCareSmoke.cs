using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using DesktopLife.Core;
using DesktopLife.Windows;

namespace DesktopLife.App;

public partial class MainWindow
{
    internal async Task SmokeDirectCare(string root)
    {
        var allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopLifeSmoke"));
        var full=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if(!string.Equals(Path.GetDirectoryName(full),allowed,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(full),"N",out _))
            throw new Exception("Direct care smoke requires a synthetic profile.");
        var started=DateTimeOffset.UtcNow;FreezeRestartVerification();Hide();aiPaused=false;
        settings=settings with{Presence=PresenceMode.All};Priorities.SelectedItem=DisplayPriority.Highest;UpdatePetVisibility();
        Pet.ConfigureHouse(1,false);
        foreach(var f in Pet.Furniture.ToArray())f.Close();Pet.Furniture.Clear();
        var cases=new List<object>();
        void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        void Case(string name)=>cases.Add(new{Case=name,Passed=true});
        foreach(var kind in Enum.GetValues<PetAppearance>())
        {
            var actor=CharacterWindow(kind);actor.PrepareCareMenuDiagnostic(Array.IndexOf(careKinds,kind));actor.Topmost=true;actor.Show();
            LifeFor(kind).ApplyCare(new(){Loneliness=60,Mood=30});CompanionFor(kind).LastCare.Clear();
            Assert(!actor.HasLegacyCareMenu,"A character still has a care context menu.");
        }
        Assert(FindName("CareTargetOptions") is null,"Care target selector still exists.");Case("old-care-menu-and-target-removed");
        SmokeCompanion();Case("three-profile-cards-and-no-legacy-side-effects");
        foreach(var actor in characterWindows)actor.DismissCareFeedback();
        await Task.Delay(150);
        var counts=Enum.GetValues<PetAppearance>().ToDictionary(k=>k,k=>CompanionFor(k).CareCount);
        foreach(var kind in Enum.GetValues<PetAppearance>())
        {
            var actor=CharacterWindow(kind);var p=actor.DirectHeadDiagnosticPoint();
            for(var i=0;i<20;i++)actor.SampleDirectCareDiagnostic(p,.033);
            Assert(actor.HoverHandVisible&&CompanionFor(kind).CareCount==counts[kind],"Stationary hover counted as care or hand is missing. "+JsonSerializer.Serialize(actor.HoverContactDiagnostic(p)));
            actor.SampleDirectCareDiagnostic(null,.033);
            for(var i=0;i<12;i++)actor.SampleDirectCareDiagnostic(new(p.X+Math.Min(8,i),p.Y),.033);
            Assert(CompanionFor(kind).CareCount==counts[kind]+1&&actor.DirectPetContacts==1&&LifeFor(kind).State.Loneliness==48,"A real head stroke did not settle exactly once.");
            Assert(Enum.GetValues<PetAppearance>().Where(k=>k!=kind).All(k=>CompanionFor(k).CareCount==counts[k]),"Stroke affected a different resident.");
            counts[kind]++;
            actor.RenderDirectCareDiagnostic(Path.Combine(root,$"petting-{kind}.png"));
            actor.SampleDirectCareDiagnostic(null,.033);
            for(var i=0;i<12;i++)actor.SampleDirectCareDiagnostic(new(p.X+Math.Min(8,i),p.Y),.033);
            Assert(CompanionFor(kind).CareCount==counts[kind],"Petting cooldown repeated a reward.");
            actor.SetPaused(true);actor.SampleDirectCareDiagnostic(p,.033);
            Assert(!actor.HoverHandVisible,"Pause retained the virtual hand.");actor.SetPaused(false);
            Case($"{kind}-stationary-intent-contact-cooldown-and-pause");
        }
        var cat=CharacterWindow(PetAppearance.Cat);var head=cat.DirectHeadDiagnosticPoint();
        var old=cat.PendingDirectContactDiagnostic(CareKind.Pet);cat.Hide();cat.Show();
        Assert(cat.ApplyDirectContact(old,LifeFor(PetAppearance.Cat).State,CompanionFor(PetAppearance.Cat)).Failure==DirectCareFailure.StaleSession,"Cancelled contact remained eligible.");Case("hide-retires-pending-contact");
        var blocker=new Window{WindowStyle=WindowStyle.None,ShowActivated=false,ShowInTaskbar=false,Topmost=true,Width=60,Height=60,Background=Brushes.Beige};
        try
        {
            blocker.Show();DisplayWorkspace.Position(blocker,head.X-10,head.Y-10);await Task.Delay(100);
            for(var i=0;i<12;i++)cat.SampleDirectCareDiagnostic(new(head.X+Math.Min(8,i),head.Y),.033);
            Assert(!cat.HoverHandVisible&&CompanionFor(PetAppearance.Cat).CareCount==counts[PetAppearance.Cat],"A covering native window allowed petting.");Case("native-occlusion-blocks-care");
        }
        finally{blocker.Close();}
        cat.SetAction(BodyAction.Sleep);
        for(var i=0;i<12;i++)cat.SampleDirectCareDiagnostic(new(head.X+Math.Min(8,i),head.Y),.033);
        Assert(!cat.HoverHandVisible&&CompanionFor(PetAppearance.Cat).CareCount==counts[PetAppearance.Cat],"Sleeping allowed direct care.");Case("sleep-blocks-contact");
        cat.PrepareCareMenuDiagnostic(0);
        var floor=Pet.House!.Floors[0];var stand=new RoomItem(Guid.NewGuid(),FurnitureKind.Comb,floor.Left+700,floor.Y-40);
        Pet.AddRoomItem(stand);var comb=Pet.Furniture.Single(f=>f.Item.Id==stand.Id);comb.Topmost=true;comb.Show();comb.SetEditing(false);
        Assert(comb.Platforms.Count==0&&FurnitureArt.LoadedCount==Enum.GetValues<FurnitureKind>().Length,"Comb altered platforms or disabled illustration loading.");
        await Task.Delay(100);head=cat.DirectGroomDiagnosticPoint();
        comb.BeginCombDiagnostic();
        var combSamples=new List<object>();
        for(var i=0;i<16;i++){comb.MoveCombDiagnostic(new(head.X+Math.Min(12,i),head.Y));await Task.Delay(20);cat.TickHeldCombDiagnostic(.02);combSamples.Add(cat.CombContactDiagnostic(comb));}
        File.WriteAllText(Path.Combine(root,"comb-contact-samples.json"),JsonSerializer.Serialize(combSamples,new JsonSerializerOptions{WriteIndented=true}));
        Assert(cat.DirectGroomContacts==1&&CompanionFor(PetAppearance.Cat).CareCount==counts[PetAppearance.Cat]+1,"Actual illustrated comb drag did not groom.");
        Assert(comb.Item==stand,"Held comb overwrote its saved stand.");
        var afterComb=CompanionFor(PetAppearance.Cat).CareCount;
        for(var i=0;i<20;i++)cat.TickHeldCombDiagnostic(.033);
        Assert(CompanionFor(PetAppearance.Cat).CareCount==afterComb,"Parked comb repeatedly granted care.");
        comb.ReleaseCombDiagnostic();Assert(!comb.CombHeld&&comb.Item==stand,"Released comb did not return to stand.");Case("actual-comb-drag-single-contact-and-return");
        foreach(var kind in new[]{PetAppearance.Girl,PetAppearance.BorderCollie})
        {
            var actor=CharacterWindow(kind);actor.PrepareCareMenuDiagnostic(Array.IndexOf(careKinds,kind));
            var target=actor.DirectGroomDiagnosticPoint();var before=CompanionFor(kind).CareCount;
            comb.BeginCombDiagnostic();
            for(var i=0;i<16;i++){comb.MoveCombDiagnostic(new(target.X+Math.Min(12,i),target.Y));await Task.Delay(20);actor.TickHeldCombDiagnostic(.02);}
            Assert(actor.DirectGroomContacts==1&&CompanionFor(kind).CareCount==before+1,"Comb did not groom the actual resident.");
            comb.ReleaseCombDiagnostic();Case($"{kind}-actual-comb-contact");
        }
        comb.BeginCombDiagnostic();Pet.SetRoomEditing(true);Assert(!comb.CombHeld,"Editing retained held comb.");Pet.SetRoomEditing(false);Case("editing-cancels-held-tool");
        var beforeLegacy=CompanionFor(PetAppearance.BorderCollie).CareCount;var legacyState=LifeFor(PetAppearance.BorderCollie).State;
        foreach(var kind in Enum.GetValues<CareKind>())RuntimeFor(PetAppearance.BorderCollie)?.Care(kind);
        Assert(CompanionFor(PetAppearance.BorderCollie).CareCount==beforeLegacy&&LifeFor(PetAppearance.BorderCollie).State==legacyState,"Legacy care still bypasses contact.");Case("legacy-care-cannot-bypass-contact");
        Assert(SavePetState(),"Direct care save failed.");var saved=organismStore.Load();
        foreach(var kind in Enum.GetValues<PetAppearance>())Assert(saved.GetCharacter(kind)!.Learning.Companion.CareCount==CompanionFor(kind).CareCount&&saved.GetCharacter(kind)!.Pet.State==LifeFor(kind).State,"Needs and care evidence reloaded separately.");Case("atomic-care-and-needs-reload");
        var windowCount=Application.Current.Windows.Count;var teaserActor=new PetWindow();
        try{await teaserActor.SmokeSharedTeaserContact(root);}
        finally{teaserActor.Close();}
        Assert(Application.Current.Windows.Count==windowCount,"Isolated teaser checks leaked windows.");Case("cat-and-dog-shared-teaser-native");
        File.WriteAllText(Path.Combine(root,"direct-care-report.json"),JsonSerializer.Serialize(new{Status="PASS",Gate="Component checks only",StartedUtc=started,ElapsedSeconds=(DateTimeOffset.UtcNow-started).TotalSeconds,Cases=cases},new JsonSerializerOptions{WriteIndented=true}));
    }
}
