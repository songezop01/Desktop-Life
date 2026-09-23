namespace DesktopLife.Core;

/// <summary>A damped pendulum. Coordinates are relative to its fixed string anchor.</summary>
public sealed class HangingToy
{
    public const double Length = 43;
    // The ball radius is 9 and its anchor is 45 from the furniture window edge.
    public const double MaximumAngle = .95;
    public double Angle { get; private set; } = .12;
    public double AngularVelocity { get; private set; }
    public double X => Math.Sin(Angle) * Length;
    public double Y => Math.Cos(Angle) * Length;

    public void Bat(double horizontalImpulse)
    {
        if (!double.IsFinite(horizontalImpulse)) throw new ArgumentOutOfRangeException(nameof(horizontalImpulse));
        AngularVelocity = Math.Clamp(AngularVelocity + horizontalImpulse / Length, -8, 8);
    }

    public void Step(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var count = Math.Max(1, (int)Math.Ceiling(Math.Min(seconds, .1) / .004));
        var dt = Math.Min(seconds, .1) / count;
        for (var i = 0; i < count; i++)
        {
            AngularVelocity += (-GravityBody.Gravity / Length * Math.Sin(Angle) - .75 * AngularVelocity) * dt;
            Angle += AngularVelocity * dt;
            if (Math.Abs(Angle) > MaximumAngle)
            {
                Angle = Math.CopySign(MaximumAngle, Angle);
                AngularVelocity *= -.35;
            }
        }
    }
}
