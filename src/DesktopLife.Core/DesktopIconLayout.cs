using System.Text.Json;
namespace DesktopLife.Core;

public sealed record DesktopIcon(string Id,int X,int Y,bool Shortcut);
public sealed record DesktopIconLayout(uint Flags,int Width,int Height,int OriginX,int OriginY,DesktopIcon[] Icons)
{
    public void Validate()
    {
        if(Width is <1 or >100000||Height is <1 or >100000||Icons is null||Icons.Length>4096||Icons.Any(i=>i is null||string.IsNullOrWhiteSpace(i.Id)||i.Id.Length>8192||Math.Abs((long)i.X)>100000||Math.Abs((long)i.Y)>100000)||Icons.Select(i=>i.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Icons.Length)
            throw new InvalidDataException("桌面圖示備份格式無效。");
    }
}
public interface IDesktopIconBackend
{
    DesktopIconLayout Read();
    void SetArrangeFlags(uint flags);
    void Move(string id,int x,int y);
    void MoveMany(IReadOnlyList<DesktopIcon> icons){foreach(var icon in icons)Move(icon.Id,icon.X,icon.Y);}
}
public sealed class DesktopIconSession(IDesktopIconBackend backend,string backupPath)
{
    public const uint ArrangeMask=5; // FWF_AUTOARRANGE | FWF_SNAPTOGRID
    public bool HasBackup=>File.Exists(backupPath)||File.Exists(backupPath+".restored");
    public DesktopIconLayout Read()=>backend.Read();
    public void MoveShortcut(string id,int x,int y)
    {
        using var gate=Lock();
        Prepare();
        var live=backend.Read();live.Validate();
        if(!live.Icons.Any(i=>i.Shortcut&&i.Id.Equals(id,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("此項目不是目前桌面上的快捷圖示。");
        x=Math.Clamp(x,0,Math.Max(0,live.Width-80));y=Math.Clamp(y,0,Math.Max(0,live.Height-90));
        if(live.Icons.Any(i=>!i.Id.Equals(id,StringComparison.OrdinalIgnoreCase)&&Math.Abs(i.X-x)<64&&Math.Abs(i.Y-y)<72))throw new InvalidOperationException("附近空間不足，暫不移動，以免重疊其他圖示。");
        if(!HasBackup)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(backupPath))!);
            var temporary=backupPath+".tmp";
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,live);stream.Flush(true);}
            File.Move(temporary,backupPath); // Never overwrite the original baseline.
        }
        var baseline=Load();
        if(baseline.Width!=live.Width||baseline.Height!=live.Height||baseline.OriginX!=live.OriginX||baseline.OriginY!=live.OriginY)throw new InvalidOperationException("螢幕配置已改變；暫停移動並保留原始排列備份。");
        File.WriteAllText(backupPath+".pending","restore required");
        backend.SetArrangeFlags(0);
        backend.Move(id,x,y);
        var moved=backend.Read().Icons.FirstOrDefault(i=>i.Id.Equals(id,StringComparison.OrdinalIgnoreCase));
        if(moved is null||Math.Abs(moved.X-x)>2||Math.Abs(moved.Y-y)>2)throw new IOException("未能核對圖示的新位置；原始排列備份已保留。");
    }
    public int Restore(bool pendingOnly=false)
    {
        using var gate=Lock();
        Prepare();
        if(!HasBackup || pendingOnly&&!File.Exists(backupPath+".pending"))return 0;
        var saved=Load();var current=backend.Read();current.Validate();
        if(saved.Width!=current.Width||saved.Height!=current.Height||saved.OriginX!=current.OriginX||saved.OriginY!=current.OriginY)throw new InvalidOperationException("請先恢復原螢幕配置，再恢復圖示排列；備份已保留。");
        var ids=current.Icons.Select(i=>i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matching=saved.Icons.Where(i=>ids.Contains(i.Id)).ToArray();
        File.WriteAllText(backupPath+".pending","restore required");
        backend.SetArrangeFlags(0);
        try{backend.MoveMany(matching);}
        finally{backend.SetArrangeFlags(saved.Flags&ArrangeMask);}
        var verified=backend.Read();
        if((verified.Flags&ArrangeMask)!=(saved.Flags&ArrangeMask)||matching.Any(old=>!verified.Icons.Any(i=>i.Id.Equals(old.Id,StringComparison.OrdinalIgnoreCase)&&Math.Abs(i.X-old.X)<=2&&Math.Abs(i.Y-old.Y)<=2)))
            throw new IOException("圖示排列尚未完整恢復，原始備份仍保留，可再按恢復重試。");
        File.Delete(backupPath+".pending"); // Baseline remains available across exits and restores.
        return matching.Length;
    }
    public int UpdateBackup()
    {
        using var gate=Lock();Prepare();
        var live=backend.Read();live.Validate();
        var temp=backupPath+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,live);stream.Flush(true);}
        if(File.Exists(backupPath))File.Replace(temp,backupPath,backupPath+".previous");else File.Move(temp,backupPath);
        File.WriteAllText(backupPath+".managed","persistent baseline");File.Delete(backupPath+".pending");
        return live.Icons.Length;
    }
    private void Prepare()
    {
        if(File.Exists(backupPath+".managed"))return;
        if(File.Exists(backupPath))File.WriteAllText(backupPath+".pending","legacy recovery");
        else if(File.Exists(backupPath+".restored"))File.Copy(backupPath+".restored",backupPath);
        File.WriteAllText(backupPath+".managed","persistent baseline");
    }
    private DesktopIconLayout Load()
    {
        if(new FileInfo(backupPath).Length>8_000_000)throw new InvalidDataException("圖示備份過大。");
        var saved=JsonSerializer.Deserialize<DesktopIconLayout>(File.ReadAllText(backupPath))??throw new InvalidDataException("圖示備份為空。");saved.Validate();return saved;
    }
    private FileStream Lock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(backupPath))!);
        return new FileStream(backupPath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }
}
