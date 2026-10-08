using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class DisplayPreferenceTests
{
    private static readonly DisplayIdentity laptop=new("panel-A",@"\\.\DISPLAY1",true);
    private static readonly DisplayIdentity phone=new("spacedesk-client",@"\\.\DISPLAY6",false);
    [Fact]public void PreferredSpacedeskSurvivesWindowsDisplayNumberChange()
    {
        Assert.Equal(phone.Id,DisplayPreference.Select(phone.Id,[laptop,phone with{DeviceName=@"\\.\DISPLAY8"}]));
    }
    [Fact]public void OldSettingsResolveToStableHardwareIdentity()
    {
        Assert.Equal(phone.Id,DisplayPreference.Select(@"\\.\display6",[laptop,phone]));
    }
    [Fact]public void TemporaryDisconnectionFallsBackThenReconnectRestoresPreference()
    {
        var preferred=phone.Id;
        Assert.Equal(laptop.Id,DisplayPreference.Select(preferred,[laptop]));
        Assert.Equal(phone.Id,DisplayPreference.Select(preferred,[laptop,phone]));
    }
    [Fact]public void RemovedPrimaryFallsBackToRemainingDisplay()
    {
        Assert.Equal(phone.Id,DisplayPreference.Select(laptop.Id,[phone]));
    }
    [Fact]public void EmptyInventoryCannotBecomeAnInvalidWorkspace()
    {
        Assert.Throws<ArgumentException>(()=>DisplayPreference.Select(phone.Id,[]));
    }
}
