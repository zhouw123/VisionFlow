using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms;

/// <summary>
/// 缩放转换。输入 BGR，输出 BGR。
/// 参数：水平缩放比例、垂直缩放比例。
/// double 参数用 lock 保护，保证跨线程读写安全。
/// </summary>
public sealed class ResizeTransform : TransformComponent
{
    private readonly object _scaleLock = new();
    private double _scaleX = 0.5;
    private double _scaleY = 0.5;

    /// <summary>
    /// 创建缩放转换。
    /// </summary>
    public ResizeTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Resize", queueCapacity, policy)
    {
        RegisterParameter(
            "ScaleX", "水平缩放",
            () => { lock (_scaleLock) return _scaleX; },
            v => { lock (_scaleLock) _scaleX = v; },
            min: 0.1, max: 4.0, step: 0.05,
            description: "宽度缩放比例（0.1 ~ 4.0）");

        RegisterParameter(
            "ScaleY", "垂直缩放",
            () => { lock (_scaleLock) return _scaleY; },
            v => { lock (_scaleLock) _scaleY = v; },
            min: 0.1, max: 4.0, step: 0.05,
            description: "高度缩放比例（0.1 ~ 4.0）");
    }

    /// <summary>当前水平缩放。</summary>
    public double ScaleX { get { lock (_scaleLock) return _scaleX; } }

    /// <summary>当前垂直缩放。</summary>
    public double ScaleY { get { lock (_scaleLock) return _scaleY; } }

    /// <summary>
    /// 处理一帧。
    /// </summary>
    protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        if (input.SubType != MediaSubType.RGB24)
        {
            return input;
        }

        int w = input.Width, h = input.Height;
        if (input.Data.Length < w * h * 3) return null;

        double sx, sy;
        lock (_scaleLock) { sx = _scaleX; sy = _scaleY; }

        // 若缩放比为 1，直接透传
        if (Math.Abs(sx - 1.0) < 0.001 && Math.Abs(sy - 1.0) < 0.001)
        {
            return input;
        }

        int newW = Math.Max(1, (int)(w * sx));
        int newH = Math.Max(1, (int)(h * sy));

        using var src = new Mat(h, w, MatType.CV_8UC3);
        Marshal.Copy(input.Data, 0, src.Data, input.Data.Length);

        using var dst = new Mat();
        Cv2.Resize(src, dst, new Size(newW, newH),
            0, 0, InterpolationFlags.Linear);

        var outBytes = new byte[dst.Total() * dst.ElemSize()];
        Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);

        return input.WithData(newW, newH, MediaSubType.RGB24, outBytes);
    }
}