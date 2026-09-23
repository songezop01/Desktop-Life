using System.Text.Json;
namespace DesktopLife.Core;
public sealed class LearningStore(string path)
{
    public LearningState Load()
    {
        if (!File.Exists(path)) return new();
        var state=JsonSerializer.Deserialize<LearningState>(File.ReadAllText(path)) ?? throw new InvalidDataException("空白學習資料。");
        state.Validate(); return state;
    }
    public void Save(LearningState state)
    {
        state.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary=path+".tmp";
        using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
        { JsonSerializer.Serialize(stream,state,new JsonSerializerOptions { WriteIndented=true }); stream.Flush(true); }
        if(File.Exists(path)) File.Replace(temporary,path,path+".bak"); else File.Move(temporary,path);
    }
}
