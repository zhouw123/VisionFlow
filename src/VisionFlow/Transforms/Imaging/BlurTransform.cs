using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 模糊。支持高斯/均值/中值三种。
/// 支持 BGR 或 GRAY 输入，输出保持输入格式。尺寸不变。
/// </summary>
public sealed class BlurTransform : TransformComponent
{
    private volatile int _blurType = 0;
    private volatile int _kernelSize = 5;
    private volatile int _sigmaX100 = 0;

    /// <summary>创建模糊组件。</summary>
    public BlurTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Blur", queueCapacity, policy)
    {
        RegisterParameter(
            "BlurType", "模糊类型",
            () => _blurType, v => _blurType = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(0, "高斯模糊"),
                new EnumOption(1, "均值模糊"),
                new EnumOption(2, "中值模糊"),
            });

        RegisterParameter(
            "KernelSize", "核大小",
            () => _kernelSize, v => _kernelSize = v,
            min: 1, max: 31, step: 2,
            description: "模糊核大小（奇数）");

        RegisterParameter(
            "SigmaX", "高斯标准差",
            () => _sigmaX100 / 100.0,
            v => _sigmaX100 = (int)(v * 100),
            min: 0, max: 10.0, step: 0.1,
            description: "仅高斯模糊使用，0 = 由核大小自动计算");
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
        if (ks < 1) ks = 1;

        byte[] outBytes;
        unsafe
        {
            fixed (byte* pSrc = input.Data)
            {
                var srcType = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
                using var src = Mat.FromPixelData(h, w, srcType, (IntPtr)pSrc);

                using var dst = new Mat();
                switch (_blurType)
                {
                    case 0:
                        Cv2.GaussianBlur(src, dst, new Size(ks, ks), _sigmaX100 / 100.0);
                        break;
                    case 1:
                        Cv2.Blur(src, dst, new Size(ks, ks));
                        break;
                    case 2:
                        if (ks >= 3) Cv2.MedianBlur(src, dst, ks);
                        else src.CopyTo(dst);
                        break;
                    default:
                        return input;
                }

                outBytes = new byte[dst.Total() * dst.ElemSize()];
                Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);
            }
        }

        return input.WithData(w, h, sub, outBytes);
    }
}