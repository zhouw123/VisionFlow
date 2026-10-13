using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms.Imaging;

/// <summary>
/// 裁剪。输入 BGR 或 GRAY，输出相同格式的指定子区域。
/// </summary>
public sealed class CropTransform : TransformComponent
{
    private volatile int _x = 0;
    private volatile int _y = 0;
    private volatile int _width = 640;
    private volatile int _height = 480;

    /// <summary>创建裁剪组件。</summary>
    public CropTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Crop", queueCapacity, policy)
    {
        RegisterParameter("X", "起点 X", () => _x, v => _x = v, min: 0, max: 10000, step: 1);
        RegisterParameter("Y", "起点 Y", () => _y, v => _y = v, min: 0, max: 10000, step: 1);
        RegisterParameter("Width", "宽度", () => _width, v => _width = v, min: 1, max: 10000, step: 1);
        RegisterParameter("Height", "高度", () => _height, v => _height = v, min: 1, max: 10000, step: 1);
    }

    /// <summary>
    /// 一次设置裁剪区域。
    /// 走 SetParameter 路径，触发 ParameterChanged 事件，让 UI 保持同步。
    /// </summary>
    public void SetRegion(int x, int y, int width, int height)
    {
        SetParameter("X", x);
        SetParameter("Y", y);
        SetParameter("Width", width);
        SetParameter("Height", height);
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

        // 裁剪区域（考虑边界）
        int cx = Math.Max(0, Math.Min(w - 1, _x));
        int cy = Math.Max(0, Math.Min(h - 1, _y));
        int cw = Math.Min(w - cx, _width);
        int ch = Math.Min(h - cy, _height);
        if (cw <= 0 || ch <= 0) return input;

        int channels = FrameFormats.IsGray(srcSub) ? 1 : 3;
        var outBytes = new byte[cw * ch * channels];

        // 逐行拷贝
        int rowBytes = cw * channels;
        for (int row = 0; row < ch; row++)
        {
            int srcOffset = ((cy + row) * w + cx) * channels;
            int dstOffset = row * rowBytes;
            Buffer.BlockCopy(input.Data, srcOffset, outBytes, dstOffset, rowBytes);
        }

        return input.WithData(cw, ch, srcSub, outBytes);
    }
}