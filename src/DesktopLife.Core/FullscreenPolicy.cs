namespace DesktopLife.Core;
public readonly record struct ScreenRect(int Left,int Top,int Right,int Bottom);
public static class FullscreenPolicy
{
    public static bool IsFullscreen(ScreenRect window,ScreenRect monitor,bool hasCaption,bool isDesktopOrOwn)
    {
        if(hasCaption||isDesktopOrOwn||monitor.Right<=monitor.Left||monitor.Bottom<=monitor.Top)return false;
        return Math.Abs(window.Left-monitor.Left)<=2 && Math.Abs(window.Top-monitor.Top)<=2
            && Math.Abs(window.Right-monitor.Right)<=2 && Math.Abs(window.Bottom-monitor.Bottom)<=2;
    }
}
