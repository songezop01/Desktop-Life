using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopLife.App;

internal sealed record SpriteAssetFrame(BitmapSource Image, byte[] Alpha, Int32Rect SourceBounds)
{
    public int Width => SourceBounds.Width;
    public int Height => SourceBounds.Height;
}

internal sealed class SpriteAssetReader
{
    private readonly byte[] pixels;
    public int Width { get; }
    public int Height { get; }
    public double DecodeScale { get; }
    public int TransparentPixels { get; }

    public static T Manifest<T>(string path)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/" + path))?.Stream
            ?? throw new FileNotFoundException("Sprite manifest is missing.", path);
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Sprite manifest is empty.");
    }

    public SpriteAssetReader(string path, int decodeWidth = 1536, bool localFile = false)
    {
        using var stream = localFile ? File.OpenRead(path) : Application.GetResourceStream(new Uri("pack://application:,,,/Assets/" + path))?.Stream
            ?? throw new FileNotFoundException(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var sourceWidth=decoder.Frames[0].PixelWidth;
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = Math.Min(decodeWidth,sourceWidth);
        bitmap.UriSource = localFile ? new Uri(Path.GetFullPath(path)) : new Uri("pack://application:,,,/Assets/" + path);
        bitmap.EndInit(); bitmap.Freeze();
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        Width = converted.PixelWidth; Height = converted.PixelHeight;
        // Source PNG dimensions, without retaining another full decoder.
        DecodeScale = (double)Width / sourceWidth;
        pixels = new byte[Width * Height * 4]; converted.CopyPixels(pixels, Width * 4, 0);
        TransparentPixels = Enumerable.Range(0, Width * Height).Count(i => pixels[i * 4 + 3] == 0);
        if (TransparentPixels < Width * Height / 5) throw new InvalidDataException("Sprite sheet must have a real alpha background.");
    }

    // Rectangles and order come from the manifest. Alpha only removes padding,
    // neighbouring cell fragments and detached generation noise inside a frame.
    public SpriteAssetFrame Extract(int[] rectangle)
    {
        if (rectangle.Length != 4) throw new InvalidDataException("Sprite frame needs four rectangle coordinates.");
        var left = Math.Clamp((int)Math.Round(rectangle[0] * DecodeScale), 0, Width - 1);
        var top = Math.Clamp((int)Math.Round(rectangle[1] * DecodeScale), 0, Height - 1);
        var right = Math.Clamp((int)Math.Round((rectangle[0] + rectangle[2]) * DecodeScale), left + 1, Width);
        var bottom = Math.Clamp((int)Math.Round((rectangle[1] + rectangle[3]) * DecodeScale), top + 1, Height);
        var frameWidth = right - left; var frameHeight = bottom - top;
        var visited = new bool[frameWidth * frameHeight]; var queue = new int[visited.Length];
        int[] largest = [];
        for (var i = 0; i < visited.Length; i++)
        {
            if (visited[i] || Alpha(i) < 24) continue;
            var start = 0; var end = 1; queue[0] = i; visited[i] = true;
            while (start < end)
            {
                var p = queue[start++]; var x = p % frameWidth; var y = p / frameWidth;
                if (x > 0) Add(p - 1); if (x + 1 < frameWidth) Add(p + 1);
                if (y > 0) Add(p - frameWidth); if (y + 1 < frameHeight) Add(p + frameWidth);
            }
            if (end > largest.Length) largest = queue.Take(end).ToArray();
            void Add(int p) { if (visited[p] || Alpha(p) < 24) return; visited[p] = true; queue[end++] = p; }
        }
        if (largest.Length < 300) throw new InvalidDataException("Sprite frame lacks a complete painted silhouette.");
        var minX = largest.Min(p => p % frameWidth); var maxX = largest.Max(p => p % frameWidth);
        var minY = largest.Min(p => p / frameWidth); var maxY = largest.Max(p => p / frameWidth);
        var width = maxX - minX + 1; var height = maxY - minY + 1;
        var data = new byte[width * height * 4]; var alpha = new byte[width * height];
        foreach (var p in largest)
        {
            var x = p % frameWidth; var y = p / frameWidth;
            var sourceOffset = ((top + y) * Width + left + x) * 4;
            var offset = (y - minY) * width + x - minX;
            Buffer.BlockCopy(pixels, sourceOffset, data, offset * 4, 4); alpha[offset] = pixels[sourceOffset + 3];
        }
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, data, width * 4); image.Freeze();
        return new(image, alpha, new(left + minX, top + minY, width, height));
        byte Alpha(int p) => pixels[((top + p / frameWidth) * Width + left + p % frameWidth) * 4 + 3];
    }
}
