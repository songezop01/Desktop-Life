using DesktopLife.Core;
using System.Windows;
namespace DesktopLife.App;
public partial class MainWindow
{
    private bool wasAway;
    private double lastThought;
    private void InitializeCompanion()
    {
        CloseOptions.ItemsSource=Enum.GetValues<CloseBehavior>();CloseOptions.SelectedItem=settings.CloseBehavior;
        PetName.Text=Learning.State.Companion.Name;
        Pet.Hit-=ApplyReward;
        Pet.CareRequested+=kind=>{SelectCare(settings.PetAppearance);CareSelected(kind);};
        QuietMode.IsChecked=settings.QuietCompanion;
        settingsStore.Save(settings);
        Loaded+=(_,_)=>{Pet.Say($"我是{Learning.State.Companion.Name}，今天也陪著你。",6);ShowCompanion();};
    }
    private void Care(CareKind kind)
    {
        CareStatus.Text=LegacyCareGuidance(kind,settings.PetAppearance);
        Pet.ShowCareFeedback(CareStatus.Text);
    }
    internal static string LegacyCareGuidance(CareKind kind,PetAppearance appearance)=>kind switch
    {
        CareKind.Feed=>appearance==PetAppearance.Girl?"點餐桌準備料理，她餓了會自己用餐。":"點飼料碗補充飼料，夥伴餓了會自己用餐。",
        CareKind.Pet=>"讓滑鼠靠近頭部，再輕輕移動，給夥伴一次溫柔的摸摸。",
        CareKind.Play=>"拖動或撥動小屋裡的球，邀夥伴一起玩。",
        CareKind.Groom=>appearance==PetAppearance.Girl?"拖動梳子靠近頭髮，輕輕梳理。":"拖動梳子靠近毛髮，輕輕梳理。",
        CareKind.Rest=>"放好適合的床鋪，夥伴累了會自己安心休息。",
        _=>"在小屋裡與夥伴輕輕互動。"
    };
    private void RenamePet(object sender,RoutedEventArgs e)
    {
        var name=PetName.Text.Trim();
        if(name.Length is <1 or >16||name.Any(char.IsControl)){CareStatus.Text="名字請使用 1～16 個字。";return;}
        SelectedCompanion.Name=name;CharacterWindow(careTarget).Title=name;
        RefreshCareNames();QueuePetSave();ShowCompanion(force:true);CareStatus.Text=$"這位夥伴的名字已改為「{name}」。";
        CharacterWindow(careTarget).Say($"{name}，我喜歡這個名字！",5);
    }
    private void ResetProfilePosition(object sender,RoutedEventArgs e)=>ResetProfilePosition(careTarget);
    private void ResetProfilePosition(PetAppearance kind)
    {CharacterWindow(kind).ResetPosition();QueuePetSave();CareStatus.Text=$"已讓{CompanionFor(kind).Name}回到安全的位置。";}
    private void ForgetProfileHabits(object sender,RoutedEventArgs e)
    {CharacterWindow(careTarget).ForgetHabits();QueuePetSave();CareStatus.Text=$"已忘記{SelectedCompanion.Name}的家具習慣；其他夥伴的習慣會保留。";ShowCompanion();}
    private void QuietChanged(object sender,RoutedEventArgs e)
    {
        if(Learning is null)return;
        settings=settings with{QuietCompanion=QuietMode.IsChecked==true};settingsStore.Save(settings);
        if(audio is not null){audio.Muted=settings.MuteAudio||settings.QuietCompanion;if(audio.Muted)audio.Stop();}
        Pet.AllowCursorAttraction=QuietMode.IsChecked!=true;
        foreach(var character in secondaryCharacters)character.Window.AllowCursorAttraction=QuietMode.IsChecked!=true;
        automaticActions=true;runningAction=null;
        Pet.Say(QuietMode.IsChecked==true?"你忙，我在旁邊安靜陪你。":"一起玩吧！",4);
    }
    private void TickCompanion()
    {
        Pet.EmotionalState=Life.State;
        Pet.Bond=Learning.State.Companion.Bond;
        var away=LatestEnvironment?.IdleSeconds.Value is >300;
        if(Pet.IsVisible&&wasAway&&LatestEnvironment?.IdleSeconds.Value is <=300&&automaticActions&&!aiPaused&&lifeClock.Elapsed.TotalSeconds>careUntil&&!Pet.Interacting)
        {if(Pet.RequestAction(Learning.State.Companion.Bond>=65?BodyAction.Nuzzle:BodyAction.Greet,BehaviorInterruptReason.Stimulus)){runningAction=null;careUntil=lifeClock.Elapsed.TotalSeconds+6;Pet.Say(Learning.State.Companion.Bond>=65?"你回來了，我想靠近你。":"你回來啦！",5);}}
        wasAway=away;
        if(Pet.IsVisible&&lifeClock.Elapsed.TotalSeconds-lastThought>100&&QuietMode.IsChecked!=true&&!aiPaused)
        {lastThought=lifeClock.Elapsed.TotalSeconds;Pet.Say(Life.State.Hunger>65?"肚子咕嚕咕嚕……":Life.State.Fatigue>75?"眼皮變重了……":Life.State.Loneliness>60?"可以陪我一下嗎？":"有你在，這裡就是我的家。",5);}
        ShowCompanion();
    }
    private void ShowCompanion(bool force=false)
    {
        if(!IsVisible&&!force)return;
        var c=SelectedCompanion;
        CompanionTitle.Text=$"{c.Name}的小日子";
        ProfileHeading.Text=$"{c.Name} · {UiText.Label(careTarget)}";
        var state=SelectedState;
        ProfileStateStatus.Text=$"{(PresencePolicy.Includes(CurrentPresence,careTarget)?"今天一起陪伴":"這次先留在小屋裡")} · {(state.Hunger>65?"肚子有點餓":state.Hunger<25?"吃得飽飽的":"肚子還舒服")} · {(state.Fatigue>75?"想好好休息":state.Fatigue<30?"精神很好":"慢慢過日子")}";
        IdentityStatus.Text=careTarget==PetAppearance.Girl?IdentityDescription().Replace("牠","她"):IdentityDescription();
        BondStatus.Text=$"{c.Relationship} · {CompanionCare.Mood(SelectedState)}";
        ProfileMemoriesSection.Header=$"{c.Name}的小回憶";
        Memories.Text=c.Memories.Count==0?"這一頁還空著，生活裡的小互動會慢慢留下回憶。":string.Join("\n",c.Memories.TakeLast(6).Reverse().Select(m=>$"{m.At.ToLocalTime():MM/dd HH:mm}  {m.Text}"));
    }
    public void RenderCompanionPanel(string path)
    {
        UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(this);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file=System.IO.File.Create(path);encoder.Save(file);
    }
    public void SmokeCompanion()
    {
        if(FindName("CareTargetOptions") is not null||new[]{"FeedButton","StrokeButton","PlayButton","GroomButton","RestButton"}.Any(name=>FindName(name) is not null))
            throw new Exception("Legacy care controls are still present.");
        var original=careTarget;var presence=CurrentPresence;var primary=settings.PetAppearance;
        string State()=>System.Text.Json.JsonSerializer.Serialize(careKinds.Select(kind=>new{Kind=kind,State=LifeFor(kind).State,Companion=CompanionFor(kind)}));
        var before=State();var cooldown=careUntil;var paused=aiPaused;var editing=EditRoom.IsChecked;
        try
        {
            foreach(var role in careKinds)
            {
                ProfileCard(role).IsChecked=true;
                if(careTarget!=role||PetName.Text!=CompanionFor(role).Name||CurrentPresence!=presence||settings.PetAppearance!=primary)
                    throw new Exception("Profile card selection changed presence or displayed another character.");
                foreach(var kind in Enum.GetValues<CareKind>())CareSelected(kind);
            }
            foreach(var kind in Enum.GetValues<CareKind>())Care(kind);
            if(State()!=before||careUntil!=cooldown||aiPaused!=paused||EditRoom.IsChecked!=editing)
                throw new Exception("Legacy care guidance changed character needs, history or room state.");
        }
        finally{SelectCare(original);}
    }
}
