using System;
using DesktopLife.Core;

namespace DesktopLife.App;

// One world scale keeps the girl's body and the animals' bodies in proportion.
// Anchors are measured from the common, ground-aligned sprite viewport.
public sealed record CharacterGeometry(double Width, double Height, double SupportWidth,
    double FootPivotX, double FootY, double SeatY, double HeadY, double HandY, double PawY)
{
    public const double CanonicalWidth = 116;
    public const double CanonicalHeight = 144;
    public double Scale => Height / CanonicalHeight;

    public static CharacterGeometry For(PetAppearance appearance, double sceneScale = 1)
    {
        if (!double.IsFinite(sceneScale) || sceneScale <= 0 || sceneScale > 4)
            throw new ArgumentOutOfRangeException(nameof(sceneScale));
        var height = (appearance switch { PetAppearance.Girl => 256d, PetAppearance.BorderCollie => 104d, _ => 80d }) * sceneScale;
        var width = height * CanonicalWidth / CanonicalHeight;
        var support = appearance == PetAppearance.Girl ? width * .24 : width * .5;
        return new(width, height, support, width / 2, height,
            height * (appearance == PetAppearance.Girl ? .74 : .82),
            height * (appearance == PetAppearance.Girl ? .17 : .31),
            height * (appearance == PetAppearance.Girl ? .58 : .83), height * .94);
    }
}
