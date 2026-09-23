using System.IO;
using DesktopLife.Core;
using DesktopLife.Windows;
namespace DesktopLife.App;
internal static class IconIntegrationCheck
{
    public static int Run(bool move)
    {
        var log=Path.GetFullPath("artifacts/icon-integration.log");Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        var session=new DesktopIconSession(new ShellDesktopIcons(),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopLife","desktop-icons-backup.json"));
        try
        {
            var layout=session.Read();layout.Validate();
            File.WriteAllText(log,$"Read PASS: {layout.Icons.Length} icons, {layout.Icons.Count(i=>i.Shortcut)} shortcuts, {layout.Width}x{layout.Height}, arrange flags {layout.Flags&5}.\n");
            using(var worker=new DesktopIconsWorker(Path.Combine(Path.GetTempPath(),"DesktopLifeIconRead","backup.json")))
            {
                var background=worker.Run(s=>s.Read()).GetAwaiter().GetResult();background.Validate();
                File.AppendAllText(log,$"Background STA read PASS: {background.Icons.Length} icons.\n");
            }
            if(!move)return 0;
            if(session.HasBackup)session.Restore();
            layout=session.Read();
            var offsets=new (int X,int Y)[]{(16,0),(-16,0),(0,16),(0,-16),(32,0),(-32,0),(0,32),(0,-32)};
            var candidate=(from item in layout.Icons where item.Shortcut from offset in offsets
                let px=item.X+offset.X let py=item.Y+offset.Y
                where px>=0&&py>=0&&px<layout.Width-80&&py<layout.Height-90
                where !layout.Icons.Any(other=>other.Id!=item.Id&&Math.Abs(other.X-px)<64&&Math.Abs(other.Y-py)<72)
                select (Icon:item,X:px,Y:py)).FirstOrDefault();
            var icon=candidate.Icon??throw new InvalidOperationException("沒有具備安全空位的測試快捷圖示。");
            try
            {
                session.MoveShortcut(icon.Id,candidate.X,candidate.Y);
                var changed=session.Read().Icons.Single(i=>i.Id==icon.Id);
                if(changed.X==icon.X&&changed.Y==icon.Y)throw new Exception("圖示沒有實際移動。");
                File.AppendAllText(log,"Move PASS: actual shortcut position changed.\n");
            }
            finally
            {
                var restored=session.Restore();File.AppendAllText(log,$"Restore PASS: {restored} positions and original arrange/grid flags verified.\n");
            }
            return 0;
        }
        catch(Exception ex){File.AppendAllText(log,"FAIL: "+ex+"\n");return 2;}
    }
}
