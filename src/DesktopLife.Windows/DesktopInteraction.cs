using System.Runtime.InteropServices;
namespace DesktopLife.Windows;
public static class DesktopInteraction
{
    public static (double X,double Y)? Cursor()
    { return GetCursorPos(out var point)?(point.X,point.Y):null; }
    public static void MakeClickThrough(nint window)
    {
        var style=GetWindowLongPtrW(window,-20).ToInt64();
        SetWindowLongPtrW(window,-20,(nint)(style|0x20|0x80000|0x08000000));
    }
    // Keep adjacent surfaces stable. Repeated SetWindowPos calls can make layered
    // windows flash, even when their visual depth has not changed.
    public static long ZOrderChanges {get;private set;}
    public static bool PlaceBehind(nint window,nint other)
    {
        if(window==0||other==0||window==other||GetWindow(other,2)==window)return false;
        return Reorder(window,other);
    }
    public static bool PlaceAbove(nint window,nint other)
    {
        if(window==0||other==0||window==other)return false;
        var previous=GetWindow(other,3);
        if(previous==window)return false;
        // hWndInsertAfter denotes the preceding (higher) window, not the lower
        // window. Zero is HWND_TOP when the reference already heads its band.
        return Reorder(window,previous);
    }
    private static bool Reorder(nint window,nint previous)
    {
        var changed=SetWindowPos(window,previous,0,0,0,0,0x0213);
        if(changed)ZOrderChanges++;
        return changed;
    }
    [DllImport("user32.dll")]private static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    public static void SetClickThrough(nint window,bool value)
    {var style=GetWindowLongPtrW(window,-20).ToInt64();SetWindowLongPtrW(window,-20,(nint)(value?style|0x20:style&~0x20));}
    public static void MakeNonActivating(nint window)
    {
        var style=GetWindowLongPtrW(window,-20).ToInt64();
        SetWindowLongPtrW(window,-20,(nint)(style|0x08000000));
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point{public int X,Y;}
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]private static extern nint GetWindowLongPtrW(nint window,int index);
    [DllImport("user32.dll")]private static extern nint SetWindowLongPtrW(nint window,int index,nint value);
}
