namespace DesktopLife.Core;

public enum BodyAction { Idle, Walk, Wander, Sit, Sleep, Stretch, ObserveCursor, ChaseCursor, AvoidCursor, ObserveDesktopIcon, PseudoPushIcon, RestInCorner, PlayToy, DrawDoodle, WriteNote, Hide, Explore, ObserveShortcut, PushShortcut, Eat, Groom, Nuzzle, Greet, Fall, BatToy }
public readonly record struct BodyBounds(double Left, double Top, double Width, double Height);
public sealed class DesktopBody
{
    public double X { get; private set; }
    public double Y { get; private set; }
    public BodyAction Action { get; set; }
    public double Direction { get; private set; } = 1;
    public const double Width = 116;
    public const double Height = 144;
    public void Reset(BodyBounds bounds)
    {
        X = bounds.Left + (bounds.Width - Width) / 2;
        Y = bounds.Top + bounds.Height - Height - 24;
        Clamp(bounds);
    }
    public void Update(double seconds, BodyBounds bounds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        seconds = Math.Min(seconds, 0.1); // Suspend/debugger must never teleport the pet.
        if (Action is BodyAction.Walk or BodyAction.Wander)
        {
            X += Direction * (Action == BodyAction.Walk ? 60 : 35) * seconds;
            if (X <= bounds.Left || X >= bounds.Left + Math.Max(0, bounds.Width - Width)) Direction *= -1;
        }
        Clamp(bounds);
    }
    public void MoveToward(double x,double y,double seconds,BodyBounds bounds,double speed=65)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(x));
        var dx=x-X;var dy=y-Y;var distance=Math.Sqrt(dx*dx+dy*dy);
        if(distance>0){var fraction=Math.Min(1,speed*Math.Min(seconds,.1)/distance);X+=dx*fraction;Y+=dy*fraction;}
        Clamp(bounds);
    }
    public void Place(double x,double y,BodyBounds bounds)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y))throw new ArgumentOutOfRangeException(nameof(x));
        X=x;Y=y;Clamp(bounds);
    }
    private void Clamp(BodyBounds bounds)
    {
        X = Math.Clamp(X, bounds.Left, bounds.Left + Math.Max(0, bounds.Width - Width));
        Y = Math.Clamp(Y, bounds.Top, bounds.Top + Math.Max(0, bounds.Height - Height));
    }
}
