using System.Runtime.InteropServices;
using DirectShowLib;

namespace VisionFlow.Infrastructure;

/// <summary>
/// 从 VIDEOINFOHEADER / VIDEOINFOHEADER2 中解析宽度、高度、帧率。
/// 不依赖 DirectShowLib 的托管结构定义，直接按内存偏移读取。
/// </summary>
public static class VideoInfoParser
{
    private const int VideoInfoHeaderBmiOffset = 48;
    private const int VideoInfoHeader2BmiOffset = 72;
    private const int AvgTimePerFrameOffset = 40;
    private const int BmiWidthOffset = 4;
    private const int BmiHeightOffset = 8;
    private const int BmiBitCountOffset = 14;

    private static readonly Guid FormatTypeVideoInfo2 =
        new Guid("F72A76A0-EB0A-11D0-ACE4-0000C0CC16BA");

    /// <summary>解析结果。</summary>
    public readonly struct VideoInfo
    {
        /// <summary>
        /// 构造解析结果。
        /// </summary>
        public VideoInfo(int width, int height, double frameRate, int bitCount)
        {
            Width = width;
            Height = height;
            FrameRate = frameRate;
            BitCount = bitCount;
        }

        /// <summary>宽度（像素）。</summary>
        public int Width { get; }
        /// <summary>高度（像素）。</summary>
        public int Height { get; }
        /// <summary>帧率（fps）。未知时为 0。</summary>
        public double FrameRate { get; }
        /// <summary>每像素位数。未知时为 0。</summary>
        public int BitCount { get; }
    }

    /// <summary>
    /// 尝试从 AMMediaType 中解析视频信息。失败时返回 false。
    /// </summary>
    public static bool TryParse(AMMediaType? mediaType, out VideoInfo info)
    {
        info = default;

        if (mediaType is null || mediaType.formatPtr == IntPtr.Zero)
        {
            return false;
        }

        int bmiOffset = (mediaType.formatType == FormatTypeVideoInfo2)
            ? VideoInfoHeader2BmiOffset
            : VideoInfoHeaderBmiOffset;

        IntPtr formatPtr = mediaType.formatPtr;
        IntPtr bmiPtr = IntPtr.Add(formatPtr, bmiOffset);

        int width = Marshal.ReadInt32(bmiPtr, BmiWidthOffset);
        int height = Marshal.ReadInt32(bmiPtr, BmiHeightOffset);
        short bitCount = Marshal.ReadInt16(bmiPtr, BmiBitCountOffset);

        long avgTimePerFrame = Marshal.ReadInt64(formatPtr, AvgTimePerFrameOffset);
        double fps = avgTimePerFrame > 0 ? 10_000_000.0 / avgTimePerFrame : 0;

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        info = new VideoInfo(width, height, fps, bitCount);
        return true;
    }
}