using System.Runtime.InteropServices;
using System.Text;
using DesktopLife.Core;
namespace DesktopLife.Windows;
public static class FullscreenDetector
{
    public static bool IsFullscreen() => Read().Fullscreen;
    public static ForegroundState Read()=>Inspect(GetForegroundWindow());
    public static ForegroundState Read(nint monitor)
    {var window=GetForegroundWindow();return monitor!=0&&MonitorFromWindow(window,2)!=monitor?new(false,false,false):Inspect(window);}
    public static ForegroundState Inspect(nint window)
    {
        if(window==0||IsIconic(window))return new(false,false,false);
        var name=new StringBuilder(256);GetClassNameW(window,name,256);
        if(name.ToString() is "Progman" or "WorkerW")return new(false,false,true);
        var info=new MonitorInfo{Size=(uint)Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfoW(MonitorFromWindow(window,2),ref info)||!GetWindowRect(window,out var rect))return new(false,false,false);
        var caption=(GetWindowLongPtrW(window,-16).ToInt64()&0x00C00000)!=0;
        var full=FullscreenPolicy.IsFullscreen(new(rect.Left,rect.Top,rect.Right,rect.Bottom),new(info.Monitor.Left,info.Monitor.Top,info.Monitor.Right,info.Monitor.Bottom),caption,false);
        var maximized=IsZoomed(window);
        return new(full,maximized,false);
    }
    // Keep desktop-only surfaces below application windows without reparenting WPF HWNDs.
    public static bool PlaceAtDesktopLevel(nint window)
    {
        nint desktop=0;
        EnumWindows((candidate,_)=>
        {
            if(FindWindowExW(candidate,0,"SHELLDLL_DefView",null)==0)return true;
            desktop=candidate;return false;
        },0);
        if(desktop==0)return false;
        var previous=GetWindow(desktop,3);
        return previous==window || (previous!=0 && SetWindowPos(window,previous,0,0,0,0,0x0013));
    }
    public static bool IsBelow(nint window,nint other)
    {
        var current=GetWindow(other,2);
        for(var i=0;current!=0&&i<10000;i++,current=GetWindow(current,2))if(current==window)return true;
        return false;
    }
    private delegate bool WindowCallback(nint window,nint data);
    [DllImport("user32.dll")]private static extern bool EnumWindows(WindowCallback callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindowExW(nint parent,nint after,string name,string? title);
    [DllImport("user32.dll")]private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]private static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsZoomed(nint window);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{public uint Size;public Rect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassNameW(nint window,StringBuilder buffer,int count);
    [DllImport("user32.dll")]private static extern nint GetWindowLongPtrW(nint window,int index);
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetMonitorInfoW(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetWindowRect(nint window,out Rect rect);
}
