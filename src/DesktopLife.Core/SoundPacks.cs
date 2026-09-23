using System.Text;
using System.Text.Json;
namespace DesktopLife.Core;

public sealed record SoundPackInfo(string Id,string Name,string Author,string License,string Directory);
public sealed record SoundPackManifest(string Name,string Author,string License,int Version=1);
public sealed class SoundPackLibrary(string root)
{
    public string Folder=>Path.Combine(root,"sound-packs");
    public IReadOnlyList<SoundPackInfo> List()
    {
        Directory.CreateDirectory(Folder);var result=new List<SoundPackInfo>();
        foreach(var directory in System.IO.Directory.EnumerateDirectories(Folder).Where(p=>!Path.GetFileName(p).StartsWith('.')))
        {try{var manifest=ReadManifest(directory);result.Add(new(Path.GetFileName(directory),manifest.Name,manifest.Author,manifest.License,directory));}catch(Exception ex) when(ex is IOException or JsonException or InvalidDataException){}}
        return result.OrderBy(p=>p.Name).ToArray();
    }
    private static SoundPackManifest ReadManifest(string folder)
    {
        if(new FileInfo(Path.Combine(folder,"pack.json")).Length>16384)throw new InvalidDataException("音效包資訊檔過大。");
        var manifest=JsonSerializer.Deserialize<SoundPackManifest>(File.ReadAllText(Path.Combine(folder,"pack.json")))??throw new InvalidDataException("缺少音效包資訊。");
        if(manifest.Version!=1||string.IsNullOrWhiteSpace(manifest.Name)||manifest.Name.Length>80||string.IsNullOrWhiteSpace(manifest.Author)||string.IsNullOrWhiteSpace(manifest.License))
            throw new InvalidDataException("pack.json 需包含 Name、Author、License，Version 必須為 1。");
        return manifest;
    }
    public SoundPackInfo Import(string source)
    {
        var manifest=ReadManifest(source);var present=Enum.GetValues<PetSound>().Select(s=>Path.Combine(source,s+".wav")).Where(File.Exists).ToArray();
        if(present.Length==0)throw new InvalidDataException("音效包至少需要 Meow.wav、Purr.wav 或 Bell.wav 其中一個。");
        var contents=present.ToDictionary(p=>Path.GetFileName(p),ReadWave);
        Directory.CreateDirectory(Folder);var id=Guid.NewGuid().ToString("N");var staging=Path.Combine(Folder,"."+id);var destination=Path.Combine(Folder,id);
        Directory.CreateDirectory(staging);
        try
        {
            File.WriteAllText(Path.Combine(staging,"pack.json"),JsonSerializer.Serialize(manifest));
            foreach(var file in contents)File.WriteAllBytes(Path.Combine(staging,file.Key!),file.Value);
            Directory.Move(staging,destination);return new(id,manifest.Name,manifest.Author,manifest.License,destination);
        }
        finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
    }
    public string? Resolve(string? id,PetSound sound)
    {
        if(id is null)return null;
        var pack=List().FirstOrDefault(p=>p.Id==id);if(pack is null)return null;
        var path=Path.Combine(pack.Directory,sound+".wav");
        if(!File.Exists(path))return null;
        ReadWave(path);return path;
    }
    public void Remove(string id)
    {
        if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("音效包識別碼無效。");
        var pack=List().SingleOrDefault(p=>p.Id==id)??throw new InvalidDataException("找不到指定的音效包。");
        // Only library-assigned identifiers can resolve here, never an external path.
        Directory.Delete(pack.Directory,true);
    }
    private static byte[] ReadWave(string path)
    {
        if(new FileInfo(path).Length>10*1024*1024)throw new InvalidDataException("音效檔超過 10 MB。");
        var bytes=File.ReadAllBytes(path);ValidateWave(bytes);return bytes;
    }
    public static void ValidateWave(byte[] data)
    {
        if(data.Length<44||data.Length>10*1024*1024)throw new InvalidDataException("WAV 必須小於 10 MB，且含有效音訊。");
        using var reader=new BinaryReader(new MemoryStream(data),Encoding.ASCII);
        if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("音效須為 PCM WAV。");
        var declared=reader.ReadUInt32();if(declared>data.Length-8)throw new InvalidDataException("WAV 檔案不完整。");
        if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("音效須為 WAV。");
        int rate=0,channels=0,bits=0;long length=0;
        while(reader.BaseStream.Position+8<=data.Length)
        {
            var tag=new string(reader.ReadChars(4));var size=reader.ReadUInt32();var start=reader.BaseStream.Position;
            if(size>data.Length-start)throw new InvalidDataException("WAV 區段不完整。");
            if(tag=="fmt ")
            {
                if(size<16||reader.ReadUInt16()!=1)throw new InvalidDataException("僅接受未壓縮的 PCM WAV。");
                channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadUInt32();var align=reader.ReadUInt16();bits=reader.ReadUInt16();
                if(channels is <1 or >2||rate is <8000 or >96000||bits is not (8 or 16 or 24 or 32)||align!=channels*bits/8)throw new InvalidDataException("WAV 的聲道、取樣率或位元深度不支援。");
            }
            if(tag=="data")length+=size;
            reader.BaseStream.Position=Math.Min(data.Length,start+size+(size%2));
        }
        if(rate==0||length==0||length%(channels*bits/8)!=0||length/(double)(rate*channels*bits/8)>30)
            throw new InvalidDataException("WAV 必須含音訊，且長度不超過 30 秒。");
    }
}
