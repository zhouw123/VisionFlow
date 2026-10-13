using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 二值化。支持固定阈值、OTSU、自适应阈值。
/// 输入 BGR 或 GRAY，输出始终为 GRAY（单通道）。
/// </summary>
public sealed class ThresholdTransform : TransformComponent
{
    private volatile int _method = 0;
    private volatile int _threshold = 128;
    private volatile int _maxValue = 255;
    private volatile int _adaptiveBlockSize = 11;
    private volatile int _adaptiveC = 2;

    /// <summary>创建二值化组件。</summary>
    public ThresholdTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Threshold", queueCapacity, policy)
    {
        RegisterParameter(
            "Method", "方法",
            () => _method, v => _method = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(0, "固定阈值"),
                new EnumOption(1, "OTSU 自动"),
                new EnumOption(2, "自适应均值"),
                new EnumOption(3, "自适应高斯"),
            });

        RegisterParameter(
            "Threshold", "阈值",
            () => _threshold, v => _threshold = v,
            min: 0, max: 255, step: 1,
            description: "固定阈值时使用");

        RegisterParameter(
            "MaxValue", "最大值",
            () => _maxValue, v => _maxValue = v,
            min: 0, max: 255, step: 1);

        RegisterParameter(
            "AdaptiveBlockSize", "自适应块大小",
            () => _adaptiveBlockSize, v => _adaptiveBlockSize = v,
            min: 3, max: 51, step: 2,
            description: "自适应方法用，必须为奇数");

        RegisterParameter(
            "AdaptiveC", "自适应 C",
            () => _adaptiveC, v => _adaptiveC = v,
            min: -10, max: 20, step: 1,
            description: "自适应方法用，从均值中减去的常数");
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

        using var src = new Mat(h, w,
            FrameFormats.IsGray(srcSub) ? MatType.CV_8UC1 : MatType.CV_8UC3);
        Marshal.Copy(input.Data, 0, src.Data, w * h * bpp);

        // 如果是 3 通道，先转灰度
        Mat gray;
        bool disposeGray = false;
        if (FrameFormats.IsGray(srcSub))
        {
            gray = src;
        }
        else
        {
            gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            disposeGray = true;
        }

        using var dst = new Mat();
        try
        {
            switch (_method)
            {
                case 0:
                    Cv2.Threshold(gray, dst, _threshold, _maxValue,
                        ThresholdTypes.Binary);
                    break;
                case 1:
                    Cv2.Threshold(gray, dst, 0, _maxValue,
                        ThresholdTypes.Binary | ThresholdTypes.Otsu);
                    break;
                case 2:
                    int bs1 = _adaptiveBlockSize;
                    if (bs1 % 2 == 0) bs1++;
                    if (bs1 < 3) bs1 = 3;
                    Cv2.AdaptiveThreshold(gray, dst, _maxValue,
                        AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary,
                        bs1, _adaptiveC);
                    break;
                case 3:
                    int bs2 = _adaptiveBlockSize;
                    if (bs2 % 2 == 0) bs2++;
                    if (bs2 < 3) bs2 = 3;
                    Cv2.AdaptiveThreshold(gray, dst, _maxValue,
                        AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary,
                        bs2, _adaptiveC);
                    break;
                default:
                    gray.CopyTo(dst);
                    break;
            }
        }
        finally
        {
            if (disposeGray) gray.Dispose();
        }

        var outBytes = new byte[dst.Total() * dst.ElemSize()];
        Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);

        return input.WithData(w, h, FrameFormats.Gray8, outBytes);
    }
}