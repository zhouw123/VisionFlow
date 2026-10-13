using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 轮廓检测 + 多边形逼近。
/// 输入 BGR 或 GRAY，先在灰度上用 Canny 或阈值找边缘，再检测轮廓，
/// 对每个轮廓做多边形逼近，最后把多边形画回 BGR 输出。
/// 输出始终为 BGR。
/// </summary>
public sealed class ContourTransform : TransformComponent
{
    private volatile int _edgeMethod = 0;      // 0=Canny, 1=Threshold
    private volatile int _cannyLow = 50;
    private volatile int _cannyHigh = 150;
    private volatile int _threshold = 128;
    private volatile int _minArea = 100;
    private volatile int _epsilonX1000 = 20;   // 多边形逼近精度
    private volatile int _lineColor = unchecked((int)0xFF00FF00);
    private volatile int _lineThickness = 2;
    private volatile int _mode = 1;            // 0=仅画轮廓, 1=近似多边形
    private volatile int _drawBox = 0;         // 是否画外接矩形
    private volatile int _boxColor = unchecked((int)0xFFFF0000);

    /// <summary>创建轮廓检测组件。</summary>
    public ContourTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Contour", queueCapacity, policy)
    {
        RegisterParameter("EdgeMethod", "边缘方法",
            () => _edgeMethod, v => _edgeMethod = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(0, "Canny"),
                new EnumOption(1, "阈值"),
            });

        RegisterParameter("CannyLow", "Canny 低阈值",
            () => _cannyLow, v => _cannyLow = v,
            min: 0, max: 500, step: 1);

        RegisterParameter("CannyHigh", "Canny 高阈值",
            () => _cannyHigh, v => _cannyHigh = v,
            min: 0, max: 500, step: 1);

        RegisterParameter("Threshold", "阈值",
            () => _threshold, v => _threshold = v,
            min: 0, max: 255, step: 1);

        RegisterParameter("MinArea", "最小面积",
            () => _minArea, v => _minArea = v,
            min: 0, max: 100000, step: 10,
            description: "过滤掉小于此面积的轮廓");

        RegisterParameter("Epsilon", "逼近精度",
            () => _epsilonX1000 / 1000.0,
            v => _epsilonX1000 = (int)(v * 1000),
            min: 0.001, max: 0.1, step: 0.001,
            description: "多边形逼近的精度（占周长比例）");

        RegisterParameter("LineColor", "线条颜色",
            () => _lineColor, v => _lineColor = v,
            kind: ParameterKind.Color);

        RegisterParameter("LineThickness", "线条厚度",
            () => _lineThickness, v => _lineThickness = v,
            min: 1, max: 10, step: 1);

        RegisterParameter("Mode", "绘制模式",
            () => _mode, v => _mode = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(0, "原始轮廓"),
                new EnumOption(1, "近似多边形"),
            });

        RegisterParameter("DrawBox", "绘制外接矩形",
            () => _drawBox == 1, v => _drawBox = v ? 1 : 0);

        RegisterParameter("BoxColor", "外接矩形颜色",
            () => _boxColor, v => _boxColor = v,
            kind: ParameterKind.Color);
    }

    /// <summary>当前处理过的轮廓总数（供诊断）。</summary>
    public long LastContourCount { get; private set; }

    /// <summary>处理一帧。</summary>
    protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        var srcSub = input.SubType;
        if (!FrameFormats.IsGray(srcSub) && !FrameFormats.Is3Channel(srcSub))
            return input;

        int w = input.Width, h = input.Height;
        int bpp = FrameFormats.GetBytesPerPixel(srcSub);
        if (input.Data.Length < w * h * bpp) return null;

        // 读入 src
        using var src = new Mat(h, w,
            FrameFormats.IsGray(srcSub) ? MatType.CV_8UC1 : MatType.CV_8UC3);
        Marshal.Copy(input.Data, 0, src.Data, w * h * bpp);

        // 转灰度
        using var gray = new Mat();
        if (FrameFormats.IsGray(srcSub))
            src.CopyTo(gray);
        else
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // 边缘图
        using var binary = new Mat();
        if (_edgeMethod == 0)
        {
            Cv2.Canny(gray, binary, _cannyLow, _cannyHigh);
        }
        else
        {
            Cv2.Threshold(gray, binary, _threshold, 255, ThresholdTypes.Binary);
        }

        // 找轮廓
        Cv2.FindContours(binary, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        LastContourCount = contours.Length;

        // 输出 BGR
        using var dst = new Mat();
        if (FrameFormats.IsGray(srcSub))
            Cv2.CvtColor(src, dst, ColorConversionCodes.GRAY2BGR);
        else
            src.CopyTo(dst);

        var lineScalar = ArgbToBgrScalar(_lineColor);
        var boxScalar = ArgbToBgrScalar(_boxColor);

        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < _minArea) continue;

            if (_mode == 0)
            {
                // 原始轮廓
                Cv2.DrawContours(dst, new[] { contour }, -1, lineScalar, _lineThickness);
            }
            else
            {
                // 多边形逼近
                double epsilon = _epsilonX1000 / 1000.0 * Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);
                Cv2.Polylines(dst, new[] { approx }, true, lineScalar, _lineThickness);
            }

            if (_drawBox == 1)
            {
                var rect = Cv2.BoundingRect(contour);
                Cv2.Rectangle(dst, rect, boxScalar, _lineThickness);
            }
        }

        var outBytes = new byte[dst.Total() * dst.ElemSize()];
        Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);

        return input.WithData(w, h, MediaSubType.RGB24, outBytes);
    }

    private static Scalar ArgbToBgrScalar(int argb)
    {
        return new Scalar(
            (argb >> 0) & 0xFF,
            (argb >> 8) & 0xFF,
            (argb >> 16) & 0xFF);
    }
}