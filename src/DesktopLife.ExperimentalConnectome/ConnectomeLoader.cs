using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace DesktopLife.ExperimentalConnectome;
public interface IConnectomeSource { string Identity { get; } Stream OpenRead(); }
public sealed class LocalConnectomeSource(string path):IConnectomeSource
{ public string Identity=>Path.GetFullPath(path);public Stream OpenRead()=>File.OpenRead(path); }
public interface IConnectomeLoader { ConnectomeGraph Load(IConnectomeSource source,SimulationBudget budget); }
public sealed class JsonConnectomeLoader : IConnectomeLoader
{
    public ConnectomeGraph Load(IConnectomeSource source,SimulationBudget budget)
    {
        using var stream=source.OpenRead();
        if(!stream.CanSeek||stream.Length>Math.Min(64L*1024*1024,budget.MaxMemoryMB*1024L*1024/2))throw new InvalidDataException("Input exceeds parsing memory budget.");
        var graph=JsonSerializer.Deserialize<ConnectomeGraph>(stream)??throw new InvalidDataException("Empty graph.");
        graph.Validate(budget);return graph;
    }
}
public sealed class ConnectomeCache(string directory)
{
    public bool LastHit { get; private set; }
    public ConnectomeGraph Load(IConnectomeSource source,SimulationBudget budget)
    {
        using var stream=source.OpenRead();var hash=Convert.ToHexString(SHA256.HashData(stream));
        var path=Path.Combine(directory,hash+".schema1.json.gz");LastHit=false;
        if(File.Exists(path))
        {
            try
            {
                using var file=File.OpenRead(path);using var zip=new GZipStream(file,CompressionMode.Decompress);using var memory=new MemoryStream();
                var buffer=new byte[8192];int count;while((count=zip.Read(buffer))>0){if(memory.Length+count>64*1024*1024)throw new InvalidDataException("Cache too large.");memory.Write(buffer,0,count);}
                var graph=JsonSerializer.Deserialize<ConnectomeGraph>(memory.ToArray())??throw new InvalidDataException("Empty cache.");graph.Validate(budget);LastHit=true;return graph;
            }
            catch(Exception e) when(e is InvalidDataException or JsonException or IOException) { /* regenerate from validated source */ }
        }
        var parsed=new JsonConnectomeLoader().Load(source,budget);Directory.CreateDirectory(directory);
        using(var file=File.Create(path+".tmp"))using(var zip=new GZipStream(file,CompressionLevel.Fastest))JsonSerializer.Serialize(zip,parsed);
        File.Move(path+".tmp",path,true);return parsed;
    }
}
