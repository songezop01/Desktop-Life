using System.Text.Json.Serialization;

namespace DesktopLife.Core;

public sealed record PetState
{
    [JsonRequired] public double Energy { get; init; } = 65;
    [JsonRequired] public double Hunger { get; init; } = 25;
    [JsonRequired] public double Mood { get; init; } = 65;
    [JsonRequired] public double Boredom { get; init; } = 25;
    [JsonRequired] public double Curiosity { get; init; } = 60;
    [JsonRequired] public double Loneliness { get; init; } = 20;
    [JsonRequired] public double Excitement { get; init; } = 30;
    [JsonRequired] public double Fatigue { get; init; } = 20;

    public void Validate()
    {
        double[] values = [Energy, Hunger, Mood, Boredom, Curiosity, Loneliness, Excitement, Fatigue];
        if (values.Any(v => !double.IsFinite(v) || v < 0 || v > 100))
            throw new InvalidDataException("PetState 必須為有限的 0–100 數值。");
    }
}

public sealed record EnvironmentFeeding(double? Level, double? Presence, string Status);

// Environment feeding has no reference to rewards, action preferences or neural weights.
public static class EnvironmentFeedingEncoder
{
    public static EnvironmentFeeding Encode(EnvironmentState? environment, DateTimeOffset now)
    {
        if (environment is null || now - environment.Timestamp > TimeSpan.FromSeconds(3)
            || environment.Timestamp - now > TimeSpan.FromSeconds(1))
            return new(null, null, "感測尚未就緒或過期；暫停環境影響");
        double sum = 0, weights = 0;
        void Add(SensorValue sensor, double scale, double weight)
        {
            if (sensor.Value is not { } value || !double.IsFinite(value) || value < 0) return;
            sum += Math.Clamp(value / scale, 0, 1) * weight;
            weights += weight;
        }
        Add(environment.CpuPercent, 100, .30);
        Add(environment.GpuPercent, 100, .20);
        Add(environment.RamPercent, 100, .10);
        Add(environment.DiskReadBytesPerSecond, 20 * 1024 * 1024, .075);
        Add(environment.DiskWriteBytesPerSecond, 20 * 1024 * 1024, .075);
        Add(environment.NetworkUploadBytesPerSecond, 2 * 1024 * 1024, .05);
        Add(environment.NetworkDownloadBytesPerSecond, 2 * 1024 * 1024, .05);
        Add(environment.MousePixelsPerSecond, 400, .15);
        double? presence = environment.IdleSeconds.Value is { } idle && double.IsFinite(idle) && idle >= 0
            ? Math.Exp(-idle / 300) : null;
        var activity = weights > 0 ? sum / weights : (double?)null;
        double? level = (activity, presence) switch
        {
            ({ } a, { } p) => .8 * a + .2 * p,
            ({ } a, null) => a,
            (null, { } p) => p,
            _ => null
        };
        return new(level, presence, level is null ? "沒有有效感測；暫停環境影響"
            : weights < .999 || presence is null ? "部分感測可用；僅依有效數值計算" : "環境供能中（不產生行為獎勵）");
    }
}

public static class Homeostasis
{
    public static readonly TimeSpan MaxOfflineDuration = TimeSpan.FromHours(8);
    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
    private static double Approach(double value, double target, double rate, double minutes) =>
        value + (target - value) * (1 - Math.Exp(-rate * minutes));

