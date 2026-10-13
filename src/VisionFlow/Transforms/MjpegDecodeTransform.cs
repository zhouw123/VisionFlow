using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using OpenCvSharp;

namespace VisionFlow.Transforms;

/// <summary>
/// MJPG 解码转换。输入 MJPG 字节流，输出 BGR 图像（MediaSubType.RGB24）。
/// 
/// 不依赖 SubType 匹配——直接尝试 ImDecode：
///   成功 → 是压缩格式，输出 BGR。
///   失败 → 尝试扫描 JPEG SOI（FF D8）后重试。
///   还是失败 → 原样透传。
/// </summary>
public sealed class MjpegDecodeTransform : TransformComponent
{
    private long _frameCount;
    private long _decodeSuccess;
    private long _decodeFail;
    private int _diagnosticLogged;  // 只打印前几帧的诊断

    /// <summary>创建 MJPG 解码转换。</summary>
    public MjpegDecodeTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("MjpegDecode", queueCapacity, policy)
    {
    }

    /// <summary>已成功解码的帧数。</summary>
    public long DecodeSuccessCount => Interlocked.Read(ref _decodeSuccess);

    /// <summary>解码失败的帧数。</summary>
    public long DecodeFailCount => Interlocked.Read(ref _decodeFail);

    /// <summary>处理一帧。</summary>
    protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        Interlocked.Increment(ref _frameCount);

        // 已经是未压缩格式（BGR/GRAY）→ 透传
        int knownChannels = FrameFormats.GetChannelCount(input.SubType);
        if (knownChannels > 0)
        {
            return input;
        }

        // 诊断：打印前 5 帧的数据头
        long n = Interlocked.Read(ref _frameCount);
        if (Interlocked.CompareExchange(ref _diagnosticLogged, 1, 0) == 0 && n <= 5)
        {
            var head = new System.Text.StringBuilder();
            int len = Math.Min(16, input.Data.Length);
            for (int i = 0; i < len; i++) head.Append($"{input.Data[i]:X2} ");
            Console.WriteLine($"[MjpegDecode 诊断] 帧 {n}: " +
                $"len={input.Data.Length}, sub={input.SubType}, head={head}");
        }

        // 尝试解码
        var bgr = TryDecode(input.Data, out int offset);

        if (bgr is not null && !bgr.Empty())
        {
            Interlocked.Increment(ref _decodeSuccess);

            // 先读属性，再释放。Dispose 后访问 Width/Height 会崩溃。
            int bw = bgr.Width;
            int bh = bgr.Height;
            var outBytes = new byte[bgr.Total() * bgr.ElemSize()];
            Marshal.Copy(bgr.Data, outBytes, 0, outBytes.Length);
            bgr.Dispose();

            return input.WithData(bw, bh, MediaSubType.RGB24, outBytes);
        }

        Interlocked.Increment(ref _decodeFail);
        return input;
    }

    /// <summary>
    /// 尝试解码。先直接 ImDecode；失败则扫描 JPEG SOI 后重试。
    /// </summary>
    private static Mat? TryDecode(byte[] data, out int offset)
    {
        offset = 0;

        // 1. 直接尝试
        var mat = Cv2.ImDecode(data, ImreadModes.Color);
        if (mat is not null && !mat.Empty())
        {
            return mat;
        }
        mat?.Dispose();

        // 2. 扫描 JPEG SOI（FF D8 FF）
        int soiIndex = FindJpegStart(data);
        if (soiIndex >= 0 && soiIndex < data.Length - 2)
        {
            var slice = new byte[data.Length - soiIndex];
            Buffer.BlockCopy(data, soiIndex, slice, 0, slice.Length);

            mat = Cv2.ImDecode(slice, ImreadModes.Color);
            if (mat is not null && !mat.Empty())
            {
                offset = soiIndex;
                return mat;
            }
            mat?.Dispose();
        }

        return null;
    }

    /// <summary>
    /// 在字节数组中查找 JPEG SOI（FF D8 FF）位置。
    /// </summary>
    private static int FindJpegStart(byte[] data)
    {
        for (int i = 0; i < data.Length - 3; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xD8 && data[i + 2] == 0xFF)
            {
                return i;
            }
        }
        return -1;
    }
}