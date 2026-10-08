using DesktopLife.Core;
using System.Windows;
namespace DesktopLife.App;
public partial class MainWindow
{
    public async Task SmokeDualPresence()
    {
        var other=otherCharacter??throw new Exception("Second runtime missing");
        if(additionalCharacter is null)throw new Exception("Third runtime missing");
        SmokeDogPlayContact();
        var originalPresence=CurrentPresence;
        var originalTarget=careTarget;
        var primaryKind=settings.PetAppearance;
        var count=Application.Current.Windows.Count;
        for(var i=0;i<6;i++)foreach(var mode in Enum.GetValues<PresenceMode>())
        {
            PresenceOptions.SelectedIndex=(int)mode;UpdatePetVisibility();
            if(Pet.IsVisible!=PresencePolicy.Includes(mode,settings.PetAppearance)||secondaryCharacters.Any(character=>character.Window.IsVisible!=PresencePolicy.Includes(mode,character.Kind)))throw new Exception("Presence visibility mismatch");
        }
        if(Application.Current.Windows.Count!=count)throw new Exception("Mode switching leaked windows");
        foreach(var character in secondaryCharacters)
            if(!ReferenceEquals(Pet.Ball,character.Window.Ball)||!ReferenceEquals(Pet.Furniture,character.Window.Furniture)||!ReferenceEquals(Pet.Art,character.Window.Art))throw new Exception("World is not shared");
        var cat=CharacterWindow(PetAppearance.Cat);
        var girl=CharacterWindow(PetAppearance.Girl);
        var dog=CharacterWindow(PetAppearance.BorderCollie);
        cat.ResetPosition();girl.ResetPosition();Pet.Art.ResetCreationCooldownsForSmoke();cat.SetAction(BodyAction.Sleep);girl.SetAction(BodyAction.DrawDoodle);
        if(cat.CurrentAction!=BodyAction.Sleep||girl.CurrentAction!=BodyAction.DrawDoodle||girl.CurrentRoomActivity!=RoomActivity.Draw)throw new Exception("Independent sleep/draw activity failed");
        girl.SmokeFinishActivity();if(!girl.HasCreativeOutput)throw new Exception("Completed drawing produced no artwork");
        cat.SetAction(BodyAction.WriteNote);if(cat.CurrentAction==BodyAction.WriteNote)throw new Exception("Cat bypassed capability");
        girl.SetAction(BodyAction.WriteNote);girl.Say("女孩測試");cat.Say("禁止的貓語句");
        var works=Pet.Art.Works.Count;
        dog.SetAction(BodyAction.WriteNote);
        if(dog.CurrentAction==BodyAction.WriteNote||Pet.Art.Works.Count!=works)throw new Exception("Dog bypassed creative capability");
        var primaryBond=Learning.State.Companion.Bond;var primaryNeeds=Life.State;
        other.Learning.State.Companion.LastCare.Clear();other.Care(CareKind.Pet);
        if(Learning.State.Companion.Bond!=primaryBond||Life.State!=primaryNeeds)throw new Exception("Care leaked to primary character");
        cat.ResetPosition();girl.ResetPosition();
        await SmokeOtherPresenceAvoidance();
        PresenceOptions.SelectedIndex=(int)PresenceMode.All;
        if(characterWindows.Any(window=>!window.IsVisible))throw new Exception("Three-character presence failed");
        Appearances.SelectedItem=PetAppearance.BorderCollie;
        if(settings.PetAppearance!=primaryKind||careTarget!=PetAppearance.BorderCollie)throw new Exception("Selecting dog replaced primary history");
        if(PetName.Text!=CompanionFor(PetAppearance.BorderCollie).Name||CompanionTitle.Text!=$"{CompanionFor(PetAppearance.BorderCollie).Name}的小日子")throw new Exception("Care panel showed another character");
        var bonds=careKinds.ToDictionary(kind=>kind,kind=>CompanionFor(kind).Bond);
        var needs=careKinds.ToDictionary(kind=>kind,kind=>RuntimeFor(kind)?.Life.State??Life.State);
        CompanionFor(PetAppearance.BorderCollie).LastCare.Clear();
        CareSelected(CareKind.Pet);
        foreach(var kind in careKinds.Where(kind=>kind!=PetAppearance.BorderCollie))
            if(CompanionFor(kind).Bond!=bonds[kind]||(RuntimeFor(kind)?.Life.State??Life.State)!=needs[kind])throw new Exception("Dog care changed another resident");
        if(CompanionFor(PetAppearance.BorderCollie).Bond<=bonds[PetAppearance.BorderCollie])throw new Exception("Dog care target was not used");
        SetPaused(true);if(secondaryCharacters.Any(character=>!character.Paused))throw new Exception("A secondary runtime remained active while paused");
        SetPaused(false);if(secondaryCharacters.Any(character=>character.Paused))throw new Exception("A secondary runtime did not resume");
        SmokeRoleConsistency();
        if(!SavePetState())throw new Exception("Household save failed");
        var saved=organismStore.Load();
        if(saved.OtherCharacter is null||saved.AdditionalCharacter is null)throw new Exception("Third resident save missing");
        foreach(var kind in careKinds)
        {
            var profile=saved.GetCharacter(kind)??throw new Exception("Saved resident identity missing");
            if(profile.Learning.Companion.Bond!=CompanionFor(kind).Bond||profile.Position!=CharacterWindow(kind).Position)throw new Exception("Saved resident history or position mismatch");
        }
        userHidden=true;UpdatePetVisibility();if(characterWindows.Any(window=>window.IsVisible))throw new Exception("Household hide failed");
        userHidden=false;UpdatePetVisibility();
        if(characterWindows.Any(window=>!window.IsVisible))throw new Exception("Household restore failed");
        PresenceOptions.SelectedIndex=(int)PresenceMode.DogOnly;
        if(!dog.IsVisible||cat.IsVisible||girl.IsVisible)throw new Exception("Dog-only presence failed");
        if(Pet!=dog)
        {
            await Task.Delay(550);
            if(Pet.DiagnosticAnimationIntervalMs>40)throw new Exception("Hidden world did not animate for its visible resident");
            userHidden=true;UpdatePetVisibility();
            await Task.Delay(100);
            if(Pet.DiagnosticAnimationIntervalMs<500)throw new Exception("Fully hidden household kept its fast world timer");
            userHidden=false;UpdatePetVisibility();
        }
        PresenceOptions.SelectedIndex=(int)originalPresence;
        SelectCare(originalTarget);
    }
    private async Task SmokeOtherPresenceAvoidance()
    {
        var count=Application.Current.Windows.Count;
        var cat=new PetWindow();var girl=new PetWindow(cat);
        try{await cat.SmokeAvoidOverlap(girl);}
        finally{girl.Close();cat.Close();}
        if(Application.Current.Windows.Count!=count)throw new Exception("Isolated presence smoke leaked windows");
    }
    private void SmokeRoleConsistency()
    {
        var originalPresence=CurrentPresence;var originalTarget=careTarget;var originalPause=aiPaused;
        var originalEnvironment=LatestEnvironment;var originalAway=wasAway;var originalAutomatic=automaticActions;var originalCareUntil=careUntil;
        var positions=careKinds.ToDictionary(kind=>kind,kind=>CharacterWindow(kind).Position);
        var bounds=DisplayWorkspace.Bounds;
        try
        {
            PresenceOptions.SelectedIndex=(int)PresenceMode.All;
            for(var i=0;i<careKinds.Length;i++)
            {
                var character=CharacterWindow(careKinds[i]);character.ResetPosition();
                character.RestorePosition(new(bounds.Left+80+i*180,bounds.Top+80+i*40));
            }
            SelectCare(PetAppearance.BorderCollie);
            var beforeReset=careKinds.ToDictionary(kind=>kind,kind=>CharacterWindow(kind).Position);
            ResetPet(this,new RoutedEventArgs());
            var expected=new DesktopBody();var dog=CharacterWindow(PetAppearance.BorderCollie);var dogGeometry=CharacterGeometry.For(PetAppearance.BorderCollie,dog.House?.SceneScale??1);expected.Resize(dogGeometry.Width,dogGeometry.Height,bounds);expected.Reset(bounds);
            if(dog.Position!=new RoomPoint(expected.X,expected.Y))throw new Exception($"Reset button did not reset selected dog: actual={dog.Position}, expected={expected.X},{expected.Y}, size={dog.Width},{dog.Height}");
            foreach(var kind in careKinds.Where(kind=>kind!=PetAppearance.BorderCollie))
                if(CharacterWindow(kind).Position!=beforeReset[kind])throw new Exception("Reset button moved another resident");
            foreach(var character in secondaryCharacters)
            {
                SelectCare(character.Kind);SetPaused(true);
                var companion=character.Learning.State.Companion;
                var lastFeed=companion.LastCare.GetValueOrDefault(CareKind.Feed);var hadFeed=companion.LastCare.ContainsKey(CareKind.Feed);
                var beforeCare=companion.CareCount;var beforeNeeds=character.Life.State;
                companion.LastCare[CareKind.Feed]=DateTimeOffset.UtcNow;
                try
                {
                    CareSelected(CareKind.Feed);
                    if(!aiPaused||secondaryCharacters.Any(resident=>!resident.Paused))throw new Exception("Rejected secondary care resumed household AI");
                    if(companion.CareCount!=beforeCare||character.Life.State!=beforeNeeds)throw new Exception("Rejected secondary care changed resident data");
                }
                finally{if(hadFeed)companion.LastCare[CareKind.Feed]=lastFeed;else companion.LastCare.Remove(CareKind.Feed);}
            }
            var acceptedCharacter=secondaryCharacters[0];SelectCare(acceptedCharacter.Kind);
            var lastPet=acceptedCharacter.Learning.State.Companion.LastCare.GetValueOrDefault(CareKind.Pet);
            var hadPet=acceptedCharacter.Learning.State.Companion.LastCare.ContainsKey(CareKind.Pet);
            var careCount=acceptedCharacter.Learning.State.Companion.CareCount;
            acceptedCharacter.Learning.State.Companion.LastCare.Remove(CareKind.Pet);
            try
            {
                CareSelected(CareKind.Pet);
                if(aiPaused||secondaryCharacters.Any(resident=>resident.Paused)||acceptedCharacter.Learning.State.Companion.CareCount!=careCount+1)throw new Exception("Accepted secondary care did not resume household AI");
            }
            finally{if(hadPet)acceptedCharacter.Learning.State.Companion.LastCare[CareKind.Pet]=lastPet;else acceptedCharacter.Learning.State.Companion.LastCare.Remove(CareKind.Pet);}
            PresenceOptions.SelectedIndex=(int)PresencePolicy.Only(acceptedCharacter.Kind);
            if(Pet.IsVisible)throw new Exception("Hidden-primary greeting fixture did not hide primary");
            Pet.ResetPosition();Pet.SetAction(BodyAction.Sit);
            var missing=SensorValue.Missing("isolated role diagnostic");
            LatestEnvironment=new(DateTimeOffset.UtcNow,missing,missing,missing,missing,missing,missing,missing,SensorValue.Available(0),missing,0);
            wasAway=true;automaticActions=true;careUntil=0;SetPaused(false);
            var sounds=0;void RecordSound(PetSound sound,double strength)=>sounds++;
            Pet.SoundRequested+=RecordSound;
            try
            {
                TickCompanion();
                if(Pet.CurrentAction!=BodyAction.Sit||Pet.CurrentPhase is not null||sounds!=0)throw new Exception("Hidden primary greeted or emitted character sound");
            }
            finally{Pet.SoundRequested-=RecordSound;}
        }
        finally
        {
            LatestEnvironment=originalEnvironment;wasAway=originalAway;automaticActions=originalAutomatic;careUntil=originalCareUntil;
            PresenceOptions.SelectedIndex=(int)originalPresence;
            SelectCare(originalTarget);
            foreach(var kind in careKinds){CharacterWindow(kind).ResetPosition();CharacterWindow(kind).RestorePosition(positions[kind]);}
            SetPaused(originalPause);
        }
    }
}
