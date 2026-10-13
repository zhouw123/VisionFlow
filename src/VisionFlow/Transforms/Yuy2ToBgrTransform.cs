using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using OpenCvSharp;

namespace VisionFlow.Transforms;

/// <summary>
/// YUY2 解码转换。输入 YUY2 字节流，输出 BGR 图像。
/// 无参数。
/// </summary>
public sealed class Yuy2ToBgrTransform : TransformComponent
{
    /// <summary>
    /// 创建 YUY2 解码转换。
    /// </summary>
    public Yuy2ToBgrTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Yuy2ToBgr", queueCapacity, policy)
    {
    }

    /// <summary>
    /// YUY2 → BGR。
    /// </summary>
    protected override unsafe FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        if (input.SubType != MediaSubType.YUY2)
        {
            return input;
        }

        int w = input.Width, h = input.Height;
        int expected = w * h * 2;
        if (input.Data.Length < expected) return null;

        var bgr = new Mat();
        fixed (byte* pIn = input.Data)
        {
            using var yuy2 = Mat.FromPixelData(h, w, MatType.CV_8UC2, (IntPtr)pIn);
            Cv2.CvtColor(yuy2, bgr, ColorConversionCodes.YUV2BGR_YUY2);
        }

        var outBytes = new byte[bgr.Total() * bgr.ElemSize()];
        Marshal.Copy(bgr.Data, outBytes, 0, outBytes.Length);
        bgr.Dispose();

        return input.WithData(w, h, MediaSubType.RGB24, outBytes);
    }
}