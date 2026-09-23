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
        Pet.CareRequested+=Care;
        QuietMode.IsChecked=settings.QuietCompanion;
        settingsStore.Save(settings);
        Loaded+=(_,_)=>{Pet.Say($"我是{Learning.State.Companion.Name}，今天也陪著你。",6);ShowCompanion();};
    }
    private void Care(CareKind kind)
    {
        if(Pet.Interacting){CareStatus.Text="等你把牠放下，再照顧牠吧。";return;}
        var result=CompanionCare.Apply(Life.State,Learning.State.Companion,kind,DateTimeOffset.UtcNow);
        CareStatus.Text=result.Message;Pet.Say(result.Message,5);
        if(!result.Accepted)return;
        Life.ApplyCare(result.State);
        SetPaused(false);automaticActions=true;Pet.AllowCursorAttraction=QuietMode.IsChecked!=true;
        runningAction?.Stop();runningAction=new(result.Action);runningAction.Start(Pet);
        Pet.ShowCareProp(kind);
        careUntil=lifeClock.Elapsed.TotalSeconds+(kind==CareKind.Rest?90:kind==CareKind.Play?20:8);
        ShowHomeostasis();ShowCompanion();SavePetState();
    }
    private void FeedPet(object sender,RoutedEventArgs e)=>Care(CareKind.Feed);
    private void StrokePet(object sender,RoutedEventArgs e)=>Care(CareKind.Pet);
    private void PlayPet(object sender,RoutedEventArgs e)=>Care(CareKind.Play);
    private void GroomPet(object sender,RoutedEventArgs e)=>Care(CareKind.Groom);
    private void RestPet(object sender,RoutedEventArgs e)=>Care(CareKind.Rest);
    private void RenamePet(object sender,RoutedEventArgs e)
    {
        var name=PetName.Text.Trim();
        if(name.Length is <1 or >16||name.Any(char.IsControl)){CareStatus.Text="名字請使用 1～16 個字。";return;}
        Learning.State.Companion.Name=name;SavePetState();ShowCompanion();Pet.Say($"{name}，我喜歡這個名字！",5);
    }
    private void QuietChanged(object sender,RoutedEventArgs e)
    {
        if(Learning is null)return;
        settings=settings with{QuietCompanion=QuietMode.IsChecked==true};settingsStore.Save(settings);
        if(audio is not null){audio.Muted=settings.MuteAudio||settings.QuietCompanion;if(audio.Muted)audio.Stop();}
        Pet.AllowCursorAttraction=QuietMode.IsChecked!=true;
        automaticActions=true;runningAction=null;
        Pet.Say(QuietMode.IsChecked==true?"你忙，我在旁邊安靜陪你。":"一起玩吧！",4);
    }
    private void TickCompanion()
    {
        Pet.EmotionalState=Life.State;
        var away=LatestEnvironment?.IdleSeconds.Value is >300;
        if(wasAway&&LatestEnvironment?.IdleSeconds.Value is <=300&&automaticActions&&!aiPaused&&lifeClock.Elapsed.TotalSeconds>careUntil&&!Pet.Interacting)
        {runningAction=new(BodyAction.Greet);runningAction.Start(Pet);careUntil=lifeClock.Elapsed.TotalSeconds+6;Pet.Say("你回來啦！我有乖乖等你。",5);}
        wasAway=away;
        if(lifeClock.Elapsed.TotalSeconds-lastThought>100&&QuietMode.IsChecked!=true&&!aiPaused)
        {lastThought=lifeClock.Elapsed.TotalSeconds;Pet.Say(Life.State.Hunger>65?"肚子咕嚕咕嚕……":Life.State.Fatigue>75?"眼皮變重了……":Life.State.Loneliness>60?"可以陪我一下嗎？":"有你在，這裡就是我的家。",5);}
        ShowCompanion();
    }
    private void ShowCompanion()
    {
        if(!IsVisible)return;
        var c=Learning.State.Companion;
        CompanionTitle.Text=$"{c.Name}的小日子";
        BondStatus.Text=$"{c.Relationship} · 親密度 {c.Bond:F1} / 100 · {CompanionCare.Mood(Life.State)}";
        Memories.Text=c.Memories.Count==0?"第一頁還空著，從一次摸摸開始。":string.Join("\n",c.Memories.TakeLast(6).Reverse().Select(m=>$"{m.At.ToLocalTime():MM/dd HH:mm}  {m.Text}"));
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
        var before=Learning.State.Companion.CareCount;
        Care(CareKind.Feed);
        if(Learning.State.Companion.CareCount!=before+1||Pet.CurrentAction!=BodyAction.Eat)throw new Exception("Care UI did not feed pet.");
        if(!SavePetState()||!VerifyPetSave())throw new Exception("Companion persistence failed.");
        careUntil=0;
    }
}
