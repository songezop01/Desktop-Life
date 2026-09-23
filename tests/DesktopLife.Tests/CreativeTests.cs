using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class CreativeTests
{
    [Theory] [InlineData(DoodlePattern.Heart)] [InlineData(DoodlePattern.Star)] [InlineData(DoodlePattern.Smile)] [InlineData(DoodlePattern.Sun)] [InlineData(DoodlePattern.Circle)] [InlineData(DoodlePattern.SimpleFace)] [InlineData(DoodlePattern.RandomLines)]
    public void EveryPatternProducesBoundedStrokes(DoodlePattern pattern){var strokes=DoodleGenerator.Generate(pattern,42);Assert.NotEmpty(strokes);Assert.All(strokes.SelectMany(s=>s),p=>{Assert.InRange(p.X,0,100);Assert.InRange(p.Y,0,100);});}
    [Fact] public void NotesReflectNeeds(){var n=new TemplateNoteGenerator();Assert.Contains("餓",n.Generate(new(){Hunger=90}));Assert.Contains("主人",n.Generate(new(){Loneliness=90}));}
}
