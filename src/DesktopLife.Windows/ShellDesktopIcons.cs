using System.Runtime.InteropServices;
using DesktopLife.Core;
namespace DesktopLife.Windows;

// Supported Shell API, not cross-process ListView messages or file/shortcut changes.
public sealed class ShellDesktopIcons : IDesktopIconBackend
{
    public DesktopIconLayout Read()
    {
        using var shell=new View();var icons=new List<DesktopIcon>();
        shell.Each((id,pidl)=>{Check(shell.Folder.GetItemPosition(pidl,out var point));icons.Add(new(id,point.X,point.Y,id.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)||id.EndsWith(".url",StringComparison.OrdinalIgnoreCase)));});
        Check(shell.Folder.GetCurrentFolderFlags(out var flags));
        Check(((IShellViewWindow)shell.RawView).GetWindow(out var hwnd));
        if(!GetClientRect(hwnd,out var rect))throw new InvalidOperationException("無法取得桌面範圍。");
        var origin=new Point();if(!ClientToScreen(hwnd,ref origin))throw new InvalidOperationException("無法轉換桌面座標。");
        return new(flags,rect.Right-rect.Left,rect.Bottom-rect.Top,origin.X,origin.Y,icons.ToArray());
    }
    public void SetArrangeFlags(uint flags)
    {using var shell=new View();Check(shell.Folder.SetCurrentFolderFlags(DesktopIconSession.ArrangeMask,flags&DesktopIconSession.ArrangeMask));}
    public void Move(string id,int x,int y)
    {
        using var shell=new View();var found=false;
        shell.Each((name,pidl)=>
        {
            if(!name.Equals(id,StringComparison.OrdinalIgnoreCase))return;
            Check(shell.Folder.SelectAndPositionItems(1,[pidl],[new(){X=x,Y=y}],0x80));found=true;
        });
        if(!found)throw new InvalidOperationException("快捷圖示已更名或移除，未移動其他圖示。");
    }
    public void MoveMany(IReadOnlyList<DesktopIcon> icons)
    {
        var byId=icons.ToDictionary(i=>i.Id,StringComparer.OrdinalIgnoreCase);
        using var shell=new View();
        shell.Each((name,pidl)=>{if(byId.TryGetValue(name,out var icon))Check(shell.Folder.SelectAndPositionItems(1,[pidl],[new(){X=icon.X,Y=icon.Y}],0x80));});
    }
    private static void Check(int result)=>Marshal.ThrowExceptionForHR(result);
    private sealed class View : IDisposable
    {
        public object RawView {get;private set;}=null!;
        public IFolderView2 Folder {get;private set;}=null!;
        private nint parent;
        public View()
        {
            object? windows=null,desktop=null,browser=null,folder=null;
            try
            {
                windows=Activator.CreateInstance(Type.GetTypeFromCLSID(new("9BA05972-F6A8-11CF-A442-00A0C90A8F39"),true)!)!;
                object location=0;object root=null!;int handle=0;
                desktop=((dynamic)windows).FindWindowSW(ref location,ref root,8,out handle,1);
                var service=new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");var browserId=typeof(IShellBrowser).GUID;
                Check(((IServiceProvider)desktop).QueryService(ref service,ref browserId,out browser));
                Check(((IShellBrowser)browser).QueryActiveShellView(out var view));RawView=view;Folder=(IFolderView2)view;
                var persistId=typeof(IPersistFolder2).GUID;Check(Folder.GetFolder(ref persistId,out folder));
                Check(((IPersistFolder2)folder).GetCurFolder(out parent));
            }
            catch{Dispose();throw;}
            finally{Release(folder);Release(browser);Release(desktop);Release(windows);}
        }
        public void Each(Action<string,nint> visit)
        {
            Check(Folder.ItemCount(2,out var count));
            if(count is <0 or >4096)throw new InvalidOperationException("桌面圖示數量超過支援範圍。");
            for(var i=0;i<count;i++)
            {
                nint child=0,absolute=0,name=0;
                try
                {
                    Check(Folder.Item(i,out child));absolute=ILCombine(parent,child);
                    if(absolute==0)throw new OutOfMemoryException();
                    Check(SHGetNameFromIDList(absolute,0x80028000,out name));
                    visit(Marshal.PtrToStringUni(name)??throw new InvalidOperationException("無法識別桌面圖示。"),child);
                }
                finally{if(name!=0)Marshal.FreeCoTaskMem(name);if(absolute!=0)Marshal.FreeCoTaskMem(absolute);if(child!=0)Marshal.FreeCoTaskMem(child);}
            }
        }
        public void Dispose(){if(parent!=0){Marshal.FreeCoTaskMem(parent);parent=0;}Release(RawView);RawView=null!;}
        private static void Release(object? value){if(value is not null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetClientRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")]private static extern bool ClientToScreen(nint hwnd,ref Point point);
    [DllImport("shell32.dll")]private static extern nint ILCombine(nint parent,nint child);
    [DllImport("shell32.dll")]private static extern int SHGetNameFromIDList(nint pidl,uint kind,out nint name);
    [ComImport,Guid("6D5140C1-7436-11CE-8034-00AA006009FA"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider{[PreserveSig]int QueryService(ref Guid service,ref Guid iid,[MarshalAs(UnmanagedType.IUnknown)]out object result);}
    [ComImport,Guid("000214E3-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellViewWindow{[PreserveSig]int GetWindow(out nint window);[PreserveSig]int ContextSensitiveHelp(bool enter);}
    [ComImport,Guid("000214E2-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        [PreserveSig]int GetWindow(out nint hwnd);[PreserveSig]int ContextSensitiveHelp(bool enter);
        void InsertMenusSB();void SetMenuSB();void RemoveMenusSB();void SetStatusTextSB();void EnableModelessSB();void TranslateAcceleratorSB();void BrowseObject();void GetViewStateStream();void GetControlWindow();void SendControlMsg();
        [PreserveSig]int QueryActiveShellView([MarshalAs(UnmanagedType.IUnknown)]out object view);
    }
    [ComImport,Guid("1AC3D9F0-175C-11D1-95BE-00609797EA4F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFolder2{void GetClassID();void Initialize();[PreserveSig]int GetCurFolder(out nint pidl);}
    [ComImport,Guid("1AF3A467-214F-4298-908E-06B03E0B39F9"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFolderView2
    {
        void GetCurrentViewMode();void SetCurrentViewMode();
        [PreserveSig]int GetFolder(ref Guid iid,[MarshalAs(UnmanagedType.IUnknown)]out object folder);
        [PreserveSig]int Item(int index,out nint pidl);
        [PreserveSig]int ItemCount(uint flags,out int count);
        void Items();void GetSelectionMarkedItem();void GetFocusedItem();
        [PreserveSig]int GetItemPosition(nint pidl,out Point point);
        void GetSpacing();void GetDefaultSpacing();void GetAutoArrange();void SelectItem();
        [PreserveSig]int SelectAndPositionItems(uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]nint[] pidls,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]Point[] points,uint flags);
        void SetGroupBy();void GetGroupBy();void SetViewProperty();void GetViewProperty();void SetTileViewProperties();void SetExtendedTileViewProperties();void SetText();
        [PreserveSig]int SetCurrentFolderFlags(uint mask,uint flags);
        [PreserveSig]int GetCurrentFolderFlags(out uint flags);
    }
}
