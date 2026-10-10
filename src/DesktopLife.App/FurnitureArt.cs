using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using DesktopLife.Core;

namespace DesktopLife.App;

internal sealed class FurnitureManifest
{
    public int Version { get; set; }
    public string Sheet { get; set; } = "";
    public FurnitureSpriteEntry[] Furniture { get; set; } = [];
}
internal sealed class FurnitureSpriteEntry
{
    public string Kind { get; set; } = "";
    public string? Sheet { get; set; }
    public int[] Rect { get; set; } = [];
    public double[][] Supports { get; set; } = [];
    public double? Foreground { get; set; }
}

public static class FurnitureArt
{
    private sealed record Asset(SpriteAssetFrame Frame, FurnitureSpriteEntry Entry, double DecodeScale);
    private static readonly Dictionary<FurnitureKind, Asset> assets = [];
    private static bool attempted;
    public static string? Failure { get; private set; }
    public static int LoadedCount { get { EnsureLoaded(); return assets.Count; } }

    private static void EnsureLoaded()
    {
        if (attempted) return; attempted = true;
        try
        {
            var manifest = SpriteAssetReader.Manifest<FurnitureManifest>("Furniture/furniture-manifest.json");
            if (manifest.Version != 1 || manifest.Furniture.Length != Enum.GetValues<FurnitureKind>().Length)
                throw new InvalidDataException("Furniture manifest must contain every furniture kind.");
            foreach (var group in manifest.Furniture.GroupBy(e => e.Sheet ?? manifest.Sheet))
            {
                var reader = new SpriteAssetReader("Furniture/" + group.Key);
                foreach (var entry in group)
                {
                    if (!Enum.TryParse<FurnitureKind>(entry.Kind, out var kind) || !Enum.IsDefined(kind) || assets.ContainsKey(kind))
                        throw new InvalidDataException("Furniture manifest has an unknown or duplicate kind.");
                    assets.Add(kind, new(reader.Extract(entry.Rect), entry, reader.DecodeScale));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or System.IO.FileFormatException)
        { Failure = ex.Message; assets.Clear(); }
    }

    private static Rect Bounds(Asset asset, double width, double height)
    {
        var scale = Math.Min(width / asset.Frame.Width, height / asset.Frame.Height);
        return new((width - asset.Frame.Width * scale) / 2, height - asset.Frame.Height * scale,
            asset.Frame.Width * scale, asset.Frame.Height * scale);
    }
    public static IReadOnlyList<RoomPlatform> Platforms(FurnitureKind kind, double width, double height)
    {
        EnsureLoaded(); if (!assets.TryGetValue(kind, out var asset)) return [];
        var bounds = Bounds(asset, width, height); var scale = bounds.Width / asset.Frame.Width;
        return asset.Entry.Supports.Select(p => new RoomPlatform(
            bounds.X + (p[0] * asset.DecodeScale - asset.Frame.SourceBounds.X) * scale,
            p[1] * asset.DecodeScale * scale,
            bounds.Y + (p[2] * asset.DecodeScale - asset.Frame.SourceBounds.Y) * scale,
            bounds.Y + (p[3] * asset.DecodeScale - asset.Frame.SourceBounds.Y) * scale)).ToArray();
    }
    public static Point Contact(FurnitureKind kind,double sourceX,double sourceY,double width,double height)
    {
        EnsureLoaded();if(!assets.TryGetValue(kind,out var asset))return new(45,31);
        var bounds=Bounds(asset,width,height);var scale=bounds.Width/asset.Frame.Width;
        return new(bounds.X+(sourceX*asset.DecodeScale-asset.Frame.SourceBounds.X)*scale,
            bounds.Y+(sourceY*asset.DecodeScale-asset.Frame.SourceBounds.Y)*scale);
    }
    public static bool Draw(DrawingContext drawing, FurnitureKind kind, double width, double height, bool foregroundOnly = false,bool removeStaticTeaser=false)
    {
        EnsureLoaded(); if (!assets.TryGetValue(kind, out var asset)) return false;
        var bounds = Bounds(asset, width, height);
        if(removeStaticTeaser&&kind==FurnitureKind.CatTree)
        {
            var start=Contact(kind,40,126,width,height);var end=Contact(kind,96,220,width,height);
            drawing.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(bounds),new RectangleGeometry(new Rect(start,end))));
        }
        if (foregroundOnly)
        {
            if (asset.Entry.Foreground is not { } sourceY) return true;
            var y = bounds.Y + (sourceY * asset.DecodeScale - asset.Frame.SourceBounds.Y) * bounds.Height / asset.Frame.Height;
            drawing.PushClip(new RectangleGeometry(new(0, Math.Clamp(y, 0, height), width, Math.Max(0, height - y))));
        }
        drawing.DrawImage(asset.Frame.Image, bounds);
        if (foregroundOnly) drawing.Pop();
        if(removeStaticTeaser&&kind==FurnitureKind.CatTree)drawing.Pop();
        return true;
    }
    internal static bool PaintedAt(FurnitureKind kind,Point point,double width,double height)
    {
        EnsureLoaded();if(!assets.TryGetValue(kind,out var asset))return false;
        var bounds=Bounds(asset,width,height);if(!bounds.Contains(point))return false;
        var x=Math.Clamp((int)((point.X-bounds.X)/bounds.Width*asset.Frame.Width),0,asset.Frame.Width-1);
        var y=Math.Clamp((int)((point.Y-bounds.Y)/bounds.Height*asset.Frame.Height),0,asset.Frame.Height-1);
        return asset.Frame.Alpha[y*asset.Frame.Width+x]>=24;
    }
    public static object Diagnostic
    {
        get
        {
            EnsureLoaded();
            return new { LoadedCount = assets.Count, Failure, Kinds = assets.Select(p => new
            { Kind = p.Key.ToString(), p.Value.Frame.Width, p.Value.Frame.Height, Supports = p.Value.Entry.Supports.Length, p.Value.Entry.Foreground }).ToArray() };
        }
    }
}

public sealed class FurnitureSpriteVisual : FrameworkElement
{
    private readonly FurnitureKind kind;
    private readonly bool foreground;
    private readonly bool removeStaticTeaser;
    public FurnitureSpriteVisual(FurnitureKind kind, double width, double height, bool foreground = false,bool removeStaticTeaser=false,bool interactive=false)
    { this.kind = kind; this.foreground = foreground;this.removeStaticTeaser=removeStaticTeaser; Width = width; Height = height; IsHitTestVisible = interactive; RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality); }
    protected override void OnRender(DrawingContext drawing) => FurnitureArt.Draw(drawing, kind, Width, Height, foreground,removeStaticTeaser);
    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters)=>FurnitureArt.PaintedAt(kind,parameters.HitPoint,Width,Height)?new PointHitTestResult(this,parameters.HitPoint):null;
}
