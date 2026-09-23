using DesktopLife.Core;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public class SoundPackTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"DesktopLifeSounds",Guid.NewGuid().ToString("N"));
    [Fact]public void ImportCopiesValidFilesAndMissingSoundsUseBuiltin()
    {
        var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"pack.json"),JsonSerializer.Serialize(new SoundPackManifest("我的音效","測試作者","CC0")));
        File.WriteAllBytes(Path.Combine(source,"Meow.wav"),PetSoundWave.Create(PetSound.Meow));
        var library=new SoundPackLibrary(root);var pack=library.Import(source);
        Assert.NotNull(library.Resolve(pack.Id,PetSound.Meow));Assert.Null(library.Resolve(pack.Id,PetSound.Bell));
        Assert.Null(library.Resolve("../../source",PetSound.Meow));
        library.Remove(pack.Id);Assert.Empty(library.List());Assert.True(Directory.Exists(source));
    }
    [Fact]public void InvalidAudioIsRejectedBeforePublishingPack()
    {
        var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"pack.json"),JsonSerializer.Serialize(new SoundPackManifest("壞包","作者","CC0")));
        File.WriteAllBytes(Path.Combine(source,"Bell.wav"),new byte[100]);
        var library=new SoundPackLibrary(root);Assert.Throws<InvalidDataException>(()=>library.Import(source));Assert.Empty(library.List());
        Assert.Throws<InvalidDataException>(()=>SoundPackLibrary.ValidateWave(PetSoundWave.Create(PetSound.Meow)[..50]));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
