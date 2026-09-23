using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DesktopLife.Core;
namespace DesktopLife.App;

public sealed record RoomDisplay(string Id,string Label,nint Handle,BodyBounds Bounds,double Scale,bool Primary);
public static class DisplayWorkspace
{
    private static RoomDisplay? active;
    public static RoomDisplay Active=>active??=Enumerate().OrderByDescending(d=>d.Primary).First();
    public static BodyBounds Bounds=>Active.Bounds;
    public static RoomDisplay Select(string? id)
    {var displays=Enumerate();return active=displays.FirstOrDefault(d=>d.Id==id)??displays.OrderByDescending(d=>d.Primary).First();}
    public static IReadOnlyList<RoomDisplay> Enumerate()
    {
        var result=new List<RoomDisplay>();
        EnumDisplayMonitors(0,0,(nint monitor,nint dc,ref NativeRect rectangle,nint data)=>
        {
            var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
            if(!GetMonitorInfoW(monitor,ref info))return true;
            // A hidden per-monitor-aware HWND provides effective DPI without assuming primary-screen scaling.
            using var probe=new HwndSource(new HwndSourceParameters("Desktop Life DPI probe") {PositionX=info.Work.Left+8,PositionY=info.Work.Top+8,Width=1,Height=1,WindowStyle=unchecked((int)0x80000000)});
            var scale=Math.Max(96,GetDpiForWindow(probe.Handle))/96d;
            var bounds=RoomCoordinates.FromPixels(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top,scale);
            result.Add(new(info.Device,$"{info.Device} · {(info.Work.Right-info.Work.Left)} × {(info.Work.Bottom-info.Work.Top)} · {scale:P0}"+((info.Flags&1)!=0?"（主要）":""),monitor,bounds,scale,(info.Flags&1)!=0));
            return true;
        },0);
        if(result.Count==0)
        {var area=SystemParameters.WorkArea;result.Add(new("primary","主要顯示器",0,new(area.Left,area.Top,area.Width,area.Height),1,true));}
        return result;
    }
    public static Point FromPixels(Point point)=>new(point.X/Active.Scale,point.Y/Active.Scale);
    public static void Position(Window window,double x,double y)
    {
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==0){window.Left=x;window.Top=y;return;}
        SetWindowPos(handle,0,(int)Math.Round(x*Active.Scale),(int)Math.Round(y*Active.Scale),0,0,0x0215);
    }
    private delegate bool MonitorCallback(nint monitor,nint dc,ref NativeRect rectangle,nint data);
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorCallback callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfoW(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
}
