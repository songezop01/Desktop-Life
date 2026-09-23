using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class FullscreenTests
{
    [Fact] public void BorderlessCoveringMonitorHidesPet()=>Assert.True(FullscreenPolicy.IsFullscreen(new(0,0,1920,1080),new(0,0,1920,1080),false,false));
    [Fact] public void NormalMaximizedBrowserDoesNotHide()=>Assert.False(FullscreenPolicy.IsFullscreen(new(0,0,1920,1080),new(0,0,1920,1080),true,false));
    [Fact] public void DesktopAndOwnWindowsDoNotHide()=>Assert.False(FullscreenPolicy.IsFullscreen(new(0,0,1920,1080),new(0,0,1920,1080),false,true));
    [Fact] public void WindowCoveringWorkAreaIsNotFullscreen()=>Assert.False(FullscreenPolicy.IsFullscreen(new(0,0,1920,1040),new(0,0,1920,1080),false,false));
}
