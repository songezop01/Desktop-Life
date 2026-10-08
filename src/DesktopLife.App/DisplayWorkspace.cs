using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopLife.Core;
namespace DesktopLife.App;

public sealed record RoomDisplay(string Id,string Label,nint Handle,BodyBounds Bounds,double Scale,bool Primary)
{
    public string DeviceName {get;init;}="";
    public string Adapter {get;init;}="";
    public BodyBounds PixelBounds {get;init;}
    public BodyBounds PixelWorkArea {get;init;}
    public bool Portrait=>PixelBounds.Height>PixelBounds.Width;
}

public static class DisplayWorkspace
{
    private static RoomDisplay? active;
    // Keep desired room coordinates while WPF processes WM_DPICHANGED during a monitor move.
    // Weak keys do not retain closed furniture or diagnostic windows.
    private sealed class WindowPlacement {public double X,Y;public bool Attached,Queued;}
    private static readonly ConditionalWeakTable<Window,WindowPlacement> placements=new();
    public static event Action? WorkspaceDpiChanged;
    public static RoomDisplay Active=>active??=Enumerate().OrderByDescending(d=>d.Primary).First();
    public static BodyBounds Bounds=>Active.Bounds;
    public static RoomDisplay Select(string? id,IReadOnlyList<RoomDisplay>? inventory=null)
    {
        var displays=inventory??Enumerate();
        var selected=DisplayPreference.Select(id,displays.Select(d=>new DisplayIdentity(d.Id,d.DeviceName,d.Primary)).ToArray());
        return active=displays.First(d=>d.Id==selected);
    }
    public static IReadOnlyList<RoomDisplay> Enumerate()
    {
        var result=new List<RoomDisplay>();var adapters=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for(uint index=0;;index++)
        {
            var adapter=new DisplayDevice{Size=Marshal.SizeOf<DisplayDevice>()};
            if(!EnumDisplayDevicesW(null,index,ref adapter,0))break;
            adapters[adapter.DeviceName]=adapter.DeviceString;
        }
        EnumDisplayMonitors(0,0,(nint monitor,nint dc,ref NativeRect rectangle,nint data)=>
        {
            var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
            if(!GetMonitorInfoW(monitor,ref info))return true;
            using var probe=new HwndSource(new HwndSourceParameters("Desktop Life DPI probe")
            {PositionX=info.Work.Left+8,PositionY=info.Work.Top+8,Width=1,Height=1,WindowStyle=unchecked((int)0x80000000)});
            var scale=Math.Max(96,GetDpiForWindow(probe.Handle))/96d;
            var child=new DisplayDevice{Size=Marshal.SizeOf<DisplayDevice>()};
            EnumDisplayDevicesW(info.Device,0,ref child,1);
            var identity=string.IsNullOrWhiteSpace(child.DeviceId)?info.Device:child.DeviceId;
            var adapter=adapters.GetValueOrDefault(info.Device,"");
            var name=adapter.Contains("spacedesk",StringComparison.OrdinalIgnoreCase)?"spacedesk"
                :string.IsNullOrWhiteSpace(child.DeviceString)||child.DeviceString.Contains("Generic",StringComparison.OrdinalIgnoreCase)?"顯示器":child.DeviceString;
            var primary=(info.Flags&1)!=0;
            var pixels=new BodyBounds(info.Monitor.Left,info.Monitor.Top,info.Monitor.Right-info.Monitor.Left,info.Monitor.Bottom-info.Monitor.Top);
            var work=new BodyBounds(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
            var bounds=RoomCoordinates.FromPixels(work.Left,work.Top,work.Width,work.Height,scale);
            var label=$"{name} · {pixels.Width:0} × {pixels.Height:0} · {scale:P0}"+(pixels.Height>pixels.Width?" · 直向":"")+(primary?"（主要）":"");
            result.Add(new(identity,label,monitor,bounds,scale,primary){DeviceName=info.Device,Adapter=adapter,PixelBounds=pixels,PixelWorkArea=work});
            return true;
        },0);
        if(result.Count==0)
        {
            var area=SystemParameters.WorkArea;var bounds=new BodyBounds(area.Left,area.Top,area.Width,area.Height);
            result.Add(new("primary","主要顯示器",0,bounds,1,true){DeviceName="primary",PixelBounds=bounds,PixelWorkArea=bounds});
        }
        return result;
    }
    public static Point FromPixels(Point point)=>new(point.X/Active.Scale,point.Y/Active.Scale);
    public static Point ToPixels(Point point)=>new(point.X*Active.Scale,point.Y*Active.Scale);
    public static Point RoomPosition(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;
        if(handle!=0&&GetWindowRect(handle,out var rectangle))return FromPixels(new(rectangle.Left,rectangle.Top));
        if(placements.TryGetValue(window,out var placement))return new(placement.X,placement.Y);
        return new(window.Left,window.Top);
    }
    public static void Position(Window window,double x,double y)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y))throw new ArgumentOutOfRangeException(nameof(x),"Window position must be finite.");
        var placement=placements.GetValue(window,_=>new());placement.X=x;placement.Y=y;
        if(!placement.Attached)
        {
            placement.Attached=true;
            window.DpiChanged+=(_,e)=>{
                if(MonitorFromWindow(new WindowInteropHelper(window).Handle,2)==Active.Handle&&Math.Abs(e.NewDpi.DpiScaleX-Active.Scale)>.01)
                    WorkspaceDpiChanged?.Invoke();
                if(placement.Queued)return;placement.Queued=true;
                window.Dispatcher.BeginInvoke(new Action(()=>{
                    placement.Queued=false;
                    if(new WindowInteropHelper(window).Handle!=0)Position(window,placement.X,placement.Y);
                }),DispatcherPriority.Loaded);
            };
        }
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==0){window.Left=x;window.Top=y;return;}
        var pixels=ToPixels(new(x,y));
        SetWindowPos(handle,0,(int)Math.Round(pixels.X),(int)Math.Round(pixels.Y),0,0,0x0215);
    }
    public static (BodyBounds Pixels,uint Dpi) NativeWindowBounds(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==0||!GetWindowRect(handle,out var rectangle))throw new InvalidOperationException("Native surface is unavailable.");
        return (new(rectangle.Left,rectangle.Top,rectangle.Right-rectangle.Left,rectangle.Bottom-rectangle.Top),GetDpiForWindow(handle));
    }
    private delegate bool MonitorCallback(nint monitor,nint dc,ref NativeRect rectangle,nint data);
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct DisplayDevice
    {
        public int Size;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string DeviceString;public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string DeviceKey;
    }
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorCallback callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfoW(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool EnumDisplayDevicesW(string? device,uint index,ref DisplayDevice info,uint flags);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint window,out NativeRect rectangle);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
}
