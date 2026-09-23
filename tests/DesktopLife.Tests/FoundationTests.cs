using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public sealed class FoundationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "DesktopLifeTests", Guid.NewGuid().ToString("N"));
    [Fact] public void MissingSettingsUseSafeDefaults() { var s = new SettingsStore(Path.Combine(root,"settings.json")).Load(); Assert.Equal(BrainMode.Utility,s.BrainMode); Assert.Equal(-2,s.Punishment); }
    [Fact] public void SettingsRoundTrip() { var store = new SettingsStore(Path.Combine(root,"settings.json")); var s = new AppSettings { StrongReward = 4, BrainMode = BrainMode.Utility }; store.Save(s); Assert.Equal(s,store.Load()); }
    [Fact] public void FutureSchemaIsNotOverwritten() { Directory.CreateDirectory(root); var path = Path.Combine(root,"settings.json"); File.WriteAllText(path,"{\"SchemaVersion\":99}"); Assert.Throws<InvalidDataException>(() => new SettingsStore(path).Load()); Assert.Contains("99",File.ReadAllText(path)); }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(11)] [InlineData(double.NaN)] public void InvalidRewardRejected(double reward) { Assert.Throws<InvalidDataException>(() => new AppSettings { StrongReward = reward }.Validate()); }
    [Fact] public void LoggingRotatesBoundedHistory() { Directory.CreateDirectory(root); var path = Path.Combine(root,"app.log"); File.WriteAllText(path,new string('x',1_048_577)); new FileAppLog(path).Write("test"); Assert.True(File.Exists(path+".previous")); Assert.Contains("test",File.ReadAllText(path)); Assert.True(new FileInfo(path).Length < 200); }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root,true); }
}