    public static PetState Step(PetState state, EnvironmentFeeding feeding, TimeSpan elapsed, bool resting)
    {
        state.Validate();
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (feeding.Level is not { } level) return state;
        if (!double.IsFinite(level) || level < 0 || level > 1
            || feeding.Presence is { } invalid && (!double.IsFinite(invalid) || invalid < 0 || invalid > 1))
            throw new ArgumentOutOfRangeException(nameof(feeding));
        var minutes = elapsed.TotalMinutes;
        var presence = feeding.Presence;
        return new()
        {
            Energy = Clamp(state.Energy + (-.12 + .55 * level + (resting ? .10 : 0)) * minutes),
            Hunger = Clamp(state.Hunger + (.12 - .70 * level) * minutes),
            Mood = Clamp(state.Mood + (-.08 + .30 * level + .05 * (presence ?? 0)) * minutes),
            Boredom = Clamp(state.Boredom + (.10 - .25 * level - .10 * (presence ?? 0)) * minutes),
            Loneliness = Clamp(state.Loneliness + (presence is { } p ? .10 - .25 * p : 0) * minutes),
            Excitement = Clamp(Approach(state.Excitement, 10 + 80 * level, .04, minutes)),
            Fatigue = Clamp(state.Fatigue + (.06 + .06 * level - (resting ? .30 : 0)) * minutes),
            Curiosity = Clamp(Approach(state.Curiosity, 40 + .25 * state.Mood + .10 * state.Boredom, .03, minutes))
        };
    }

    public static PetState ApplyOffline(PetState state, TimeSpan duration)
    {
        state.Validate();
        var hours = Math.Clamp(duration.TotalHours, 0, MaxOfflineDuration.TotalHours);
        return state with
        {
            Energy = Clamp(state.Energy - hours), Hunger = Clamp(state.Hunger + 2 * hours),
            Mood = Clamp(state.Mood - .75 * hours), Boredom = Clamp(state.Boredom + hours),
            Loneliness = Clamp(state.Loneliness + 2 * hours),
            Excitement = Clamp(Approach(state.Excitement, 10, .1, hours)),
            Fatigue = Clamp(state.Fatigue - hours)
        };
    }
}

public sealed class HomeostasisSession
{
    public PetState State { get; private set; }
    public double TotalRuntimeSeconds { get; private set; }
    public EnvironmentFeeding Feeding { get; private set; } = new(null, null, "等待環境取樣");
    public HomeostasisSession(PetState state, double totalRuntimeSeconds = 0)
    {
        state.Validate();
        if (!double.IsFinite(totalRuntimeSeconds) || totalRuntimeSeconds < 0) throw new ArgumentOutOfRangeException(nameof(totalRuntimeSeconds));
        State = state; TotalRuntimeSeconds = totalRuntimeSeconds;
    }
    public void Advance(EnvironmentState? environment, DateTimeOffset now, TimeSpan elapsed, bool resting)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (elapsed > TimeSpan.FromSeconds(10)) { ApplyOffline(elapsed); return; }
        Feeding = EnvironmentFeedingEncoder.Encode(environment, now);
        State = Homeostasis.Step(State, Feeding, elapsed, resting);
        TotalRuntimeSeconds += elapsed.TotalSeconds;
    }
    public void ApplyCare(PetState state) { state.Validate(); State=state; }
    public void AdvanceCompanion(TimeSpan elapsed,BodyAction action,bool present)
    {
        if(elapsed<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(elapsed));
        if(elapsed>TimeSpan.FromSeconds(10)){ApplyCompanionOffline(elapsed);return;}
        State=CompanionCare.Step(State,elapsed,action,present);TotalRuntimeSeconds+=elapsed.TotalSeconds;
        Feeding=new(0,present?1:0,"飯飯補充能量，陪伴滋養心情");
    }
    public void ApplyCompanionOffline(TimeSpan duration)
    {
        var hours=Math.Clamp(duration.TotalHours,0,8);
        State=State with{Energy=Math.Min(100,State.Energy+hours*4),Fatigue=Math.Max(0,State.Fatigue-hours*6),
            Hunger=Math.Max(State.Hunger,Math.Min(85,State.Hunger+hours*3)),Boredom=Math.Min(100,State.Boredom+hours),
            Loneliness=Math.Min(100,State.Loneliness+hours)};
        Feeding=new(null,null,"離開時牠也會休息；最多計算 8 小時，不扣親密度。");
    }
    public void ApplyOffline(TimeSpan duration)
    {
        State = Homeostasis.ApplyOffline(State, duration);
        Feeding = new(null, null, "離線影響已套用，每次離線最多計算 8 小時");
    }
}
