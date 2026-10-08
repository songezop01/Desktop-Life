using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesktopLife.Core;

namespace DesktopLife.App;

internal sealed class WalkManifest
{
    public int Version { get; set; }
    public WalkClip[] Clips { get; set; } = [];
}
internal sealed class WalkClip
{
    public string Character { get; set; } = "";
    public string Sheet { get; set; } = "";
    public double ReferenceHeight { get; set; }
    public double DurationSeconds { get; set; }
    public double StrideDip { get; set; }
    public int[][] Frames { get; set; } = [];
}

internal sealed class CompanionWalkAtlas
{
    private static readonly Dictionary<PetAppearance, CompanionWalkAtlas?> cache = [];
    public SpriteAssetFrame[] Frames { get; }
    public double Scale { get; }
    public double StrideDip { get; }
    public int TransparentPixels { get; }
    public static int LoadedCount => cache.Count(p => p.Value is not null);
    public string Source { get; }
    private CompanionWalkAtlas(PetAppearance kind)
    {
        var manifest = SpriteAssetReader.Manifest<WalkManifest>("Companions/walk-manifest.json");
        if (manifest.Version != 1) throw new InvalidDataException("Unsupported walk animation manifest.");
        var clip = manifest.Clips.Single(c => c.Character == kind.ToString());
        if (clip.Frames.Length != 8 || clip.ReferenceHeight <= 0 || clip.StrideDip <= 0)
            throw new InvalidDataException("Walk clip must define eight frames, scale reference and stride.");
        Source = clip.Sheet; StrideDip = clip.StrideDip;
        var sheet = new SpriteAssetReader("Companions/" + clip.Sheet, kind == PetAppearance.Girl ? 1536 : 1024);
        Frames = clip.Frames.Select(sheet.Extract).ToArray(); TransparentPixels = sheet.TransparentPixels;
        var referenceTarget = kind == PetAppearance.Girl ? 144 : 110;
        Scale = Math.Min(referenceTarget / (clip.ReferenceHeight * sheet.DecodeScale), 112d / Frames.Max(f => f.Width));
    }
    public static CompanionWalkAtlas? For(PetAppearance appearance)
    {
        if (cache.TryGetValue(appearance, out var result)) return result;
        try { result = new(appearance); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or System.IO.FileFormatException)
        { result = null; }
        cache[appearance] = result; return result;
    }
}
