using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesktopLife.Core;

namespace DesktopLife.App;

internal sealed class TransitionManifest
{
    public int Version {get;set;}
    public double DurationSeconds {get;set;}
    public TransitionClip[] Clips {get;set;}=[];
}
internal sealed class TransitionClip
{
    public string Character {get;set;}="";
    public string Sheet {get;set;}="";
    public int[][] Frames {get;set;}=[];
}
internal sealed class CompanionTransitionAtlas
{
    private static readonly Dictionary<PetAppearance,CompanionTransitionAtlas?> cache=[];
    public static int LoadedCount=>cache.Count(p=>p.Value is not null);
    public SpriteAssetFrame[] Frames {get;}
    public double StandScale {get;}
    public double SitScale {get;}
    public double DurationSeconds {get;}
    public string Source {get;}
    private CompanionTransitionAtlas(PetAppearance kind)
    {
        var manifest=SpriteAssetReader.Manifest<TransitionManifest>("Companions/transition-manifest.json");
        var clip=manifest.Clips.Single(c=>c.Character==kind.ToString());
        if(manifest.Version!=1||clip.Frames.Length!=4||manifest.DurationSeconds is <=0 or >1)
            throw new InvalidDataException("Sit/stand animation needs four frames and a bounded duration.");
        var reader=new SpriteAssetReader("Companions/"+clip.Sheet,kind==PetAppearance.Girl?1536:1024);
        Frames=clip.Frames.Select(reader.Extract).ToArray();Source=clip.Sheet;DurationSeconds=manifest.DurationSeconds;
        StandScale=144d/Frames[0].Height;
        // A girl's seated pose stays naturally shorter. Small animal sketches
        // need a slight progressive scale calibration to meet their 80/104 DIP
        // seated reference; the endpoint itself does not jump when a clip ends.
        SitScale=kind==PetAppearance.Girl?StandScale:144d/Frames[3].Height;
    }
    public static CompanionTransitionAtlas? For(PetAppearance kind)
    {
        if(cache.TryGetValue(kind,out var result))return result;
        try{result=new(kind);}
        catch(Exception ex)when(ex is IOException or InvalidOperationException or NotSupportedException or System.IO.FileFormatException){result=null;}
        cache[kind]=result;return result;
    }
}
