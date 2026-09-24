using System.IO;
using System.Windows.Threading;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    public RewardLearning Learning { get; private set; } = null!;
    private AppSettings settings = null!;
    private SettingsStore settingsStore = null!;
    private readonly DispatcherTimer learningTimer = new() { Interval=TimeSpan.FromMilliseconds(250) };
    private double lastLearningTick;
    private void InitializeLearning(string dataRoot, AppSettings configuration, LearningState savedLearning)
    {
        settings=configuration with{BrainMode=BrainMode.Utility};
        Learning=new(savedLearning);

        var previousWorkArea=DisplayWorkspace.Bounds;
        DisplayWorkspace.Select(settings.ActiveDisplay);
        Pet.RemapWorkspace(previousWorkArea,DisplayWorkspace.Bounds);
        Pet.RestoreRoom(Learning.State.Room,previousWorkArea);
        Pet.Art.RestoreWorks(Learning.State.Artworks);
        Pet.ParameterFactory=CreateParameters;
        Pet.BehaviorPersonality=personality;Pet.Bond=Learning.State.Companion.Bond;
        Pet.CreativeAction+=(action,x,y)=>
        {
            Pet.HasCreativeOutput=Pet.Art.Create(action,Pet.Parameters,Learning.State.Variation,x,y);
            if(!Pet.HasCreativeOutput)HitStatus.Text="作品已滿且全數保留；清除部分作品後可繼續創作。";
        };
        settingsStore=new(Path.Combine(dataRoot,"settings.json"));
        InitializePresentation();
        Pet.Hit += ApplyReward;
        Pet.InteractionRequested+=action=>
        {
            if(aiPaused||Pet.Interacting)return;
            if(!Pet.RequestAction(action,BehaviorInterruptReason.Stimulus))return;
            runningAction?.Stop();runningAction=null;Learning.Trace.Clear();
        };
        learningTimer.Tick += (_,_) => ObserveAction();
        Loaded += (_,_) => learningTimer.Start();
        Closed += (_,_) => learningTimer.Stop();
        ShowLearning();
    }
    private void ObserveAction()
    {
        var now=lifeClock.Elapsed.TotalSeconds;
        var gap=now-lastLearningTick; lastLearningTick=now;
        if (gap>5 || suspendedAt is not null || !Pet.IsVisible || aiPaused || Pet.Interacting) { Learning.Trace.Clear(); return; }
        Learning.Decay(Math.Max(0,gap));
        TickBrain(gap);
        Learning.Trace.Observe(new(now,Pet.CurrentAction,ActionCatalog.Category(Pet.CurrentAction),
            ActionCatalog.Context(LatestEnvironment,DateTimeOffset.UtcNow),1,currentOutput.Activity,currentOutput.Outputs,null,Pet.CurrentAction is BodyAction.DrawDoodle or BodyAction.WriteNote && !Pet.HasCreativeOutput?null:Pet.Parameters));
        ShowLearning();
    }
    private void ApplyReward(RewardButton button)
    {
        if (!Pet.IsVisible || suspendedAt is not null || aiPaused) return;
        var credits=Learning.Reward(button,lifeClock.Elapsed.TotalSeconds,settings);
        HitStatus.Text=credits.Count==0?"牠感覺到你在身邊。":"牠記住了這次溫柔的互動。";
        if(button!=RewardButton.Middle)Care(CareKind.Pet);
        else Pet.Say("好，我先自己玩一下。",3);
        ShowLearning();
    }
    private void ShowLearning()
    {
        if(!IsVisible)return;
        var reward=Learning.LastReward;
        LearningStatus.Text=$"最近獎勵：{(reward is null ? "尚無" : $"{UiText.Label(reward.Button)} {reward.Amount:+0.##;-0.##}")} · 回溯 {Learning.Trace.Credits(lifeClock.Elapsed.TotalSeconds).Count} 樣本 / 5 秒";
        PreferenceStatus.Text="行為偏好："+string.Join(" · ",Learning.State.ActionPreference.OrderByDescending(p=>p.Value).Select(p=>$"{UiText.Label(p.Key)} {p.Value:+0.000;-0.000;0}"))
            +"\n類別："+string.Join(" · ",Learning.State.CategoryPreference.Select(p=>$"{UiText.Label(p.Key)} {p.Value:+0.000;-0.000;0}"))
            +"\n情境："+string.Join(" · ",Learning.State.ContextAssociation.OrderByDescending(p=>Math.Abs(p.Value)).Take(4).Select(p=>$"{UiText.Label(p.Key)} {p.Value:+0.000;-0.000;0}"));
    }
    public bool SmokeReward()
    {
        var before=Learning.State.PositiveRewards;
        var careBefore=Learning.State.Companion.CareCount;
        Pet.SmokeClickAt(new System.Windows.Point(0,140),System.Windows.Input.MouseButton.Left);
        if (Learning.State.PositiveRewards!=before) return false;
        Pet.SmokeClickAt(new System.Windows.Point(60,112),System.Windows.Input.MouseButton.Left);
        var passed=Learning.State.PositiveRewards==before+1 && Learning.State.ActionPreference.Values.Any(v=>v>0)
            && Learning.State.Companion.CareCount==careBefore+1 && Pet.CurrentAction==BodyAction.Nuzzle
;
        if(!passed)throw new Exception($"Reward diagnostic: positive {before}->{Learning.State.PositiveRewards}, visible {Pet.IsVisible}, trace {Learning.LastCredits.Count}, action {Pet.CurrentAction}, drag {Pet.DragDiagnostic}, target {Pet.InputHitTest(new System.Windows.Point(60,112))?.GetType().Name}; {HitStatus.Text}");
        return true;
    }
}
