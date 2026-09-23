using System.Text.Json;

namespace DesktopLife.Core;

public enum BrainMode { Utility, FlyInspired, RealConnectomeExperimental, Hybrid }
public enum CloseBehavior { Tray, Exit }
public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public BrainMode BrainMode { get; init; } = BrainMode.Utility;
    public DisplayPriority DisplayPriority { get; init; } = DisplayPriority.Medium;
    public PetAppearance PetAppearance { get; init; } = PetAppearance.Cat;
    public CloseBehavior CloseBehavior {get;init;}=CloseBehavior.Tray;
    public double AudioVolume {get;init;}=.35;
    public double CatVolume {get;init;}=1;
    public double ToyVolume {get;init;}=1;
    public double AmbientVolume {get;init;}=.7;
    public string? SoundPackPath {get;init;}
    public string? ActiveDisplay {get;init;}
    public bool MuteAudio {get;init;}
    public bool QuietCompanion {get;init;}
    public bool DesktopIconsEnabled {get;init;}
    public double StrongReward { get; init; } = 3;
    public double PositiveReward { get; init; } = 1;
    public double Punishment { get; init; } = -2;
    public double LearningRate { get; init; } = .002;
    public double UtilityWeight { get; init; } = .6;
    public double FlyWeight { get; init; } = .4;
    public double ConnectomeWeight { get; init; } = .2;
    public string? ConnectomePath { get; init; }
    public void Validate()
    {
        if(new[]{AudioVolume,CatVolume,ToyVolume,AmbientVolume}.Any(v=>!double.IsFinite(v)||v<0||v>1))throw new InvalidDataException("音量必須為 0–1。");
        if (!Enum.IsDefined(CloseBehavior)||!Enum.IsDefined(DisplayPriority) || !Enum.IsDefined(PetAppearance)) throw new InvalidDataException("顯示層級或外觀設定無效。");
        if (SchemaVersion != 1) throw new InvalidDataException("Unsupported settings schema.");
        if (!Enum.IsDefined(BrainMode)) throw new InvalidDataException("Invalid brain mode.");
        if(new[]{UtilityWeight,FlyWeight,ConnectomeWeight}.Any(v=>!double.IsFinite(v)||v<0||v>1) || UtilityWeight+FlyWeight+ConnectomeWeight<=0)
            throw new InvalidDataException("Hybrid weights must be 0–1 with a positive sum.");
        if (!double.IsFinite(LearningRate) || LearningRate <= 0 || LearningRate > .01) throw new InvalidDataException("LearningRate must be in (0, .01].");
        if (!double.IsFinite(StrongReward) || !double.IsFinite(PositiveReward) || !double.IsFinite(Punishment)
            || StrongReward <= 0 || PositiveReward <= 0 || Punishment >= 0
            || StrongReward > 10 || PositiveReward > 10 || Punishment < -10)
            throw new InvalidDataException("Rewards must be finite, correctly signed and within -10..10.");
    }
}

public sealed class SettingsStore(string path)
{
    public AppSettings Load()
    {
        if (!File.Exists(path)) return new();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Empty settings.");
        settings.Validate();
        return settings;
    }
    public void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
}

public interface IAppLog { void Write(string message); }
public sealed class FileAppLog(string path) : IAppLog
{
    private readonly object gate = new();
    public void Write(string message)
    {
        lock (gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
                File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
    }
}
