using DesktopLife.Core;
using System.Windows;
using System.Windows.Controls;
namespace DesktopLife.App;
public partial class MainWindow
{
    private CharacterRuntime? otherCharacter;
    private CharacterRuntime? additionalCharacter;
    private CharacterRuntime[] secondaryCharacters=[];
    private PetWindow[] characterWindows=[];
    private static readonly PetAppearance[] careKinds=[PetAppearance.Cat,PetAppearance.Girl,PetAppearance.BorderCollie];
    // Kept for existing diagnostics: this selects a data profile, never a care recipient.
    private PetAppearance careTarget;
    private bool presenceReady;
    private bool profileSelectionUpdating;
    private double lastAwareness;
    private PresenceMode CurrentPresence=>settings.Presence??PresencePolicy.Only(settings.PetAppearance);
    private CharacterRuntime? RuntimeFor(PetAppearance kind)=>secondaryCharacters.FirstOrDefault(character=>character.Kind==kind);
    private PetWindow CharacterWindow(PetAppearance kind)=>kind==settings.PetAppearance?Pet:RuntimeFor(kind)?.Window??throw new InvalidOperationException("角色尚未初始化。");
    private CompanionState CompanionFor(PetAppearance kind)=>kind==settings.PetAppearance?Learning.State.Companion:RuntimeFor(kind)?.Learning.State.Companion??throw new InvalidOperationException("角色尚未初始化。");
    private PetState SelectedState=>RuntimeFor(careTarget)?.Life.State??Life.State;
    private CompanionState SelectedCompanion=>RuntimeFor(careTarget)?.Learning.State.Companion??Learning.State.Companion;
    private LearningState SelectedLearning=>RuntimeFor(careTarget)?.Learning.State??Learning.State;
    private static string PresenceLabel(PresenceMode mode)=>mode switch
    {
        PresenceMode.CatOnly=>"只顯示橘貓",
        PresenceMode.GirlOnly=>"只顯示女孩",
        PresenceMode.Both=>"橘貓與女孩",
        PresenceMode.DogOnly=>"只顯示邊牧犬",
        PresenceMode.All=>"三個角色都顯示",
        _=>"角色"
    };
    private void InitializePresence(OrganismSnapshot checkpoint)
    {
        if(checkpoint.PrimaryPosition is {} position)Pet.RestorePosition(position);
        var now=DateTimeOffset.UtcNow;
        var remaining=Enum.GetValues<PetAppearance>().Where(kind=>kind!=settings.PetAppearance).ToArray();
        var otherKind=checkpoint.OtherCharacter?.Kind??remaining[0];
        var additionalKind=remaining.Single(kind=>kind!=otherKind);
        otherCharacter=new(checkpoint.GetCharacter(otherKind)??CharacterProfile.Create(otherKind,now),Pet);
        additionalCharacter=new(checkpoint.GetCharacter(additionalKind)??CharacterProfile.Create(additionalKind,now),Pet,360);
        secondaryCharacters=[otherCharacter,additionalCharacter];
        characterWindows=[Pet,otherCharacter.Window,additionalCharacter.Window];
        careTarget=settings.PetAppearance;
        PresenceOptions.ItemsSource=Enum.GetValues<PresenceMode>().Select(PresenceLabel).ToArray();PresenceOptions.SelectedIndex=(int)CurrentPresence;
        RefreshCareNames();
        foreach(var character in secondaryCharacters)
        {
            character.Window.CareRequested+=kind=>{SelectCare(character.Kind);CareSelected(kind);};
            character.Window.Hit+=_=>SelectCare(character.Kind);
            character.Window.SoundRequested+=(sound,strength)=>audio?.Play(sound,strength);
            character.Window.PreviewMouseDown+=(_,_)=>SelectCare(character.Kind);
        }
        Pet.PreviewMouseDown+=(_,_)=>SelectCare(settings.PetAppearance);
        Loaded+=(_,_)=>UpdatePetVisibility();
        Closed+=(_,_)=>{foreach(var character in secondaryCharacters)character.Window.Close();};
        presenceReady=true;UpdateCareTarget();
    }
    private void RefreshCareNames()
    {
        CatProfileName.Text=CompanionFor(PetAppearance.Cat).Name;
        GirlProfileName.Text=CompanionFor(PetAppearance.Girl).Name;
        DogProfileName.Text=CompanionFor(PetAppearance.BorderCollie).Name;
        foreach(var kind in careKinds)
            ProfileCard(kind).ToolTip=$"查看{CompanionFor(kind).Name}的資料與回憶";
    }
    private RadioButton ProfileCard(PetAppearance kind)=>kind switch
    {PetAppearance.Cat=>CatProfileCard,PetAppearance.Girl=>GirlProfileCard,PetAppearance.BorderCollie=>DogProfileCard,_=>throw new ArgumentOutOfRangeException(nameof(kind))};
    private void ChooseCharacterProfile(object sender,RoutedEventArgs e)
    {
        if(!presenceReady||profileSelectionUpdating||sender is not RadioButton {IsChecked:true} card
            ||!Enum.TryParse<PetAppearance>(card.Tag?.ToString(),out var kind))return;
        SelectCare(kind);
    }
    private void SelectCare(PetAppearance kind)
    {
        careTarget=kind;UpdateCareTarget();
    }
    private void UpdateCareTarget()
    {
        profileSelectionUpdating=true;
        try{ProfileCard(careTarget).IsChecked=true;}
        finally{profileSelectionUpdating=false;}
        PetName.Text=SelectedCompanion.Name;
        // The hidden presentation shim must follow the profile before priority changes.
        // careTarget is already assigned, so ChangePresentation cannot change presence.
        if(Appearances.SelectedItem is not PetAppearance shown||shown!=careTarget)Appearances.SelectedItem=careTarget;
        ShowCompanion(force:true);ShowHomeostasis();
    }
    private void ChangePresence(object sender,SelectionChangedEventArgs e)
    {
        if(!presenceReady||PresenceOptions.SelectedIndex<0)return;
        settings=settings with{Presence=(PresenceMode)PresenceOptions.SelectedIndex};settingsStore.Save(settings);
        UpdatePetVisibility();ShowCompanion();QueuePetSave();
    }
    private void CareSelected(CareKind kind)
    {
        // Compatibility entry point for old callers; actual contact owns all effects.
        CareStatus.Text=LegacyCareGuidance(kind,careTarget);
        CharacterWindow(careTarget).ShowCareFeedback(CareStatus.Text);
    }
    private void TickOther(double dt)
    {
        foreach(var character in secondaryCharacters)
        {
            character.Window.SetRoomEditing(Pet.EditingRoom);
            character.Tick(dt,LatestEnvironment,aiPaused,QuietMode.IsChecked==true);
        }
        if(lifeClock.Elapsed.TotalSeconds-lastAwareness<8)return;
        lastAwareness=lifeClock.Elapsed.TotalSeconds;
        for(var i=0;i<characterWindows.Length;i++)for(var j=i+1;j<characterWindows.Length;j++)
            if(characterWindows[i].IsVisible&&characterWindows[j].IsVisible)
            {characterWindows[i].NoticeOther(characterWindows[j]);characterWindows[j].NoticeOther(characterWindows[i]);}
    }
    private void SmokeDogPlayContact()
    {
        var count=Application.Current.Windows.Count;
        var isolated=new PetWindow();
        try{isolated.SmokeDogPlayContact();}
        finally{isolated.Close();}
        if(Application.Current.Windows.Count!=count)throw new Exception("Isolated dog play smoke leaked windows.");
    }
}
