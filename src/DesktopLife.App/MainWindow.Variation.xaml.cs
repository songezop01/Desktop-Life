using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    private BehaviorParameters CreateParameters(BodyAction action)
    {
        var context=new EnvironmentContext(Life.State,personality,LatestEnvironment,ActionCatalog.Context(LatestEnvironment,DateTimeOffset.UtcNow));
        var output=currentOutput.Scores.Count>0?currentOutput:utilityBrain.Evaluate(context);
        var memory=Learning.State.Variation;var now=DateTimeOffset.UtcNow;
        BehaviorParameters? chosen=null;var best=-1d;
        for(var i=0;i<4;i++)
        {
            var candidate=BehaviorDecoder.Decode(action,context,output,Learning.State,Learning.LastReward,lifeClock.Elapsed.TotalSeconds,Random.Shared.Next());
            var score=Random.Shared.NextDouble()/(1+candidate.Drives.Novelty*memory.Repetition(candidate.Signature(action),now));
            if(score>best){best=score;chosen=candidate;}
        }
        memory.Remember(new(action,chosen!.Signature(action),null,now));return chosen;
    }
    private void ShowVariation()
    {
        var p=Pet.Parameters;var d=p.Drives;
        StyleStatus.Text=$"創意 {d.Creativity:F2} · 新奇 {d.Novelty:F2} · 對稱 {d.Symmetry:F2} · 複雜 {d.Complexity:F2}\n社交 {d.Social:F2} · 調皮 {d.Mischief:F2} · 愛玩 {d.Playfulness:F2} · 平靜 {d.Calmness:F2}\n"
            +$"繪畫：{p.Draw.StrokeCount} 組元素 · 複雜 {p.Draw.Complexity:F2} · 對稱 {p.Draw.Symmetry:F2} · {UiText.Label(p.Draw.Primary)} · 大小 {p.Draw.Size:F2}\n"
            +$"文字：{UiText.Label(p.Write.Emotion)}／{UiText.Label(p.Write.Intent)} · 長度 {p.Write.Length:F2} · 直接 {p.Write.Direct:F2} · 俏皮 {p.Write.Playful:F2}\n"
            +$"移動：{p.Movement.Speed:F0} 點／秒 · 距離 {p.Movement.Distance:F0} · 偏角落 {p.Movement.PreferredRegion:F2} · 游標距離 {p.Movement.CursorDistance:F0}\n"
            +$"社交：{UiText.Label(p.Social.Pattern)} · 親近 {p.Social.Affinity:F2} · 最近輸出 {Learning.State.Variation.RecentOutputHistory.Count}/64\n"
            +"已學畫風："+string.Join(" · ",Learning.State.Variation.DrawStylePreference.Select(k=>$"{UiText.Label(k.Key)} {k.Value:+0.000;-0.000;0}"))
            +"\n已學文風："+string.Join(" · ",Learning.State.Variation.TextStylePreference.Select(k=>$"{UiText.Label(k.Key)} {k.Value:+0.000;-0.000;0}"));
    }
    public void SmokeGenerative(string path)
    {
        var generated=CreateParameters(BodyAction.DrawDoodle);
        Pet.Art.Create(BodyAction.DrawDoodle,generated,Learning.State.Variation,20,20);
        Pet.Art.Create(BodyAction.WriteNote,CreateParameters(BodyAction.WriteNote),Learning.State.Variation,320,20);
        if(!Pet.Art.Works.Any(w=>w.Drawing is not null)||!Pet.Art.Works.Any(w=>w.Text is not null))throw new Exception("Generative output not created.");
        if(!SavePetState()||!VerifyPetSave())throw new Exception("Generative state save failed.");
        var restored=organismStore.Load();
        if(!restored.Learning.Artworks.Any(w=>w.Drawing is not null)||restored.Learning.Variation.RecentOutputHistory.Count==0)throw new Exception("Generative output/history did not survive persistence.");
        Pet.Art.RenderDiagnosticSheet(path,Enumerable.Range(0,6).Select(i=>BehaviorDecoder.Decode(BodyAction.DrawDoodle,
            new(Life.State,personality,null,LearningContext.QuietDesktop),new(new Dictionary<BehaviorDrive,double>{[BehaviorDrive.Create]=i/5d},[],[]),Learning.State,null,0,i+17)).ToArray());
    }
}
