namespace VisionFlow.WpfSamples.Controls;

/// <summary>
/// A point in normalized coordinates: X and Y are in [0, 1],
/// relative to the video's display area.
/// </summary>
public readonly struct NormalizedPoint
{
    public NormalizedPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }

    public NormalizedPoint Clamped()
        => new(Math.Clamp(X, 0, 1), Math.Clamp(Y, 0, 1));

    public override string ToString() => $"({X:F3},{Y:F3})";
}