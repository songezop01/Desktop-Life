using System.Windows;
using System.Windows.Interop;

namespace DesktopLife.App;

public sealed partial class RoomWindow
{
    internal bool ReceivesPointerAt(Point room)
    {
        var origin=CombHeld?combOrigin:new Point(Item.X,Item.Y);
        return canvas.InputHitTest(new((room.X-origin.X)/SceneScale,(room.Y-origin.Y)/SceneScale)) is not null;
    }
    private void AttachSurfaceInput()
    {
        var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook((nint hwnd,int message,nint wParam,nint lParam,ref bool handled)=>
        {
            if(message!=0x0084||editing)return 0; // WM_NCHITTEST
            var packed=lParam.ToInt64();
            var pixel=new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff));
            var room=DisplayWorkspace.FromPixels(pixel);
            var origin=CombHeld?combOrigin:new Point(Item.X,Item.Y);
            var local=new Point((room.X-origin.X)/SceneScale,(room.Y-origin.Y)/SceneScale);
            if(canvas.InputHitTest(local) is null){handled=true;return -1;} // HTTRANSPARENT
            return 0;
        });
    }
}
