namespace DesktopLife.Core;

/// <summary>A damped pendulum. Coordinates are relative to its fixed string anchor.</summary>
public sealed class HangingToy
{
    public const double Length = 43;
    public const double MaximumLength = 160;
    public const double MaximumHorizontalTravel = 35;
    public double RopeLength { get; private set; } = Length;
    public double TargetLength { get; private set; } = Length;
    // The ball radius is 9 and its anchor is 45 from the furniture window edge.
    public const double MaximumAngle = .95;
    public double Angle { get; private set; } = .12;
    public double AngularVelocity { get; private set; }
    public bool IsResting => Angle == 0 && AngularVelocity == 0 && RopeLength == TargetLength;
    public double X => Math.Sin(Angle) * RopeLength;
    public double Y => Math.Cos(Angle) * RopeLength;

    public void SetLength(double length)
    {
        if (!double.IsFinite(length) || length < Length || length > MaximumLength)
            throw new ArgumentOutOfRangeException(nameof(length));
        TargetLength = length;
    }

    public void Bat(double horizontalImpulse)
    {
        if (!double.IsFinite(horizontalImpulse)) throw new ArgumentOutOfRangeException(nameof(horizontalImpulse));
        AngularVelocity = Math.Clamp(AngularVelocity + horizontalImpulse / RopeLength, -8, 8);
    }

    public void Step(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if(IsResting)return;
        if(Math.Abs(Angle)<.0002&&Math.Abs(AngularVelocity)<.0002)Angle=AngularVelocity=0;
        var count = Math.Max(1, (int)Math.Ceiling(Math.Min(seconds, .1) / .004));
        var dt = Math.Min(seconds, .1) / count;
        for (var i = 0; i < count; i++)
        {
            // The adjustable string retracts visibly, including after cancellation.
            RopeLength += Math.Clamp(TargetLength - RopeLength, -70 * dt, 70 * dt);
            if(Math.Abs(TargetLength-RopeLength)<.000001)RopeLength=TargetLength;
            AngularVelocity += (-GravityBody.Gravity / RopeLength * Math.Sin(Angle) - .75 * AngularVelocity) * dt;
            Angle += AngularVelocity * dt;
            var angleLimit = Math.Min(MaximumAngle, Math.Asin(MaximumHorizontalTravel / RopeLength));
            if (Math.Abs(Angle) > angleLimit)
            {
                Angle = Math.CopySign(angleLimit, Angle);
                AngularVelocity *= -.35;
            }
        }
    }
}
