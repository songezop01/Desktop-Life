using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    public void BeginDiagnostic(){Priorities.SelectedItem=DisplayPriority.Highest;SetManualAction(BodyAction.Idle);}
    private PersonalityProfile personality = new();
    private readonly ActionSelection selector = new(Random.Shared.Next());
    private readonly UtilityBrain utilityBrain = new();
    private double lastDebugUpdate=-1;
    private PetAction? runningAction;
    private bool automaticActions=true;
    private double careUntil;
    private BrainOutput currentOutput = new(new Dictionary<BehaviorDrive,double>(),[],[]);
    private void TickBrain(double elapsed)
    {
        if(Pet.Interacting)return;
        if((Life.State.Energy<=10||Life.State.Fatigue>=95)&&Pet.CurrentAction!=BodyAction.Sleep)
        {Pet.RequestAction(BodyAction.Sleep,BehaviorInterruptReason.CriticalNeed);return;}
        if(lifeClock.Elapsed.TotalSeconds<careUntil)return;
        if(Pet.SequenceCommitted)return;
        var environment=LatestEnvironment is {} fresh && DateTimeOffset.UtcNow-fresh.Timestamp<=TimeSpan.FromSeconds(3)?fresh:null;
        var context=new EnvironmentContext(Life.State,personality,environment,ActionCatalog.Context(environment,DateTimeOffset.UtcNow),Affordances:Pet.SenseAffordances());
        currentOutput=utilityBrain.Evaluate(context);
        var scores=currentOutput.Scores.ToDictionary(x=>x.Key,x=>x.Value);
        scores[BehaviorDrive.Interact]=Math.Clamp(personality.Social*.5+Learning.State.Companion.Bond/200+Life.State.Loneliness/300,0,1);
        currentOutput=currentOutput with{Scores=scores};
        runningAction?.Update(TimeSpan.FromSeconds(Math.Clamp(elapsed,0,1)));
        if(Pet.FinishingMotion)return;
        var minimum=Pet.CurrentAction==BodyAction.Sleep?45:Pet.CurrentAction is BodyAction.Sit or BodyAction.RestInCorner?20:10;
        var decision=selector.Select(context,currentOutput,Learning,Pet.CurrentAction,!automaticActions || runningAction?.ElapsedSeconds<minimum);
        if(automaticActions && (runningAction is null || runningAction.ElapsedSeconds>=minimum || decision.Reason.StartsWith("生理安全")))
        {
            var action=decision.Action??BodyAction.Idle;
            if(QuietMode.IsChecked==true && action is not (BodyAction.Sleep or BodyAction.Sit or BodyAction.Groom or BodyAction.Stretch))action=BodyAction.Sit;
            runningAction?.Stop();runningAction=new(action);runningAction.Start(Pet);
        }
        if(!IsVisible || lifeClock.Elapsed.TotalSeconds-lastDebugUpdate<1)return;
        lastDebugUpdate=lifeClock.Elapsed.TotalSeconds;ShowVariation();
        BrainStatus.Text=$"現在：{UiText.Label(Pet.CurrentAction)} · {CompanionCare.Mood(Life.State)}";
        PersonalityStatus.Text=$"好奇 {personality.Curiosity:P0} · 愛玩 {personality.Playfulness:P0} · 親人 {personality.Social:P0} · 獨立 {personality.Independence:P0}";
        ActionScores.Text="偏好的活動："+string.Join("、",decision.Scores.OrderByDescending(s=>s.FinalScore).Take(3).Select(s=>UiText.Label(s.Action)));
    }
    private void SetManualAction(BodyAction action)
    { automaticActions=false;Pet.AllowCursorAttraction=false;runningAction?.Stop();runningAction=new(action);runningAction.Start(Pet); }
}
