using DirectShowLib;
using VisionFlow.Streaming;
using VisionFlow.Transforms;
using VisionFlow.Transforms.Imaging;

namespace VisionFlow.WpfSamples.Modes;

/// <summary>
/// The list of built-in processing modes and their component factories.
/// </summary>
public static class ProcessingModes
{
    /// <summary>All built-in modes in display order.</summary>
    public static IReadOnlyList<ProcessingMode> All { get; } = new List<ProcessingMode>
    {
        new("Original",   () => Array.Empty<FrameComponent>()),
        new("Gray",       () => new FrameComponent[] { MakeColorConvert("Gray") }),
        new("Canny",      () => new FrameComponent[]
        {
            MakeColorConvert("Gray"),
            new CannyTransform(),
        }),
        new("Threshold",  () => new FrameComponent[] { new ThresholdTransform() }),
        new("Contour",    () => new FrameComponent[]
        {
            MakeColorConvert("Gray"),
            new CannyTransform(),
            new ContourTransform(),
        }),
        new("Sharpen",    () => new FrameComponent[] { new SharpenTransform() }),
        new("Blur",       () => new FrameComponent[] { new BlurTransform() }),
        new("Denoise",    () => new FrameComponent[] { new DenoiseTransform() }),
        new("Morph-Dilate", () => new FrameComponent[]
        {
            MakeColorConvert("Gray"),
            MakeMorphology("Dilate"),
        }),
        new("Morph-Gradient", () => new FrameComponent[]
        {
            MakeColorConvert("Gray"),
            MakeMorphology("Gradient"),
        }),
        new("Sobel",      () => new FrameComponent[] { new GradientTransform() }),
        new("HSV",        () => new FrameComponent[] { MakeColorConvert("HSV") }),
        new("Crop",       () => new FrameComponent[] { MakeCrop() }),
        new("Perspective",() => new FrameComponent[] { MakePerspective() }),
    };

    // ---- Factory helpers ----

    private static ColorConvertTransform MakeColorConvert(string target)
    {
        var t = new ColorConvertTransform();
        t.SetParameter("Target", target switch
        {
            "Gray" => 1,
            "RGB" => 2,
            "HSV" => 3,
            "YCrCb" => 4,
            "YUV" => 5,
            _ => 0,
        });
        return t;
    }

    private static MorphologyTransform MakeMorphology(string op)
    {
        var t = new MorphologyTransform();
        t.SetParameter("Operation", op switch
        {
            "Erode" => 0,
            "Dilate" => 1,
            "Open" => 2,
            "Close" => 3,
            "Gradient" => 4,
            "TopHat" => 5,
            "BlackHat" => 6,
            _ => 0,
        });
        return t;
    }

    private static CropTransform MakeCrop()
    {
        var t = new CropTransform();
        t.SetRegion(160, 120, 320, 240);
        return t;
    }

    private static PerspectiveTransform MakePerspective()
    {
        var t = new PerspectiveTransform();
        t.SetSourcePoints(80, 40, 560, 40, 620, 440, 20, 440);
        t.SetDestinationSize(640, 480);
        return t;
    }
}