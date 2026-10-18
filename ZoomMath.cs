using System;
using System.Globalization;

namespace SilverpineMods.CameraZoom;

internal static class ZoomMath
{
    internal const float Minimum = 0.2f, Maximum = 2.0f;
    internal const int SliderSteps = 18;

    internal static float Normalize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
        return (float)Math.Round((decimal)Math.Max(Minimum, Math.Min(Maximum, value)), 1,
            MidpointRounding.AwayFromZero);
    }

    internal static bool TryParse(string text, out float value)
    {
        text = text.Trim().TrimEnd('x', 'X', '×').Trim();
        bool parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        if (!parsed || float.IsNaN(value) || float.IsInfinity(value) || value < Minimum || value > Maximum)
            return false;
        value = Normalize(value);
        return true;
    }
}

internal readonly struct FogBounds : IEquatable<FogBounds>
{
    internal static FogBounds Native => new(-10, 9, -5, 6);
    internal readonly int MinX, MaxX, MinY, MaxY;
    internal FogBounds(int minX, int maxX, int minY, int maxY)
    { MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY; }

    internal static FogBounds Cover(float minX, float maxX, float minY, float maxY) => new(
        (int)Math.Floor(minX) - 1, (int)Math.Ceiling(maxX) + 1,
        (int)Math.Floor(minY) - 1, (int)Math.Ceiling(maxY) + 1);
    public bool Equals(FogBounds other) => MinX == other.MinX && MaxX == other.MaxX && MinY == other.MinY && MaxY == other.MaxY;
    public override bool Equals(object? other) => other is FogBounds bounds && Equals(bounds);
    public override int GetHashCode() => (MinX, MaxX, MinY, MaxY).GetHashCode();
}
