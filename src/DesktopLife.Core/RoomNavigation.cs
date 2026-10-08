namespace DesktopLife.Core;

public enum NavigationPhase { Idle,Approaching,Crouching,Airborne,Landing,Unreachable,Orienting,Recovering }
public readonly record struct RoomWaypoint(double TakeoffX,double LandingX,double LandingY,double SourceY,bool Drops);
public static class RoomNavigation
{
    public static double EscapeX(IReadOnlyList<RoomPlatform> platforms,BodyBounds bounds,double center,double feet,double halfWidth=58)
    {
        var cover=platforms.Where(p=>center>=p.X&&center<=p.X+p.Width&&p.HeightAt(center)<feet-8).ToArray();
        var min=bounds.Left+halfWidth;var max=bounds.Left+Math.Max(halfWidth,bounds.Width-halfWidth);
        var choices=cover.SelectMany(p=>new[]{p.X-halfWidth-10,p.X+p.Width+halfWidth+10}).Concat(new[]{center-150,center+150}).Where(x=>x>=min&&x<=max)
            .Where(x=>!cover.Any(p=>x>=p.X-halfWidth&&x<=p.X+p.Width+halfWidth)).OrderBy(x=>Math.Abs(x-center)).ToArray();
        return choices.Length>0?choices[0]:Math.Clamp(center<bounds.Left+bounds.Width/2?center+150:center-150,min,max);
    }
    public const double MaximumRise=175;
    private const double SupportSeparationEpsilon=.000001;
    // Drop takeoff points are ten pixels beyond an edge. A fourteen-pixel
    // tolerance starts falling while the body is still supported by the shelf.
    public static bool ReachedDropExit(RoomWaypoint step,double center)=>step.Drops&&Math.Abs(center-step.TakeoffX)<=2;
    public static IReadOnlyList<RoomWaypoint>? Plan(IReadOnlyList<RoomPlatform> furniture,BodyBounds bounds,double x,double feet,double goalX,double goalY,double halfWidth=58,double bodyHeight=144)
    {
        var nodes=furniture.Where(p=>p.Width>=24&&p.Y>=bounds.Top+bodyHeight).Append(new RoomPlatform(bounds.Left,bounds.Width,bounds.Top+bounds.Height,bounds.Top+bounds.Height)).ToArray();
        int Find(double px,double py)
        {
            var nearest=-1;var distance=4d;
            for(var i=0;i<nodes.Length;i++)
            {
                var p=nodes[i];if(px<p.X-2||px>p.X+p.Width+2)continue;
                var difference=Math.Abs(p.HeightAt(px)-py);
                if(difference<distance){nearest=i;distance=difference;}
            }
            return nearest;
        }
        var source=Find(x,feet);var destination=Find(goalX,goalY);
        if(source<0||destination<0)return null;
        if(source==destination)return [];
        var queue=new Queue<int>();queue.Enqueue(source);var parents=new Dictionary<int,(int Parent,RoomWaypoint Step)>{{source,(-1,default)}};
        while(queue.TryDequeue(out var current))
        {
            for(var next=0;next<nodes.Length;next++)
            {
                if(parents.ContainsKey(next)||!Edge(nodes[current],nodes[next],nodes,bounds,goalX,halfWidth,out var step))continue;
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
    private static bool Edge(RoomPlatform from,RoomPlatform to,IReadOnlyList<RoomPlatform> platforms,BodyBounds bounds,double goalX,double halfWidth,out RoomWaypoint step)
    {
        step=default;var margin=Math.Min(20,to.Width/3);
        var landingMin=Math.Max(to.X+margin,bounds.Left+halfWidth);var landingMax=Math.Min(to.X+to.Width-margin,bounds.Left+bounds.Width-halfWidth);
        var takeoffMin=Math.Max(from.X+Math.Min(10,from.Width/3),bounds.Left+halfWidth);var takeoffMax=Math.Min(from.X+from.Width-Math.Min(10,from.Width/3),bounds.Left+bounds.Width-halfWidth);
        if(landingMin>landingMax||takeoffMin>takeoffMax)return false;
        var landing=Math.Clamp(goalX,landingMin,landingMax);
        var takeoff=Math.Clamp(landing,takeoffMin,takeoffMax);
        var sourceY=from.HeightAt(takeoff);var destinationY=to.HeightAt(landing);var rise=sourceY-destinationY;
        if(rise>MaximumRise||rise< -420)return false;
        if(rise< -SupportSeparationEpsilon)
        {
            var left=from.X-10;var right=from.X+from.Width+10;
            var exits=new[]{left,right}.Where(x=>x>=bounds.Left+halfWidth&&x<=bounds.Left+bounds.Width-halfWidth).OrderBy(x=>Math.Abs(goalX-x)).ToArray();
            foreach(var exit in exits)
            {
                // Leave the edge, then fall vertically. Steering back toward a
                // distant goal in midair can land on a different lower shelf.
                // Such a shelf must be an explicit node in the route instead.
                if(exit<to.X+2||exit>to.X+to.Width-2)continue;
                var departureY=from.HeightAt(exit);var arrivalY=to.HeightAt(exit);
                if(arrivalY-departureY<=SupportSeparationEpsilon||arrivalY-departureY>420)continue;
                if(platforms.Any(p=>p!=to&&exit>=p.X&&exit<=p.X+p.Width&&p.HeightAt(exit)>=departureY-GravityBody.PlatformContactTolerance&&p.HeightAt(exit)<arrivalY-SupportSeparationEpsilon))continue;
                step=new(exit,exit,arrivalY,departureY,true);return true;
            }
            return false;
        }
        var jumpSpeed=Math.Sqrt(2*GravityBody.Gravity*(Math.Max(0,rise)+25));
        var flight=(jumpSpeed+Math.Sqrt(jumpSpeed*jumpSpeed-2*GravityBody.Gravity*rise))/GravityBody.Gravity;
        if(Math.Abs(landing-takeoff)>190*flight)return false;
        if(platforms.Any(p=>p!=to&&IntersectsDescendingJump(p,takeoff,sourceY,landing,destinationY,jumpSpeed,flight)))return false;
        step=new(takeoff,landing,destinationY,sourceY,false);return true;
    }
    private static bool IntersectsDescendingJump(RoomPlatform platform,double takeoff,double sourceY,double landing,double destinationY,double velocity,double flight)
    {
        // Gravity can still catch a support after the feet pass its plane by
        // five pixels. Check that entire descending contact window, including
        // a substep on either side, rather than a single analytic crossing.
        double Horizontal(double time)=>takeoff+(landing-takeoff)*(1-Math.Exp(-.18*time))/(1-Math.Exp(-.18*flight));
        double ContactTime(double extra)
        {
            var surface=platform.HeightAt(landing);var at=0d;
            for(var i=0;i<5;i++)
            {
                var discriminant=velocity*velocity-2*GravityBody.Gravity*(sourceY-surface-extra);
                if(discriminant<0)return double.NaN;
                at=(velocity+Math.Sqrt(discriminant))/GravityBody.Gravity;
                surface=platform.HeightAt(Horizontal(at));
            }
            return at;
        }
        var first=ContactTime(0);var last=ContactTime(GravityBody.PlatformContactTolerance);
        if(!double.IsFinite(first)||!double.IsFinite(last)||first>flight+GravityBody.MaximumSubstepSeconds)return false;
        var startX=Horizontal(Math.Max(velocity/GravityBody.Gravity,first-GravityBody.MaximumSubstepSeconds));
        var endX=Horizontal(Math.Min(flight+GravityBody.MaximumSubstepSeconds,last+GravityBody.MaximumSubstepSeconds));
        var minX=Math.Min(startX,endX);var maxX=Math.Max(startX,endX);
        return Math.Min(platform.Y,platform.EndY)<destinationY-1&&maxX>=platform.X-2&&minX<=platform.X+platform.Width+2;
    }
}
