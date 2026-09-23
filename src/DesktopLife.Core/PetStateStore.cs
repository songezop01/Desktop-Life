using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopLife.Core;

public sealed record PetSnapshot
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public PetState State { get; init; } = new();
    [JsonRequired] public DateTimeOffset LastSaveTime { get; init; }
    [JsonRequired] public double TotalRuntimeSeconds { get; init; }
    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("不支援的 PetState schema，原始檔案不會覆寫。");
        if (State is null || LastSaveTime == default || !double.IsFinite(TotalRuntimeSeconds) || TotalRuntimeSeconds < 0)
            throw new InvalidDataException("PetState 存檔缺少資料或含無效數值。");
        State.Validate();
    }
}

public sealed class PetStateStore(string path)
{
    public PetSnapshot? Load()
    {
        if (!File.Exists(path)) return null;
        var snapshot = JsonSerializer.Deserialize<PetSnapshot>(File.ReadAllText(path))
            ?? throw new InvalidDataException("PetState 存檔不可為 null。");
        snapshot.Validate();
        return snapshot;
    }
    public void Save(PetSnapshot snapshot)
    {
        snapshot.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, snapshot, new JsonSerializerOptions { WriteIndented = true });
            stream.Flush(flushToDisk: true);
        }
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }
}
