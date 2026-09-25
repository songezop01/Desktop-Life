namespace DesktopLife.Core;
public sealed record RestSpot(string Id,FurnitureKind? Kind,double X,double Feet);
public sealed class RestSpotPreference
{
    public List<LocationHabit> Habits {get;private set;}=[];
    public void Attach(List<LocationHabit> habits){Habits=habits;Sanitize();}
    public void Sanitize()
    {var good=Habits.Where(h=>h is {Valid:true}).DistinctBy(h=>(h.FurnitureId,h.Use)).Take(32).ToArray();Habits.Clear();Habits.AddRange(good);}
    public void Prune(IEnumerable<Guid> ids){var live=ids.ToHashSet();Habits.RemoveAll(h=>!live.Contains(h.FurnitureId));}
    public void Forget(){Habits.Clear();recent.Clear();}
    public double Familiarity(Guid id)=>Habits.Where(h=>h.FurnitureId==id).Select(h=>h.Familiarity).DefaultIfEmpty().Max();
    public void Learn(Guid id,FurnitureUse use,DateTimeOffset now,double quality=1)
    {
        if(id==Guid.Empty||!Enum.IsDefined(use)||use==FurnitureUse.None||!double.IsFinite(quality))return;
        var old=Habits.FirstOrDefault(h=>h.FurnitureId==id&&h.Use==use);
        if(old is not null)Habits.Remove(old);
        if(Habits.Count>=32)Habits.Remove(Habits.MinBy(h=>h.LastUsedMinute)!);
        Habits.Add(new(id,use,Math.Min(1,(old?.Familiarity??0)+.16),Math.Clamp((old?.Preference??0)+.08*Math.Clamp(quality,0,1),0,1),Math.Min(10000,(old?.Uses??0)+1),Math.Max(0,now.ToUnixTimeSeconds()/60)));
    }
    public double HabitWeight(Guid id,DateTimeOffset now,PersonalityProfile p)
    {
        var h=Habits.Where(h=>h.FurnitureId==id).MaxBy(h=>h.Preference);
        if(h is null)return .22*p.Curiosity;
        var minutes=Math.Max(0,now.ToUnixTimeSeconds()/60d-h.LastUsedMinute);
        return h.Preference*Math.Exp(-minutes/(7*1440d))*.65+.22*p.Curiosity*(1-h.Familiarity)-.85*Math.Exp(-minutes/3);
    }
    public RestSpot? ChooseHome(IEnumerable<RestSpot> candidates,PersonalityProfile p,double bond,double petX,double? cursorX,DateTimeOffset now,double fatigue,Random random)
    {
        var choices=candidates.ToArray();if(choices.Length==0)return null;
        // Urgent rest chooses among nearby reachable alternatives before soft weighting.
        if(fatigue>=85){var nearest=choices.Min(s=>Math.Abs(s.X-petX));choices=choices.Where(s=>Math.Abs(s.X-petX)<=nearest+220).ToArray();}
        double Weight(RestSpot s)
        {
            var hasId=Guid.TryParse(s.Id.Split(':')[0],out var id);
            var suitability=s.Kind is FurnitureKind.PetBed or FurnitureKind.Cushion?1:s.Kind==FurnitureKind.Box?.85:s.Kind is null?.45:.7;
            var score=suitability+(hasId?HabitWeight(id,now,p):0)-Math.Min(2,Math.Abs(s.X-petX)/700)*(.4+p.Laziness*.3)
                +(cursorX is {} x?(1-Math.Min(1,Math.Abs(s.X-x)/600))*Math.Clamp(bond/100,0,1)*p.Social*.2:0);
            return Math.Exp(Math.Clamp(score*2,-6,4));
        }
        var weights=choices.Select(Weight).ToArray();var draw=random.NextDouble()*weights.Sum();
        for(var i=0;i<choices.Length;i++){draw-=weights[i];if(draw<=0)return choices[i];}return choices[^1];
    }
    private readonly Dictionary<string,double> recent=[];
    public void Used(string id){if(recent.Count>=32&&!recent.ContainsKey(id))recent.Remove(recent.Keys.First());recent[id]=Math.Min(1,recent.GetValueOrDefault(id)+.2);}
    public RestSpot? Choose(IEnumerable<RestSpot> candidates,PersonalityProfile p,double bond,double petX,double? cursorX)
    {
        double Score(RestSpot s)=>(s.Kind is FurnitureKind.Cushion or FurnitureKind.PetBed?1:s.Kind==FurnitureKind.CatTree?.7:s.Kind is null?.4:.5)
            +recent.GetValueOrDefault(s.Id)*.3
            +(s.Kind is null?p.Timidity*.25:p.Curiosity*.15)
            -Math.Min(1,Math.Abs(s.X-petX)/1200)*(.15+p.Laziness*.2)
            +(cursorX is {} x?(1-Math.Min(1,Math.Abs(s.X-x)/600))*Math.Clamp(bond/100,0,1)*p.Social*.25:0);
        return candidates.OrderByDescending(Score).ThenBy(s=>s.Id,StringComparer.Ordinal).FirstOrDefault();
    }
}
