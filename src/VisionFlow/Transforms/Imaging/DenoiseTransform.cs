using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 降噪。支持双边滤波和非局部均值。
/// 支持 BGR 或 GRAY 输入，输出保持输入格式。尺寸不变。
/// 
/// 性能要点：
///   BilateralFilter 的 d 参数直接决定核大小，d 越大越慢。
///   本实现固定 d 在 5-9，用 Strength 控制颜色容差。
///   ProcessScale &lt; 1 时先降采样处理再放大回原尺寸，
///   速度提升约为 1/scale^2。
/// </summary>
public sealed class DenoiseTransform : TransformComponent
{
    private volatile int _denoiseType = 0;
    private volatile int _strengthX10 = 50;
    private volatile int _colorStrengthX10 = 50;
    private volatile int _processScaleX100 = 100;   // 100 = 原分辨率

    /// <summary>创建降噪组件。</summary>
    public DenoiseTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Denoise", queueCapacity, policy)
    {
        RegisterParameter(
            "DenoiseType", "降噪类型",
            () => _denoiseType, v => _denoiseType = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(0, "双边滤波（快）"),
                new EnumOption(1, "非局部均值（很慢）"),
            });

        RegisterParameter(
            "Strength", "强度",
            () => _strengthX10 / 10.0,
            v => _strengthX10 = (int)(v * 10),
            min: 0, max: 30.0, step: 0.5,
            description: "双边滤波控制颜色容差；NLMeans 控制滤波强度");

        RegisterParameter(
            "ColorStrength", "颜色强度",
            () => _colorStrengthX10 / 10.0,
            v => _colorStrengthX10 = (int)(v * 10),
            min: 0, max: 30.0, step: 0.5,
            description: "仅 NLMeans + BGR 输入时使用");

        RegisterParameter(
            "ProcessScale", "处理分辨率",
            () => _processScaleX100 / 100.0,
            v => _processScaleX100 = (int)(v * 100),
            min: 0.2, max: 1.0, step: 0.05,
            description: "小于 1 时降采样处理再放大，速度提升约 1/scale^2。"
                + "0.5 时约快 4 倍。");
    }

    /// <summary>处理一帧。</summary>
    protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        var sub = input.SubType;
        int channels = FrameFormats.GetChannelCount(sub);
        if (channels == 0) return input;

        int w = input.Width, h = input.Height;
        if (input.Data.Length < w * h * channels) return null;

        double scale = _processScaleX100 / 100.0;
        bool needResize = scale < 0.999;

        byte[] outBytes;
        unsafe
        {
            fixed (byte* pSrc = input.Data)
            {
                var srcType = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
                using var src = Mat.FromPixelData(h, w, srcType, (IntPtr)pSrc);

                // 可选降采样
                Mat workSrc;
                bool disposeWorkSrc = false;
                int workW = w, workH = h;

                if (needResize)
                {
                    workW = Math.Max(16, (int)(w * scale));
                    workH = Math.Max(16, (int)(h * scale));
                    var small = new Mat();
                    Cv2.Resize(src, small, new Size(workW, workH),
                        0, 0, InterpolationFlags.Area);
                    workSrc = small;
                    disposeWorkSrc = true;
                }
                else
                {
                    workSrc = src;
                }

                try
                {
                    using var denoisedSmall = new Mat();
                    ApplyDenoise(workSrc, denoisedSmall, channels);

                    Mat finalDst;
                    bool disposeFinal = false;

                    if (needResize)
                    {
                        finalDst = new Mat();
                        Cv2.Resize(denoisedSmall, finalDst, new Size(w, h),
                            0, 0, InterpolationFlags.Linear);
                        disposeFinal = true;
                    }
                    else
                    {
                        finalDst = denoisedSmall;
                    }

                    try
                    {
                        outBytes = new byte[w * h * channels];
                        // dst 尺寸始终是 w x h（无论是否经过缩放）
                        int copyBytes = Math.Min(outBytes.Length,
                            (int)(finalDst.Total() * finalDst.ElemSize()));
                        Marshal.Copy(finalDst.Data, outBytes, 0, copyBytes);
                    }
                    finally
                    {
                        if (disposeFinal) finalDst.Dispose();
                    }
                }
                finally
                {
                    if (disposeWorkSrc) workSrc.Dispose();
                }
            }
        }

        return input.WithData(w, h, sub, outBytes);
    }

    private void ApplyDenoise(Mat src, Mat dst, int channels)
    {
        double strength = _strengthX10 / 10.0;
        double colorStrength = _colorStrengthX10 / 10.0;

        if (_denoiseType == 0)
        {
            // 双边滤波。d 决定核大小，固定在 5-9。
            // Strength 控制 sigmaColor（颜色容差），sigmaSpace 固定为 5。
            int d = 5;
            if (strength > 15.0) d = 9;
            else if (strength > 5.0) d = 7;

            double sigmaColor = Math.Max(1.0, strength * 8.0);
            double sigmaSpace = 5.0;

            Cv2.BilateralFilter(src, dst, d, sigmaColor, sigmaSpace);
        }
        else
        {
            // 非局部均值
            if (channels == 3)
            {
                Cv2.FastNlMeansDenoisingColored(
                    src, dst,
                    h: (float)strength,
                    hColor: (float)colorStrength,
                    templateWindowSize: 7,
                    searchWindowSize: 15);
            }
            else
            {
                Cv2.FastNlMeansDenoising(
                    src, dst,
                    h: (float)strength,
                    templateWindowSize: 7,
                    searchWindowSize: 15);
            }
        }
    }
}