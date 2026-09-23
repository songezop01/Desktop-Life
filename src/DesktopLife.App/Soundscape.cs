using System.IO;
using System.Windows.Media;
using DesktopLife.Core;
namespace DesktopLife.App;
public sealed class Soundscape : IDisposable
{
    private readonly Dictionary<PetSound,MediaPlayer> players=[];
    private readonly Dictionary<PetSound,DateTimeOffset> last=[];
    private readonly bool diagnostic;
    private readonly string root;
    private readonly Dictionary<PetSound,string> builtin=[];
    public event Action<string>? ErrorChanged;
    public double Volume {get;set;}=.35;
    public double CatVolume {get;set;}=1;
    public double ToyVolume {get;set;}=1;
    public double AmbientVolume {get;set;}=.7;
    public bool Muted {get;set;}
    public string? Error {get;private set;}
    public Soundscape(string root)
    {
        this.root=root;
        diagnostic=root.StartsWith(Path.Combine(Path.GetTempPath(),"DesktopLifeSmoke"),StringComparison.OrdinalIgnoreCase);
        var folder=Path.Combine(root,"audio-v1");Directory.CreateDirectory(folder);
        foreach(var sound in Enum.GetValues<PetSound>())
        {
            var file=Path.Combine(folder,sound+".wav");
            if(!File.Exists(file))File.WriteAllBytes(file,PetSoundWave.Create(sound));
            builtin[sound]=file;
        }
        SelectPack(null);
    }
    public void SelectPack(string? id)
    {
        Stop();foreach(var player in players.Values)player.Close();players.Clear();last.Clear();Error=null;
        var library=new SoundPackLibrary(root);
        foreach(var sound in Enum.GetValues<PetSound>())
        {
            string? custom=null;
            try
            {
                if(id=="legacy")
                {
                    var file=Path.Combine(root,"audio-v1","custom",sound+".wav");
                    if(File.Exists(file)){SoundPackLibrary.ValidateWave(File.ReadAllBytes(file));custom=file;}
                }
                else custom=library.Resolve(id,sound);
            }
            catch(Exception ex) when(ex is IOException or InvalidDataException){Report("自訂音效無效，已使用內建音效："+ex.Message);}
            Open(sound,custom??builtin[sound],custom is not null);
        }
    }
    private void Open(PetSound sound,string file,bool fallback)
    {
        var player=new MediaPlayer();players[sound]=player;
        player.MediaFailed+=(_,e)=>
        {
            if(!players.TryGetValue(sound,out var current)||current!=player)return;
            Report("音效播放失敗"+(fallback?"，已切回內建音效。":"：請檢查 Windows 音訊輸出。"));
            if(fallback){player.Close();Open(sound,builtin[sound],false);}
        };
        player.Open(new Uri(file));
    }
    private void Report(string message){Error=message;ErrorChanged?.Invoke(message);}
    public void ApplyLevels()
    {foreach(var entry in players)entry.Value.Volume=Muted?0:Math.Clamp(Volume*(entry.Key==PetSound.Bell?ToyVolume:CatVolume),0,1);}
    public void Play(PetSound sound,double strength=1,bool preview=false)
    {
        if(Muted||Volume<=0||diagnostic)return;
        var now=DateTimeOffset.UtcNow;var cooldown=sound==PetSound.Bell?.22:sound==PetSound.Purr?5:10;
        if(!preview&&last.TryGetValue(sound,out var previous)&&(now-previous).TotalSeconds<cooldown)return;
        last[sound]=now;var player=players[sound];var category=sound==PetSound.Bell?ToyVolume:CatVolume;player.Volume=Math.Clamp(Volume*category*strength,0,1);player.Position=TimeSpan.Zero;player.Play();
    }
    public void Stop(){foreach(var player in players.Values)player.Stop();}
    public void Dispose(){foreach(var player in players.Values)player.Close();}
}
