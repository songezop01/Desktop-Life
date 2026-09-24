namespace DesktopLife.Core;
public sealed record RestSpot(string Id,FurnitureKind? Kind,double X,double Feet);
public sealed class RestSpotPreference
{
    private readonly Dictionary<string,double> recent=[];
    public void Used(string id){if(recent.Count>=32&&!recent.ContainsKey(id))recent.Remove(recent.Keys.First());recent[id]=Math.Min(1,recent.GetValueOrDefault(id)+.2);}
    public RestSpot? Choose(IEnumerable<RestSpot> candidates,PersonalityProfile p,double bond,double petX,double? cursorX)
    {
        double Score(RestSpot s)=>(s.Kind==FurnitureKind.Cushion?1:s.Kind==FurnitureKind.CatTree?.7:s.Kind is null?.4:.5)
            +recent.GetValueOrDefault(s.Id)*.3
            +(s.Kind is null?p.Timidity*.25:p.Curiosity*.15)
            -Math.Min(1,Math.Abs(s.X-petX)/1200)*(.15+p.Laziness*.2)
            +(cursorX is {} x?(1-Math.Min(1,Math.Abs(s.X-x)/600))*Math.Clamp(bond/100,0,1)*p.Social*.25:0);
        return candidates.OrderByDescending(Score).ThenBy(s=>s.Id,StringComparer.Ordinal).FirstOrDefault();
    }
}
