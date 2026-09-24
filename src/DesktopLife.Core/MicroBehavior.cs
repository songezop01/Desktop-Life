namespace DesktopLife.Core;

public readonly record struct MicroPose(double Blink, double Ear, double Head, double Weight);

/// <summary>Runtime-only, irregular gestures driven by the existing render clock.</summary>
public sealed class MicroBehavior(int seed)
{
    private readonly Random random = new(seed);
    private double blinkWait = 2, gestureWait = 1, blinkAge = 1, gestureAge = 9;
    private double direction = 1;
    public MicroPose Step(double dt, PersonalityProfile personality, bool sleeping, bool contact)
    {
        if (!double.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        dt = Math.Min(dt, .1);
        blinkAge += dt; gestureAge += dt; blinkWait -= dt; gestureWait -= dt;
        if (blinkWait <= 0)
        {
            blinkAge = 0;
            blinkWait = 2.4 + random.NextDouble() * 5.5;
        }
        if (gestureWait <= 0)
        {
            gestureAge = 0; direction = random.Next(2) == 0 ? -1 : 1;
            gestureWait = (sleeping ? 12 : 3) + random.NextDouble() * (8 - 3 * Math.Clamp(personality.Curiosity, 0, 1));
        }
        var blink = sleeping ? 1 : blinkAge < .22 ? Math.Sin(blinkAge / .22 * Math.PI) : 0;
        var envelope = gestureAge < 1.4 ? Math.Sin(gestureAge / 1.4 * Math.PI) : 0;
        var ear = envelope * Math.Sin(gestureAge * 18) * (sleeping ? 2 : 7);
        return new(blink, ear, contact || sleeping ? 0 : direction * envelope * 2,
            contact || sleeping ? 0 : direction * envelope * .7);
    }
}
