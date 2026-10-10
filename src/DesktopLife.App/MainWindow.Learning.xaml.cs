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
    private double lastLearningDisplayTick=-1;
    private void InitializeLearning(string dataRoot, AppSettings configuration, LearningState savedLearning)
    {
        settings=configuration with{BrainMode=BrainMode.Utility};
        Learning=new(savedLearning);

        var previousWorkArea=DisplayWorkspace.Bounds;
        DisplayWorkspace.Select(settings.ActiveDisplay);
        Pet.RemapWorkspace(previousWorkArea,DisplayWorkspace.Bounds);
        Pet.AttachHabits(Learning.State.LocationHabits);
        Pet.BehaviorCompleted+=RecordCompletedBehavior;
        Pet.FurnitureUseCompleted+=RecordFurnitureUse;
        Pet.RestoreRoom(Learning.State.Room,previousWorkArea);
        Pet.Art.RestoreWorks(Learning.State.Artworks);
        Pet.ParameterFactory=CreateParameters;
        RefreshIdentity();Pet.Bond=Learning.State.Companion.Bond;
        Pet.CreativeAction+=(action,x,y)=>
        {
            Pet.HasCreativeOutput=Pet.Art.Create(action,Pet.Parameters,Learning.State.Variation,x,y,settings.PetAppearance);
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
        LearningStatus.IsVisibleChanged+=(_,_)=>{if(LearningStatus.IsVisible)ShowLearning(force:true);};
        StyleStatus.IsVisibleChanged+=(_,_)=>{if(StyleStatus.IsVisible)ShowVariation();};
        Loaded += (_,_) => {if(!verificationFrozen)learningTimer.Start();};
        Closed += (_,_) => learningTimer.Stop();
        ShowLearning(force:true);
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
        ShowLearning(force:true);
    }
    private void ShowLearning(bool force=false)
    {
        if(!IsVisible||!LearningStatus.IsVisible){performance.SkippedDiagnosticRefreshes++;return;}
        var now=lifeClock.Elapsed.TotalSeconds;
        if(!force&&now-lastLearningDisplayTick<1){performance.SkippedDiagnosticRefreshes++;return;}
        lastLearningDisplayTick=now;performance.LearningDisplayRefreshes++;
        var reward=Learning.LastReward;
        LearningStatus.Text=$"最近獎勵：{(reward is null ? "尚無" : $"{UiText.Label(reward.Button)} {reward.Amount:+0.##;-0.##}")} · 回溯 {Learning.Trace.Credits(lifeClock.Elapsed.TotalSeconds).Count} 樣本 / 5 秒";
        PreferenceStatus.Text="行為偏好："+string.Join(" · ",Learning.State.ActionPreference.OrderByDescending(p=>p.Value).Select(p=>$"{UiText.Label(p.Key)} {p.Value:+0.000;-0.000;0}"))
            +"\n類別："+string.Join(" · ",Learning.State.CategoryPreference.Select(p=>$"{UiText.Label(p.Key)} {p.Value:+0.000;-0.000;0}"))
            +"\n情境："+string.Join(" · ",Learning.State.ContextAssociation.OrderByDescending(p=>Math.Abs(p.Value)).Take(4).Select(p=>$"{UiText.Label(p.Key)} {p.Value:+0.000;-0.000;0}"));
    }
    public bool SmokeReward()
    {
        var originalPresence=CurrentPresence;var originalProfile=careTarget;var originalPause=aiPaused;var originalHidden=userHidden;
        var controllerVisible=IsVisible;var primary=settings.PetAppearance;
        var positions=careKinds.ToDictionary(kind=>kind,kind=>CharacterWindow(kind).Position);
        Hide();
        try
        {
            userHidden=false;PresenceOptions.SelectedIndex=(int)PresenceMode.All;UpdatePetVisibility();SetPaused(false);
            foreach(var kind in careKinds)PrepareDirectCareSubset(kind);
            var beforeClick=DirectCareFingerprint(primary);
            // Transparent pixels and painted-character clicks are no longer care
            // or reward buttons. Both must leave learning and needs untouched.
            Pet.SmokeClickAt(new System.Windows.Point(0,Pet.Height-2),System.Windows.Input.MouseButton.Left);
            Pet.SmokeClickAt(Pet.DiagnosticHitPoint(),System.Windows.Input.MouseButton.Left);
            if(DirectCareFingerprint(primary)!=beforeClick)throw new Exception("Legacy character click still grants a reward or care");
            PrepareDirectCareSubset(primary);
            Learning.State.Companion.LastCare.Remove(CareKind.Pet);
            var point=Pet.DirectHeadDiagnosticPoint();var stationary=DirectCareFingerprint(primary);
            for(var i=0;i<20;i++)Pet.SampleDirectCareDiagnostic(point,.033);
            if(!Pet.HoverHandVisible||DirectCareFingerprint(primary)!=stationary)
                throw new Exception("Stationary hover granted a learning reward or lost the hand");
            Pet.SampleDirectCareDiagnostic(null,.033);
            var before=Learning.State.PositiveRewards;var careBefore=Learning.State.Companion.CareCount;
            var preference=Learning.State.ActionPreference.GetValueOrDefault(BodyAction.Sit);
            var others=careKinds.Where(kind=>kind!=primary).ToDictionary(kind=>kind,DirectCareFingerprint);
            Learning.Trace.Clear();Learning.Trace.Observe(new(lifeClock.Elapsed.TotalSeconds,BodyAction.Sit,ActionCategory.Rest,
                LearningContext.UserNearby,1,[],[]));
            SampleDirectCareStroke(primary,point);
            if(Learning.State.PositiveRewards!=before+1||Learning.State.Companion.CareCount!=careBefore+1
                ||Learning.LastCredits.Count==0||Learning.State.ActionPreference.GetValueOrDefault(BodyAction.Sit)<=preference)
                throw new Exception($"Actual stroke did not reinforce a real eligibility trace: positive {before}->{Learning.State.PositiveRewards}, care {careBefore}->{Learning.State.Companion.CareCount}, credits {Learning.LastCredits.Count}");
            if(others.Any(pair=>DirectCareFingerprint(pair.Key)!=pair.Value))throw new Exception("Primary stroke rewarded another resident");
            var accepted=DirectCareFingerprint(primary);
            Pet.SampleDirectCareDiagnostic(null,.033);SampleDirectCareStroke(primary,point);
            if(DirectCareFingerprint(primary)!=accepted)throw new Exception("Cooldown stroke granted another learning reward");
            if(!SavePetState()||!VerifyPetSave())throw new Exception("Accepted physical-contact learning did not persist");
            return true;
        }
        finally
        {
            foreach(var kind in careKinds)
            {var actor=CharacterWindow(kind);actor.CancelDirectCare();actor.ResetPosition();actor.RestorePosition(positions[kind]);actor.SetSimulationEnabled(!verificationFrozen);}
            userHidden=originalHidden;PresenceOptions.SelectedIndex=(int)originalPresence;UpdatePetVisibility();SelectCare(originalProfile);SetPaused(originalPause);
            if(controllerVisible)Show();
        }
    }
}
