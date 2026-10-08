using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    private PersonalityProfile effectivePersonality=new();
    private double lastIdentityUpdate=-60;
    private void RefreshIdentity()
    {
        var now=DateTimeOffset.UtcNow;Learning.State.Adaptation.Advance(now);
        effectivePersonality=Learning.State.Adaptation.Effective(personality);Pet.BehaviorPersonality=effectivePersonality;
    }
    private void TickIdentity()
    {
        if(lifeClock.Elapsed.TotalSeconds-lastIdentityUpdate<60)return;lastIdentityUpdate=lifeClock.Elapsed.TotalSeconds;
        if(QuietMode.IsChecked==true&&LatestEnvironment is {} env&&DateTimeOffset.UtcNow-env.Timestamp<TimeSpan.FromSeconds(3)&&env.IdleSeconds.Value<300)
            Learning.State.Adaptation.Observe(AdaptiveTrait.Independence,DateTimeOffset.UtcNow);
        RefreshIdentity();
    }
    private void RecordCompletedBehavior(CompletedBehavior completed)
    {
        var now=DateTimeOffset.UtcNow;var state=Learning.State;
        state.Transitions.Complete(completed.Behavior,now,completed.Successful);
        if(!completed.Successful)return;
        if(completed.Behavior==LifeBehavior.Social&&completed.Autonomous&&completed.NearUser&&state.Companion.Bond>=65)
            state.Milestones.Record(MilestoneKind.AutonomousNuzzle,state.Companion,now);
        if(state.Transitions.Pairs.Any(p=>p.Count>=8&&p.Weight>=.15))state.Milestones.Record(MilestoneKind.TransitionHabit,state.Companion,now);
    }
    private void RecordFurnitureUse(FurnitureUsed use)
    {
        var state=Learning.State;var now=DateTimeOffset.UtcNow;
        if(use.Novel){state.Adaptation.Observe(AdaptiveTrait.Curiosity,now);RefreshIdentity();}
        if(use.Use==FurnitureUse.Sleep&&use.Item.Kind==FurnitureKind.Box)state.Milestones.Record(MilestoneKind.BoxSleep,state.Companion,now);
        else if(use.Use==FurnitureUse.Scratch)state.Milestones.Record(MilestoneKind.ScratcherUse,state.Companion,now);
        else if(use.Use==FurnitureUse.Sleep&&use.Autonomous)state.Milestones.Record(MilestoneKind.FurnitureSleep,state.Companion,now);
        if(use.Familiarity>=.8)state.Milestones.Record(MilestoneKind.FamiliarFurniture,state.Companion,now);
    }
    private void RecordCareEvidence(CareKind kind)
    {
        var now=DateTimeOffset.UtcNow;
        var companion=Learning.State.Companion;
        if(!companion.Memories.Any(m=>m.Kind?.StartsWith("milestone:",StringComparison.Ordinal)!=true&&now-m.At<TimeSpan.FromMinutes(30)))
            companion.Remember(now,CharacterCapability.CareDescription(kind,settings.PetAppearance));
        if(kind==CareKind.Play)Learning.State.Adaptation.Observe(AdaptiveTrait.Playfulness,now);
        if(kind==CareKind.Pet)Learning.State.Adaptation.Observe(AdaptiveTrait.Social,now);
        if(kind==CareKind.Feed)Learning.State.Transitions.Complete(LifeBehavior.Eat,now);
        RefreshIdentity();
    }
    private static string BehaviorName(LifeBehavior behavior)=>behavior switch
    {LifeBehavior.Wake=>"睡醒",LifeBehavior.Play=>"玩耍",LifeBehavior.Groom=>"整理自己",LifeBehavior.Explore=>"探索",LifeBehavior.Social=>"親近",LifeBehavior.Scratch=>"抓板／伸展",LifeBehavior.Rest=>"休息",LifeBehavior.Eat=>"吃點心",_=>"觀察"};
    private string IdentityDescription()
    {
        var learning=SelectedLearning;
        var a=learning.Adaptation;
        if(a.Offset(AdaptiveTrait.Social)>.008)return "牠漸漸更喜歡和你相處。";
        if(a.Offset(AdaptiveTrait.Playfulness)>.008)return "牠漸漸更喜歡一起玩耍。";
        if(a.Offset(AdaptiveTrait.Curiosity)>.008)return "牠漸漸更願意探索身邊的新東西。";
        if(a.Offset(AdaptiveTrait.Independence)>.008)return "牠漸漸習慣在你身邊安靜做自己的事。";
        var habit=learning.LocationHabits.Where(h=>h.Familiarity>=.64).MaxBy(h=>h.Preference);
        var item=habit is null?null:Pet.Furniture.FirstOrDefault(f=>f.Item.Id==habit.FurnitureId);
        return item is null?"牠正在慢慢形成自己的生活習慣。":$"牠已經逐漸熟悉房間裡的{UiText.Label(item.Item.Kind)}。";
    }
    private string IdentityDiagnostics()
    {
        static string Profile(PersonalityProfile p)=>$"好奇 {p.Curiosity:F3}／愛玩 {p.Playfulness:F3}／親人 {p.Social:F3}／創意 {p.Creativity:F3}／調皮 {p.Mischief:F3}／獨立 {p.Independence:F3}／膽怯 {p.Timidity:F3}／慵懶 {p.Laziness:F3}";
        var state=Learning.State;var a=state.Adaptation;
        var offsets=$"好奇 {a.Offset(AdaptiveTrait.Curiosity):+0.000;-0.000;0}／愛玩 {a.Offset(AdaptiveTrait.Playfulness):+0.000;-0.000;0}／親人 {a.Offset(AdaptiveTrait.Social):+0.000;-0.000;0}／獨立 {a.Offset(AdaptiveTrait.Independence):+0.000;-0.000;0}";
        var transitions=string.Join("、",state.Transitions.Pairs.OrderByDescending(p=>p.Weight).Take(4).Select(p=>$"{BehaviorName(p.Previous)}→{BehaviorName(p.Next)} {p.Weight:F2}（{p.Count}次）"));
        var furniture=string.Join("、",state.LocationHabits.OrderByDescending(h=>h.Familiarity).Take(3).Select(h=>$"{h.FurnitureId.ToString()[..8]} 熟悉 {h.Familiarity:F2}／偏好 {h.Preference:F2}"));
        var capability=settings.PetAppearance switch
        {
            PetAppearance.Cat=>"貓：喵叫／呼嚕／抓板／理毛；不寫字、不畫畫、不說人話",
            PetAppearance.Girl=>"女孩：說話／便條／模板塗鴉；不使用貓專屬表現",
            PetAppearance.BorderCollie=>"邊牧犬：玩球／親近／休息／梳理；不寫字、不畫畫、不使用貓專屬表現",
            _=>"角色能力"
        };
        return $"原始人格：{Profile(personality)}\n學習偏移：{offsets}\n目前人格：{Profile(effectivePersonality)}\n活動銜接：{transitions}\n家具習慣：{furniture}\n{capability}\n最近里程碑原因：{MilestoneReason(state.Milestones.LastReason)}";
    }

    private static string MilestoneReason(string? reason)=>reason switch
    {"BoxSleep"=>"箱內入睡","ScratcherUse"=>"實際使用抓板","FurnitureSleep"=>"自主選家具入睡","FamiliarFurniture"=>"家具熟悉度達標","AutonomousNuzzle"=>"高親密度且在游標附近自主蹭蹭","TransitionHabit"=>"活動銜接重複達標",_=>"尚無"};

}
