using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using DesktopLife.App;
using DesktopLife.Core;

internal static partial class Program
{
    private static void ValidateTransitionCandidates(string root)
    {
        var candidateRoot=Path.GetFullPath("artifacts/design/0.11/transition-candidates");
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(candidateRoot,"candidate-manifest.json")));
        var sheet=new DrawingVisual();var cases=new List<object>();
        using(var drawing=sheet.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.FloralWhite,null,new Rect(0,0,464,432));
            foreach(var clip in manifest.RootElement.GetProperty("clips").EnumerateArray())
            {
                var kind=Enum.Parse<PetAppearance>(clip.GetProperty("character").GetString()!);
                var reader=new SpriteAssetReader(Path.Combine(candidateRoot,clip.GetProperty("file").GetString()!),1536,true);
                var frames=clip.GetProperty("frames").EnumerateArray().Select(e=>reader.Extract(e.EnumerateArray().Select(p=>p.GetInt32()).ToArray())).ToArray();
                var scale=Math.Min(144d/frames.Max(f=>f.Height),112d/frames.Max(f=>f.Width));var hashes=new HashSet<string>();
                for(var i=0;i<frames.Length;i++)
                {
                    var pose=new DrawingVisual();
                    using(var context=pose.RenderOpen())context.DrawImage(frames[i].Image,new Rect((116-frames[i].Width*scale)/2,144-frames[i].Height*scale,frames[i].Width*scale,frames[i].Height*scale));
                    var frame=Render(pose,232,288);var pixels=Pixels(frame);hashes.Add(Convert.ToHexString(SHA256.HashData(pixels)));
                    Require(Enumerable.Range(232*286,232*2).Any(p=>pixels[p*4+3]>24),"Candidate grounded foot "+kind+"/"+i);
                    drawing.DrawImage(frame,new Rect(i*116,(int)kind*144,116,144));
                }
                Require(hashes.Count==4,"Four unique transition candidate frames "+kind);
                var oldAtlas=CompanionSpriteAtlas.For(kind)!;var oldSeatedFrame=kind==PetAppearance.Girl?3:0;
                cases.Add(new{Character=kind.ToString(),DistinctFrames=4,ReferenceScale=scale,VisibleHeights=frames.Select(f=>f.Height*scale).ToArray(),Grounded=true,
                    CandidateSeatedHeight=frames[3].Height*scale,ExistingSeatedHeight=Math.Min(oldAtlas.Scale,112d/oldAtlas.Frames[oldSeatedFrame].Width)*oldAtlas.Frames[oldSeatedFrame].Height,
                    Integrated=false,Reason="Candidate must replace seated reference consistently; do not concatenate with differently sized v1 sitting poses."});
            }
        }
        Save(Render(sheet,928,864),Path.Combine(root,"sit-transition-candidates.png"));
        File.WriteAllText(Path.Combine(root,"transition-candidate-checks.json"),JsonSerializer.Serialize(new{Status="PASS",RuntimeIntegrated=false,Cases=cases},new JsonSerializerOptions{WriteIndented=true}));
    }
}
