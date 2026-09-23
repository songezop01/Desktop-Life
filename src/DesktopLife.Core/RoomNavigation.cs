namespace DesktopLife.Core;

public enum NavigationPhase { Idle,Approaching,Crouching,Airborne,Landing,Unreachable }
public readonly record struct RoomWaypoint(double TakeoffX,double LandingX,double LandingY,double SourceY,bool Drops);
public static class RoomNavigation
{
    public const double MaximumRise=175;
    public static IReadOnlyList<RoomWaypoint>? Plan(IReadOnlyList<RoomPlatform> furniture,BodyBounds bounds,double x,double feet,double goalX,double goalY)
    {
        var nodes=furniture.Where(p=>p.Width>=24&&p.Y>=bounds.Top+DesktopBody.Height).Append(new RoomPlatform(bounds.Left,bounds.Width,bounds.Top+bounds.Height,bounds.Top+bounds.Height)).ToArray();
        int Find(double px,double py)=>Array.FindIndex(nodes,p=>px>=p.X-2&&px<=p.X+p.Width+2&&Math.Abs(p.HeightAt(px)-py)<8);
        var source=Find(x,feet);var destination=Find(goalX,goalY);
        if(source<0||destination<0)return null;
        if(source==destination)return [];
        var queue=new Queue<int>();queue.Enqueue(source);var parents=new Dictionary<int,(int Parent,RoomWaypoint Step)>{{source,(-1,default)}};
        while(queue.TryDequeue(out var current))
        {
            for(var next=0;next<nodes.Length;next++)
            {
                if(parents.ContainsKey(next)||!Edge(nodes[current],nodes[next],bounds,goalX,out var step))continue;
                parents[next]=(current,step);
                if(next==destination)
                {
                    var result=new List<RoomWaypoint>();var index=next;
                    while(index!=source){var item=parents[index];result.Add(item.Step);index=item.Parent;}
                    result.Reverse();return result;
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }
    private static bool Edge(RoomPlatform from,RoomPlatform to,BodyBounds bounds,double goalX,out RoomWaypoint step)
    {
        step=default;var margin=Math.Min(20,to.Width/3);
        var landing=Math.Clamp(goalX,to.X+margin,to.X+to.Width-margin);
        var takeoff=Math.Clamp(landing,from.X+Math.Min(10,from.Width/3),from.X+from.Width-Math.Min(10,from.Width/3));
        var sourceY=from.HeightAt(takeoff);var destinationY=to.HeightAt(landing);var rise=sourceY-destinationY;
        if(rise>MaximumRise||rise< -420)return false;
        if(rise< -8)
        {
            var left=from.X-10;var right=from.X+from.Width+10;
            var exits=new[]{left,right}.Where(x=>x>=bounds.Left+DesktopBody.Width/2&&x<=bounds.Left+bounds.Width-DesktopBody.Width/2).OrderBy(x=>Math.Abs(goalX-x)).ToArray();
            if(exits.Length==0)return false;
            takeoff=exits[0];
            if(takeoff<to.X-100||takeoff>to.X+to.Width+100)return false;
            step=new(takeoff,landing,destinationY,sourceY,true);return true;
        }
        var jumpSpeed=Math.Sqrt(2*GravityBody.Gravity*(Math.Max(0,rise)+25));
        var flight=(jumpSpeed+Math.Sqrt(jumpSpeed*jumpSpeed-2*GravityBody.Gravity*rise))/GravityBody.Gravity;
        if(Math.Abs(landing-takeoff)>190*flight)return false;
        step=new(takeoff,landing,destinationY,sourceY,false);return true;
    }
}
