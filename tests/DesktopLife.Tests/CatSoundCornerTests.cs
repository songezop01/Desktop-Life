using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class CatSoundCornerTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void CornerToyEscapesEvenWithPetBlockingIt(bool right)
    {
        var bounds=new BodyBounds(0,0,1000,800);var toy=new InteractiveToy(right?960:0,760);
        var petX=right?884:0;
        for(var i=0;i<180;i++){toy.Step(.016,bounds);ToyContactPhysics.Separate(toy,petX,656,bounds);}
        Assert.InRange(toy.X,100,860);Assert.InRange(toy.Y,0,760);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void SideWallReturnsSlowToyWithUsefulInwardSpeed(bool right)
    {
        var toy=new InteractiveToy(right?960:0,500);toy.Kick(right?30:-30,0);toy.Step(.016,new(0,0,1000,800));
        Assert.True(right?toy.VelocityX<-150:toy.VelocityX>150);
    }
    [Theory][InlineData(PetSound.Meow)][InlineData(PetSound.Purr)][InlineData(PetSound.Bell)]
    public void GeneratedSoundIsBoundedNonSilentPcm(PetSound kind)
    {
        var bytes=PetSoundWave.Create(kind);Assert.Equal("RIFF",System.Text.Encoding.ASCII.GetString(bytes,0,4));
        Assert.Equal(bytes.Length-44,BitConverter.ToInt32(bytes,40));Assert.Equal(22050,BitConverter.ToInt32(bytes,24));
        var peak=Enumerable.Range(0,(bytes.Length-44)/2).Max(i=>Math.Abs((int)BitConverter.ToInt16(bytes,44+i*2)));
        Assert.InRange(peak,100,32000);Assert.True(bytes.Length>10000);
    }
    [Fact]public void AudioSettingsRejectInvalidVolumeAndDefaultToModerateLevel()
    {Assert.Equal(.35,new AppSettings().AudioVolume);Assert.Throws<InvalidDataException>(()=>(new AppSettings{AudioVolume=double.NaN}).Validate());}
}
