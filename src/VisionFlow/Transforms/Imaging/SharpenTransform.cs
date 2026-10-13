using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 锐化。unsharp mask：原图 + strength * (原图 - 高斯模糊)。
/// 支持 BGR 或 GRAY 输入，输出保持输入格式。尺寸不变。
/// </summary>
public sealed class SharpenTransform : TransformComponent
{
    private volatile int _strengthX100 = 100;
    private volatile int _kernelSize = 5;

    /// <summary>创建锐化组件。</summary>
    public SharpenTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Sharpen", queueCapacity, policy)
    {
        RegisterParameter(
            "Strength", "强度",
            () => _strengthX100 / 100.0,
            v => _strengthX100 = (int)(v * 100),
            min: 0, max: 5.0, step: 0.05,
            description: "锐化强度，0 = 不锐化，5 = 强锐化");

        RegisterParameter(
            "KernelSize", "核大小",
            () => _kernelSize, v => _kernelSize = v,
            min: 3, max: 15, step: 2,
            description: "高斯模糊核大小（奇数）");
    }

    /// <summary>处理一帧。</summary>
    protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        var sub = input.SubType;
        int channels = FrameFormats.GetChannelCount(sub);
        if (channels == 0) return input;

        int w = input.Width, h = input.Height;
        if (input.Data.Length < w * h * channels) return null;

        int ks = _kernelSize;
        if (ks % 2 == 0) ks++;
        if (ks < 3) ks = 3;

        double strength = _strengthX100 / 100.0;
        if (strength <= 0.001) return input;

        byte[] outBytes;
        unsafe
        {
            fixed (byte* pSrc = input.Data)
            {
                var srcType = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
                using var src = Mat.FromPixelData(h, w, srcType, (IntPtr)pSrc);

                using var blurred = new Mat();
                Cv2.GaussianBlur(src, blurred, new Size(ks, ks), 0);

                using var dst = new Mat();
                Cv2.AddWeighted(src, 1 + strength, blurred, -strength, 0, dst);

                outBytes = new byte[dst.Total() * dst.ElemSize()];
                Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);
            }
        }

        return input.WithData(w, h, sub, outBytes);
    }
}