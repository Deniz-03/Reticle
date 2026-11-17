namespace Reticle.App.Overlay;

/// <summary>Integer rectangle in device pixels.</summary>
public readonly record struct RectI(int Left, int Top, int Width, int Height);

/// <summary>Integer point in device pixels.</summary>
public readonly record struct PointI(int X, int Y);
