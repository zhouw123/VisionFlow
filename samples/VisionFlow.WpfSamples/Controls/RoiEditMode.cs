namespace VisionFlow.WpfSamples.Controls;

/// <summary>Which kind of region is being edited.</summary>
public enum RoiEditMode
{
    /// <summary>Axis-aligned rectangle. Used for crop, Canny ROI, etc.</summary>
    Rect,

    /// <summary>Four-point quadrilateral. Used for perspective transform.</summary>
    Quad,
}