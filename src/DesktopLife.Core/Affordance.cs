namespace DesktopLife.Core;
public sealed record DesktopObject(string Id,string Kind,double X,double Y);
public interface IDesktopObjectProvider { IReadOnlyList<DesktopObject> GetObjects(BodyBounds bounds); }
public sealed class VirtualDesktopObjectProvider : IDesktopObjectProvider
{
    public IReadOnlyList<DesktopObject> GetObjects(BodyBounds bounds)=>[
        new("virtual-icon","Icon",bounds.Left+bounds.Width*.30,bounds.Top+bounds.Height-70),
        new("virtual-toy","Toy",bounds.Left+bounds.Width*.70,bounds.Top+bounds.Height-60)];
}
public sealed record Affordances(bool CursorNearby,bool HasIcon,bool HasToy,bool HasSpace,bool HasShortcut=false);
public sealed record ShortcutTarget(string Id,double X,double Y);
public static class AffordanceProvider
{
    public static bool Allows(BodyAction action,Affordances affordances)=>action switch
    {
        BodyAction.ObserveCursor or BodyAction.ChaseCursor or BodyAction.AvoidCursor=>affordances.CursorNearby,
        BodyAction.ObserveDesktopIcon or BodyAction.PseudoPushIcon=>affordances.HasIcon,
        BodyAction.PlayToy=>affordances.HasToy,
        BodyAction.ObserveShortcut or BodyAction.PushShortcut=>affordances.HasShortcut,
        BodyAction.DrawDoodle or BodyAction.WriteNote=>affordances.HasSpace,
        _=>true
    };
}
