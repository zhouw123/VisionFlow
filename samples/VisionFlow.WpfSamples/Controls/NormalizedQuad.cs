namespace VisionFlow.WpfSamples.Controls;

/// <summary>
/// A quadrilateral in normalized coordinates. Points are ordered:
/// P0 = top-left, P1 = top-right, P2 = bottom-right, P3 = bottom-left.
/// All coordinates are in [0, 1].
/// </summary>
public readonly struct NormalizedQuad
{
    public NormalizedQuad(NormalizedPoint p0, NormalizedPoint p1,
                          NormalizedPoint p2, NormalizedPoint p3)
    {
        P0 = p0; P1 = p1; P2 = p2; P3 = p3;
    }

    public NormalizedPoint P0 { get; }
    public NormalizedPoint P1 { get; }
    public NormalizedPoint P2 { get; }
    public NormalizedPoint P3 { get; }

    public NormalizedPoint this[int index] => index switch
    {
        0 => P0,
        1 => P1,
        2 => P2,
        3 => P3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    /// <summary>Full-frame quad: four corners of [0,1] x [0,1].</summary>
    public static NormalizedQuad FullFrame => new(
        new NormalizedPoint(0, 0),
        new NormalizedPoint(1, 0),
        new NormalizedPoint(1, 1),
        new NormalizedPoint(0, 1));

    /// <summary>Create a quad with one point replaced.</summary>
    public NormalizedQuad WithPoint(int index, NormalizedPoint newPoint)
    {
        var pts = new[] { P0, P1, P2, P3 };
        pts[index] = newPoint;
        return new NormalizedQuad(pts[0], pts[1], pts[2], pts[3]);
    }

    /// <summary>
    /// Check whether the quad is convex and non-self-intersecting.
    /// Uses the sign of the cross product of consecutive edges.
    /// </summary>
    public bool IsConvex()
    {
        var pts = new[] { P0, P1, P2, P3 };
        int sign = 0;

        for (int i = 0; i < 4; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % 4];
            var c = pts[(i + 2) % 4];

            double cross = (b.X - a.X) * (c.Y - b.Y)
                         - (b.Y - a.Y) * (c.X - b.X);

            if (Math.Abs(cross) < 1e-9) continue;  // collinear, ignore

            int s = cross > 0 ? 1 : -1;
            if (sign == 0) sign = s;
            else if (sign != s) return false;
        }

        return true;
    }

    public override string ToString()
        => $"{P0} {P1} {P2} {P3}";
}