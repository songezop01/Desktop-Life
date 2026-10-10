using System.Runtime.InteropServices;

namespace DesktopLife.Windows;

public static class PointerExposure
{
    public static bool IsExposed(nint expected,double pixelX,double pixelY,nint ignoredTool=0,Func<nint,bool>? sceneReceivesAtPoint=null)
    {
        if(expected==0||!double.IsFinite(pixelX)||!double.IsFinite(pixelY))return false;
        var p=new Point{X=(int)Math.Round(pixelX),Y=(int)Math.Round(pixelY)};
        var hit=GetAncestor(WindowFromPoint(p),2);
        if(hit==expected)return true;
        if(hit!=ignoredTool&&(sceneReceivesAtPoint is null||sceneReceivesAtPoint(hit)))return false;
        // Skip only the tool held by this scene. Any other visible, receiving
        // window above the pet blocks care; desktop programs are never clicked.
        var w=GetWindow(hit,2);var inspected=0;
        while(w!=0&&inspected++<256)
        {
            if(IsWindowVisible(w)&&(GetWindowLongPtrW(w,-20).ToInt64()&0x20)==0&&GetWindowRect(w,out var r)
                &&p.X>=r.Left&&p.X<r.Right&&p.Y>=r.Top&&p.Y<r.Bottom
                &&(sceneReceivesAtPoint is null||sceneReceivesAtPoint(w)))return w==expected;
            w=GetWindow(w,2);
        }
        return false;
    }
    public static bool EscapeDown()=> (GetAsyncKeyState(0x1b)&0x8000)!=0;
    public static nint WindowAt(double pixelX,double pixelY)=>GetAncestor(WindowFromPoint(new Point{X=(int)Math.Round(pixelX),Y=(int)Math.Round(pixelY)}),2);
    [StructLayout(LayoutKind.Sequential)] private struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] private struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")]private static extern nint GetAncestor(nint hwnd,uint flag);
    [DllImport("user32.dll")]private static extern nint GetWindow(nint hwnd,uint command);
    [DllImport("user32.dll")]private static extern nint GetWindowLongPtrW(nint hwnd,int index);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsWindowVisible(nint hwnd);
}
