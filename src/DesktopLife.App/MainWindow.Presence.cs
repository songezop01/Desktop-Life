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
    private PetAppearance careTarget;
    private bool presenceReady;
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
            character.Window.Hit+=button=>{SelectCare(character.Kind);if(button!=RewardButton.Middle)CareSelected(CareKind.Pet);};
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
        var ready=presenceReady;presenceReady=false;
        CareTargetOptions.ItemsSource=careKinds.Select(kind=>$"{CompanionFor(kind).Name}（{UiText.Label(kind)}）").ToArray();
        CareTargetOptions.SelectedIndex=Array.IndexOf(careKinds,careTarget);
        presenceReady=ready;
    }
    private void SelectCare(PetAppearance kind)
    {
        careTarget=kind;CareTargetOptions.SelectedIndex=Array.IndexOf(careKinds,kind);UpdateCareTarget();
    }
    private void UpdateCareTarget()
    {
        var girl=careTarget==PetAppearance.Girl;
        FeedButton.Content=girl?"用餐":"餵飯";StrokeButton.Content=girl?"互動":"摸摸";
        PlayButton.Content=girl?"活動":"陪玩";GroomButton.Content=girl?"整理頭髮":"梳理";
        GroomButton.IsEnabled=true;RestButton.Content="休息";
        PetName.Text=SelectedCompanion.Name;
        if(Appearances.SelectedItem is not PetAppearance shown||shown!=careTarget)Appearances.SelectedItem=careTarget;
        ShowCompanion();ShowHomeostasis();
    }
    private void ChangeCareTarget(object sender,SelectionChangedEventArgs e)
    {if(!presenceReady||CareTargetOptions.SelectedIndex<0)return;careTarget=careKinds[CareTargetOptions.SelectedIndex];UpdateCareTarget();}
    private void ChangePresence(object sender,SelectionChangedEventArgs e)
    {
        if(!presenceReady||PresenceOptions.SelectedIndex<0)return;
        settings=settings with{Presence=(PresenceMode)PresenceOptions.SelectedIndex};settingsStore.Save(settings);
        if(!PresencePolicy.Includes(CurrentPresence,careTarget))SelectCare(careKinds.First(kind=>PresencePolicy.Includes(CurrentPresence,kind)));
        UpdatePetVisibility();QueuePetSave();
    }
    private void CareSelected(CareKind kind)
    {
        if(!PresencePolicy.Includes(CurrentPresence,careTarget)){CareStatus.Text="請先顯示要照顧的角色。";return;}
        if(RuntimeFor(careTarget) is {} character)
        {CareStatus.Text=character.Care(kind,out var accepted);if(accepted)SetPaused(false);ShowCompanion();ShowHomeostasis();QueuePetSave();return;}
        Care(kind);
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
