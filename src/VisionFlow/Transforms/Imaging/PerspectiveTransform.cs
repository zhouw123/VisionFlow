using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 四点透视变换。
/// 输入 BGR，输出指定尺寸的 BGR。
/// 源四点坐标由参数指定，目标四点固定为输出图像的四个角。
/// </summary>
public sealed class PerspectiveTransform : TransformComponent
{
    // 源图像上的四个点
    private volatile int _srcX0 = 0;
    private volatile int _srcY0 = 0;
    private volatile int _srcX1 = 640;
    private volatile int _srcY1 = 0;
    private volatile int _srcX2 = 640;
    private volatile int _srcY2 = 480;
    private volatile int _srcX3 = 0;
    private volatile int _srcY3 = 480;

    // 目标尺寸
    private volatile int _dstWidth = 640;
    private volatile int _dstHeight = 480;

    /// <summary>创建透视变换。</summary>
    public PerspectiveTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Perspective", queueCapacity, policy)
    {
        RegisterParameter("SrcX0", "源点0 X", () => _srcX0, v => _srcX0 = v, min: 0, max: 10000, step: 1);
        RegisterParameter("SrcY0", "源点0 Y", () => _srcY0, v => _srcY0 = v, min: 0, max: 10000, step: 1);
        RegisterParameter("SrcX1", "源点1 X", () => _srcX1, v => _srcX1 = v, min: 0, max: 10000, step: 1);
        RegisterParameter("SrcY1", "源点1 Y", () => _srcY1, v => _srcY1 = v, min: 0, max: 10000, step: 1);
        RegisterParameter("SrcX2", "源点2 X", () => _srcX2, v => _srcX2 = v, min: 0, max: 10000, step: 1);
        RegisterParameter("SrcY2", "源点2 Y", () => _srcY2, v => _srcY2 = v, min: 0, max: 10000, step: 1);
        RegisterParameter("SrcX3", "源点3 X", () => _srcX3, v => _srcX3 = v, min: 0, max: 10000, step: 1);
        RegisterParameter("SrcY3", "源点3 Y", () => _srcY3, v => _srcY3 = v, min: 0, max: 10000, step: 1);
        RegisterParameter("DstWidth", "输出宽", () => _dstWidth, v => _dstWidth = v, min: 16, max: 4096, step: 1);
        RegisterParameter("DstHeight", "输出高", () => _dstHeight, v => _dstHeight = v, min: 16, max: 4096, step: 1);
    }

    /// <summary>
    /// 一次设置四个源点。
    /// 走 SetParameter 路径，触发 ParameterChanged 事件，让 UI 保持同步。
    /// </summary>
    public void SetSourcePoints(
        int x0, int y0, int x1, int y1,
        int x2, int y2, int x3, int y3)
    {
        SetParameter("SrcX0", x0); SetParameter("SrcY0", y0);
        SetParameter("SrcX1", x1); SetParameter("SrcY1", y1);
        SetParameter("SrcX2", x2); SetParameter("SrcY2", y2);
        SetParameter("SrcX3", x3); SetParameter("SrcY3", y3);
    }

    /// <summary>设置输出尺寸。</summary>
    public void SetDestinationSize(int width, int height)
    {
        _dstWidth = width;
        _dstHeight = height;
    }

    /// <summary>处理一帧。</summary>
    protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        if (!FrameFormats.Is3Channel(input.SubType)) return input;
        if (input.SubType != MediaSubType.RGB24) return input;  // 只支持 BGR

        int w = input.Width, h = input.Height;
        if (input.Data.Length < w * h * 3) return null;

        int dstW = _dstWidth;
        int dstH = _dstHeight;
        if (dstW < 1 || dstH < 1) return input;

        using var src = new Mat(h, w, MatType.CV_8UC3);
        Marshal.Copy(input.Data, 0, src.Data, w * h * 3);

        // 源点（注意：要在图像范围内）
        var srcPts = new[]
        {
            new Point2f(Clamp(_srcX0, 0, w - 1), Clamp(_srcY0, 0, h - 1)),
            new Point2f(Clamp(_srcX1, 0, w - 1), Clamp(_srcY1, 0, h - 1)),
            new Point2f(Clamp(_srcX2, 0, w - 1), Clamp(_srcY2, 0, h - 1)),
            new Point2f(Clamp(_srcX3, 0, w - 1), Clamp(_srcY3, 0, h - 1)),
        };

        // 目标点：输出图的四个角
        var dstPts = new[]
        {
            new Point2f(0, 0),
            new Point2f(dstW - 1, 0),
            new Point2f(dstW - 1, dstH - 1),
            new Point2f(0, dstH - 1),
        };

        using var matrix = Cv2.GetPerspectiveTransform(srcPts, dstPts);
        using var dst = new Mat();
        Cv2.WarpPerspective(src, dst, matrix, new Size(dstW, dstH));

        var outBytes = new byte[dst.Total() * dst.ElemSize()];
        Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);

        return input.WithData(dstW, dstH, MediaSubType.RGB24, outBytes);
    }

    private static int Clamp(int v, int min, int max)
        => Math.Max(min, Math.Min(max, v));
}