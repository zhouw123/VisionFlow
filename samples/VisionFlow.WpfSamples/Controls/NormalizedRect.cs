namespace VisionFlow.WpfSamples.Controls;

/// <summary>
/// An axis-aligned rectangle in normalized coordinates.
/// X, Y are the top-left corner; Width, Height are extents.
/// All values are in [0, 1].
/// </summary>
public readonly struct NormalizedRect
{
    public NormalizedRect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>Full-frame: (0, 0, 1, 1).</summary>
    public static NormalizedRect FullFrame => new(0, 0, 1, 1);

    public override string ToString()
        => $"({X:F3},{Y:F3}) {Width:F3}x{Height:F3}";
}