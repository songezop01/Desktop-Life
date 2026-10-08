namespace DesktopLife.Core;

public sealed record DisplayIdentity(string Id,string DeviceName,bool Primary);

/// <summary>Preserves the preferred display across device-number changes and temporary disconnection.</summary>
public static class DisplayPreference
{
    public static string Select(string? preferred,IReadOnlyList<DisplayIdentity> connected)
    {
        if(connected.Count==0)throw new ArgumentException("At least one connected display is required.",nameof(connected));
        bool Match(string value)=>string.Equals(value,preferred,StringComparison.OrdinalIgnoreCase);
        // Versions through 0.10.1 stored \\.\DISPLAYn. Accept that value once, then persist the hardware identity.
        return (connected.FirstOrDefault(d=>Match(d.Id))??connected.FirstOrDefault(d=>Match(d.DeviceName))
            ??connected.FirstOrDefault(d=>d.Primary)??connected[0]).Id;
    }
}
