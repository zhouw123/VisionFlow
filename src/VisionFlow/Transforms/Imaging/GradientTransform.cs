using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 梯度计算。Sobel / Scharr / Laplacian。
/// 输入 BGR 或 GRAY，输出始终为 GRAY（单通道，绝对值归一化后）。
/// </summary>
public sealed class GradientTransform : TransformComponent
{
    private volatile int _method = 0;
    private volatile int _kernelSize = 3;
    private volatile int _scale = 1;
    private volatile int _useAbs = 1;

    /// <summary>创建梯度组件。</summary>
    public GradientTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Gradient", queueCapacity, policy)
    {
        RegisterParameter(
            "Method", "方法",
            () => _method, v => _method = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(0, "Sobel X"),
                new EnumOption(1, "Sobel Y"),
                new EnumOption(2, "Sobel XY"),
                new EnumOption(3, "Scharr X"),
                new EnumOption(4, "Laplacian"),
            });

        RegisterParameter(
            "KernelSize", "核大小",
            () => _kernelSize, v => _kernelSize = v,
            min: 1, max: 7, step: 2);

        RegisterParameter(
            "Scale", "缩放",
            () => _scale, v => _scale = v,
            min: 1, max: 10, step: 1);

        RegisterParameter(
            "UseAbs", "取绝对值",
            () => _useAbs == 1, v => _useAbs = v ? 1 : 0);
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

        using var grad = new Mat();
        try
        {
            int ddepth = MatType.CV_16S;
            int ks = _kernelSize;
            if (ks % 2 == 0) ks++;
            if (ks < 1) ks = 1;

            switch (_method)
            {
                case 0:
                    Cv2.Sobel(gray, grad, ddepth, 1, 0, ksize: ks, scale: _scale);
                    break;
                case 1:
                    Cv2.Sobel(gray, grad, ddepth, 0, 1, ksize: ks, scale: _scale);
                    break;
                case 2:
                    Cv2.Sobel(gray, grad, ddepth, 1, 1, ksize: ks, scale: _scale);
                    break;
                case 3:
                    Cv2.Scharr(gray, grad, ddepth, 1, 0, _scale);
                    break;
                case 4:
                    Cv2.Laplacian(gray, grad, ddepth, ksize: ks, scale: _scale);
                    break;
                default:
                    gray.CopyTo(grad);
                    break;
            }

            // 16 位 → 8 位
            using var abs = new Mat();
            if (_useAbs == 1)
            {
                Cv2.ConvertScaleAbs(grad, abs);
            }
            else
            {
                grad.ConvertTo(abs, MatType.CV_8U);
            }

            var outBytes = new byte[abs.Total() * abs.ElemSize()];
            Marshal.Copy(abs.Data, outBytes, 0, outBytes.Length);

            return input.WithData(w, h, FrameFormats.Gray8, outBytes);
        }
        finally
        {
            if (disposeGray) gray.Dispose();
        }
    }
}