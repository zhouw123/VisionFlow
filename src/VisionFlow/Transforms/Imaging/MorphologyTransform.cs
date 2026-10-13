using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 形态学运算：腐蚀、膨胀、开运算、闭运算、梯度、顶帽、黑帽。
/// 支持 BGR 或 GRAY 输入，输出保持输入格式。尺寸不变。
/// </summary>
public sealed class MorphologyTransform : TransformComponent
{
    private volatile int _operation = 0;
    private volatile int _shape = 0;
    private volatile int _kernelSize = 3;
    private volatile int _iterations = 1;

    /// <summary>创建形态学组件。</summary>
    public MorphologyTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Morphology", queueCapacity, policy)
    {
        RegisterParameter(
            "Operation", "运算",
            () => _operation, v => _operation = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(0, "腐蚀 Erode"),
                new EnumOption(1, "膨胀 Dilate"),
                new EnumOption(2, "开运算 Open"),
                new EnumOption(3, "闭运算 Close"),
                new EnumOption(4, "梯度 Gradient"),
                new EnumOption(5, "顶帽 TopHat"),
                new EnumOption(6, "黑帽 BlackHat"),
            });

        RegisterParameter(
            "Shape", "核形状",
            () => _shape, v => _shape = v,
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption(0, "矩形"),
                new EnumOption(1, "十字"),
                new EnumOption(2, "椭圆"),
            });

        RegisterParameter(
            "KernelSize", "核大小",
            () => _kernelSize, v => _kernelSize = v,
            min: 1, max: 31, step: 2,
            description: "结构元素大小（奇数）");

        RegisterParameter(
            "Iterations", "迭代次数",
            () => _iterations, v => _iterations = v,
            min: 1, max: 10, step: 1);
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

        var shape = _shape switch
        {
            0 => MorphShapes.Rect,
            1 => MorphShapes.Cross,
            2 => MorphShapes.Ellipse,
            _ => MorphShapes.Rect,
        };

        var op = _operation switch
        {
            0 => MorphTypes.Erode,
            1 => MorphTypes.Dilate,
            2 => MorphTypes.Open,
            3 => MorphTypes.Close,
            4 => MorphTypes.Gradient,
            5 => MorphTypes.TopHat,
            6 => MorphTypes.BlackHat,
            _ => MorphTypes.Erode,
        };

        byte[] outBytes;
        unsafe
        {
            fixed (byte* pSrc = input.Data)
            {
                var srcType = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
                using var src = Mat.FromPixelData(h, w, srcType, (IntPtr)pSrc);

                using var kernel = Cv2.GetStructuringElement(shape, new Size(ks, ks));

                using var dst = new Mat();
                Cv2.MorphologyEx(src, dst, op, kernel, iterations: _iterations);

                outBytes = new byte[dst.Total() * dst.ElemSize()];
                Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);
            }
        }

        return input.WithData(w, h, sub, outBytes);
    }
}