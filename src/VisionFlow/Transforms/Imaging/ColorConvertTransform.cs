using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 色彩空间转换。
/// 支持的转换：
///   BGR -> GRAY / RGB / HSV / YCrCb / YUV
///   GRAY -> BGR / RGB
///   RGB / HSV / YCrCb / YUV -> BGR
/// 输入输出都是 8 位/通道。
/// 源与目标相同时直接透传。
/// </summary>
public sealed class ColorConvertTransform : TransformComponent
{
    private volatile int _target = 1;  // 默认 BGR -> GRAY

    private const int Target_Bgr = 0;
    private const int Target_Gray = 1;
    private const int Target_Rgb = 2;
    private const int Target_Hsv = 3;
    private const int Target_YCrCb = 4;
    private const int Target_Yuv = 5;

    /// <summary>创建色彩空间转换。</summary>
    public ColorConvertTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("ColorConvert", queueCapacity, policy)
    {
        RegisterParameter(
            "Target", "目标格式",
            () => _target, v => _target = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(Target_Bgr, "BGR"),
                new EnumOption(Target_Gray, "灰度"),
                new EnumOption(Target_Rgb, "RGB"),
                new EnumOption(Target_Hsv, "HSV"),
                new EnumOption(Target_YCrCb, "YCrCb"),
                new EnumOption(Target_Yuv, "YUV"),
            });
    }

    /// <summary>处理一帧。</summary>
    protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        var srcSub = input.SubType;

        if (!FrameFormats.IsGray(srcSub) && !FrameFormats.Is3Channel(srcSub))
            return input;

        int w = input.Width, h = input.Height;
        int bpp = FrameFormats.GetBytesPerPixel(srcSub);
        if (input.Data.Length < w * h * bpp) return null;

        var targetSub = TargetToSubType(_target);

        // 源与目标相同：直接透传
        if (targetSub == srcSub) return input;

        // 用 unsafe 块保证 Mat 生命周期内 data 不被 GC 移动
        byte[] outBytes;
        int outChannels;

        unsafe
        {
            fixed (byte* pSrc = input.Data)
            {
                int srcType = FrameFormats.IsGray(srcSub) ? 1 : 3;
                var srcMat = Mat.FromPixelData(h, w,
                    srcType == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3,
                    (IntPtr)pSrc);

                using var dst = new Mat();
                if (!TryConvert(srcMat, dst, srcSub, _target))
                    return input;

                outChannels = dst.Channels();
                outBytes = new byte[dst.Total() * dst.ElemSize()];
                Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);
            }
        }

        return input.WithData(w, h, targetSub, outBytes);
    }

    private static bool TryConvert(Mat src, Mat dst, Guid srcSub, int target)
    {
        try
        {
            if (srcSub == MediaSubType.RGB24)  // BGR 输入
            {
                switch (target)
                {
                    case Target_Gray:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2GRAY);
                        return true;
                    case Target_Rgb:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2RGB);
                        return true;
                    case Target_Hsv:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2HSV);
                        return true;
                    case Target_YCrCb:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2YCrCb);
                        return true;
                    case Target_Yuv:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2YUV);
                        return true;
                    case Target_Bgr:
                        src.CopyTo(dst);
                        return true;
                    default:
                        return false;
                }
            }

            if (srcSub == FrameFormats.Gray8)
            {
                switch (target)
                {
                    case Target_Bgr:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.GRAY2BGR);
                        return true;
                    case Target_Rgb:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.GRAY2RGB);
                        return true;
                    default:
                        src.CopyTo(dst);
                        return true;
                }
            }

            if (srcSub == FrameFormats.Rgb8)
            {
                switch (target)
                {
                    case Target_Bgr:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.RGB2BGR);
                        return true;
                    case Target_Gray:
                        Cv2.CvtColor(src, dst, ColorConversionCodes.RGB2GRAY);
                        return true;
                    default:
                        src.CopyTo(dst);
                        return true;
                }
            }

            if (srcSub == FrameFormats.Hsv8)
            {
                if (target == Target_Bgr)
                {
                    Cv2.CvtColor(src, dst, ColorConversionCodes.HSV2BGR);
                    return true;
                }
                src.CopyTo(dst);
                return true;
            }

            if (srcSub == FrameFormats.YCrCb8)
            {
                if (target == Target_Bgr)
                {
                    Cv2.CvtColor(src, dst, ColorConversionCodes.YCrCb2BGR);
                    return true;
                }
                src.CopyTo(dst);
                return true;
            }

            if (srcSub == FrameFormats.Yuv8)
            {
                if (target == Target_Bgr)
                {
                    Cv2.CvtColor(src, dst, ColorConversionCodes.YUV2BGR);
                    return true;
                }
                src.CopyTo(dst);
                return true;
            }

            // 未知源格式
            src.CopyTo(dst);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Guid TargetToSubType(int target)
    {
        return target switch
        {
            Target_Bgr => MediaSubType.RGB24,
            Target_Gray => FrameFormats.Gray8,
            Target_Rgb => FrameFormats.Rgb8,
            Target_Hsv => FrameFormats.Hsv8,
            Target_YCrCb => FrameFormats.YCrCb8,
            Target_Yuv => FrameFormats.Yuv8,
            _ => MediaSubType.RGB24,
        };
    }
}