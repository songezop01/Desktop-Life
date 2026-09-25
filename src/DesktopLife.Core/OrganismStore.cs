using System.Text.Json;
using System.Text.Json.Serialization;
namespace DesktopLife.Core;
public sealed record OrganismSnapshot
{
    [JsonRequired] public int SchemaVersion { get; init; } = 3;
    [JsonRequired] public PetSnapshot Pet { get; init; } = new();
    [JsonRequired] public LearningState Learning { get; init; } = new();
    [JsonRequired] public PersonalityProfile Personality { get; init; } = new();
    [JsonRequired] public AppSettings Settings { get; init; } = new();
    public void Validate()
    {
        if(SchemaVersion!=3||Pet is null||Learning is null||Personality is null||Settings is null)throw new InvalidDataException("Unknown organism schema or missing data.");
        Pet.Validate();Learning.Validate();Personality.Validate();Settings.Validate();
    }
}
public sealed class OrganismStore(string directory)
{
    public string PathName=>Path.Combine(directory,"organism.json");
    public bool Exists=>new[]{PathName,PathName+".bak",PathName+".bak.1",PathName+".bak.2"}.Any(File.Exists);
    public string? RecoveryMessage {get;private set;}
    private static OrganismSnapshot Read(string path)
    {
        var json=File.ReadAllText(path);
        using(var document=JsonDocument.Parse(json))
        {
            if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("存檔格式無效。");
            if(document.RootElement.TryGetProperty("SchemaVersion",out var version)&&version.ValueKind==JsonValueKind.Number&&version.TryGetInt32(out var number)&&number>3)
                throw new NotSupportedException("存檔來自較新的程式版本，請更新程式；原檔未被修改。");
        }
        var value=JsonSerializer.Deserialize<OrganismSnapshot>(json)??throw new InvalidDataException("存檔內容為空。");
        if(value.SchemaVersion==2)value=value with{SchemaVersion=3};
        value.Validate();return value;
    }
    private static bool Damaged(Exception ex)=>ex is JsonException or InvalidDataException or ArgumentException;
    public OrganismSnapshot LoadOrMigrate(AppSettings settings,DateTimeOffset now)
    {
        if(Exists)return Load();
        // Explicit migration from M1–M14 split schema-1 files. Originals are preserved.
        var pet=new PetStateStore(Path.Combine(directory,"pet-state.json")).Load()??new PetSnapshot{LastSaveTime=now};
        var learning=new LearningStore(Path.Combine(directory,"learning.json")).Load();
        var migrated=new OrganismSnapshot{Pet=pet,Learning=learning,Settings=settings};migrated.Validate();return migrated;
    }
    public OrganismSnapshot Load()
    {
        try{return Read(PathName);}
        catch(Exception failure) when(Damaged(failure)||failure is FileNotFoundException)
        {
            foreach(var backup in new[]{PathName+".bak",PathName+".bak.1",PathName+".bak.2"})
            {
                if(!File.Exists(backup))continue;
                OrganismSnapshot recovered;
                try{recovered=Read(backup);}catch(Exception ex) when(Damaged(ex)||ex is NotSupportedException){continue;}
                var preserved=PreserveDamaged();
                WriteAtomic(recovered,PathName);
                RecoveryMessage=$"已從 {Path.GetFileName(backup)} 找回存檔。損毀原檔保留於 {preserved}。";
                return recovered;
            }
            throw new InvalidDataException("存檔與備份皆無法讀取；所有原檔均已保留。請匯回已匯出的存檔。",failure);
        }
    }
    public void Save(OrganismSnapshot snapshot)
    {
        snapshot.Validate();Directory.CreateDirectory(directory);
        if(File.Exists(PathName))
        {
            try
            {
                Read(PathName);
                if(File.Exists(PathName+".bak.1"))File.Copy(PathName+".bak.1",PathName+".bak.2",true);
                if(File.Exists(PathName+".bak"))File.Copy(PathName+".bak",PathName+".bak.1",true);
                File.Copy(PathName,PathName+".bak",true);
            }
            catch(Exception ex) when(Damaged(ex)){PreserveDamaged();}
        }
        WriteAtomic(snapshot,PathName);
    }
    private string PreserveDamaged()
    {
        var name=PathName+".damaged-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        if(File.Exists(PathName))File.Copy(PathName,name,false);
        return name;
    }
    private static void WriteAtomic(OrganismSnapshot snapshot,string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
        try
        {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {JsonSerializer.Serialize(stream,snapshot,new JsonSerializerOptions{WriteIndented=true});stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public void Export(string destination)=>WriteAtomic(Load(),destination);
    public void StageImport(string source)=>WriteAtomic(Read(source),Path.Combine(directory,"organism.import.json"));
    public bool ApplyPendingImport()
    {
        var pending=Path.Combine(directory,"organism.import.json");
        if(!File.Exists(pending))return false;
        var imported=Read(pending);Save(imported);
        File.Move(pending,pending+".applied-"+Guid.NewGuid().ToString("N"));
        RecoveryMessage="已套用匯入存檔；原本的寵物存檔保留在備份，桌面圖示備份沒有變動。";
        return true;
    }
}
