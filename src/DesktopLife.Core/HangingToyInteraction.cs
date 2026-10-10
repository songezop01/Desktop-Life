namespace DesktopLife.Core;

/// <summary>A hanging toy is shared; its shelf is still a cat-only support.</summary>
public static class HangingToyInteraction
{
    public const double CanonicalHeight = 144;
    public const double ContactRadius = 9;
    public static bool Allowed(PetAppearance character) => character is PetAppearance.Cat or PetAppearance.BorderCollie;

    public static HangingToyStance? Plan(PetAppearance character, RoomPoint anchor, RoomPlatform lowerShelf,
        RoomPlatform floor, BodyBounds bounds, double bodyWidth, double bodyHeight, double sceneScale,
        HangingToySilhouette? contactSilhouette = null)
    {
        if (!Allowed(character)) return null;
        if (new[] { anchor.X, anchor.Y, bodyWidth, bodyHeight, sceneScale }.Any(v => !double.IsFinite(v)) || bodyWidth <= 0 || bodyHeight <= 0 || sceneScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(bodyHeight));
        var scale = bodyHeight / CanonicalHeight;
        var support = character == PetAppearance.Cat ? lowerShelf : floor;
        if (support.Width <= 0 || Math.Abs(support.Y - support.EndY) > .001) return null;
        var feet = support.Y;
        var preferredLength = character == PetAppearance.Cat ? HangingToy.Length :
            Math.Clamp((feet - bodyHeight * .12 - anchor.Y) / sceneScale, HangingToy.Length, HangingToy.MaximumLength);
        // Stay beside the string and within the real support. Choosing the other
        // side near a screen edge changes facing, not the character's capability.
        // A compact room can put the maximum-length ball beside a different
        // part of the illustrated dog. Use the same future-pose silhouette as
        // contact, and shorten the real string only to a reachable, visible
        // sphere. The original cat string and all physical limits stay fixed.
        var minimumLength = contactSilhouette is not null && character == PetAppearance.BorderCollie
            ? HangingToy.Length : preferredLength;
        for (var ropeLength = preferredLength; ropeLength >= minimumLength; ropeLength -= 2)
        foreach (var facing in new[] { -1, 1 })
        {
            // The sphere sits beside the outline, not over the face of the
            // illustrated crouching/play pose. The reachable tip remains inside.
            var outside=character==PetAppearance.Cat?13:10;
            var targetX = facing < 0 ? -outside : 116+outside;
            var minimum = Math.Max(bounds.Left, support.X - bodyWidth / 2 + .5 * sceneScale);
            var maximum = Math.Min(bounds.Left + bounds.Width - bodyWidth, support.X + support.Width - bodyWidth / 2 - .5 * sceneScale);
            if (minimum > maximum) continue;
            var x = Math.Clamp(anchor.X - targetX * scale, minimum, maximum);
            var y = feet - bodyHeight;
            if (y < bounds.Top || feet > bounds.Top + bounds.Height) continue;
            var restingBall = new RoomPoint(anchor.X, anchor.Y + ropeLength * sceneScale);
            var ballX=(restingBall.X-x)/scale;
            if(facing<0&&ballX>=0||facing>0&&ballX<=116)continue;
            if (ContactPoint(character, restingBall, x, y, bodyHeight, sceneScale) is null) continue;
            if (contactSilhouette is not null && !contactSilhouette.IsSphereClear(
                new(ballX, (restingBall.Y - y) / scale), ContactRadius * sceneScale / scale)) continue;
            return new(x, y, facing, ropeLength, support, character == PetAppearance.Cat);
        }
        return null;
    }

    public static RoomPoint? ContactPoint(PetAppearance character, RoomPoint ball, double bodyX, double bodyY, double bodyHeight, double sceneScale)
    {
        if (!Allowed(character)) return null;
        var scale = bodyHeight / CanonicalHeight;
        if (!double.IsFinite(scale) || scale <= 0 || !double.IsFinite(sceneScale) || sceneScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(bodyHeight));
        var localX = (ball.X - bodyX) / scale;
        var localY = (ball.Y - bodyY) / scale;
        var readyX = localX < 58 ? 34 : 82;
        const double readyY = 118;
        var dx = readyX - localX; var dy = readyY - localY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var radius = ContactRadius * sceneScale / scale;
        var tipX = distance > .0001 ? localX + dx / distance * radius : localX;
        var tipY = distance > .0001 ? localY + dy / distance * radius : localY + radius;
        var shoulderX = localX < 58 ? 43 : 73;
        const double shoulderY = 113;
        if (tipX is < 1 or > 115 || tipY is < 76 or > 140 || Math.Sqrt((tipX - shoulderX) * (tipX - shoulderX) + (tipY - shoulderY) * (tipY - shoulderY)) > 52)
            return null;
        return new(bodyX + tipX * scale, bodyY + tipY * scale);
    }
}

public sealed record HangingToyStance(double X, double Y, int Facing, double RopeLength, RoomPlatform Support, bool UsesShelf);
