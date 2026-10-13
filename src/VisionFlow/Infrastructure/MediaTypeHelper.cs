using System.Runtime.InteropServices;
using DirectShowLib;

namespace VisionFlow.Infrastructure;

/// <summary>
/// 媒体类型辅助方法。
/// </summary>
public static class MediaTypeHelper
{
    /// <summary>
    /// 生成媒体类型的可读描述，用于日志和调试。
    /// </summary>
    public static string Describe(AMMediaType? mediaType)
    {
        if (mediaType is null || mediaType.majorType == Guid.Empty)
            return "<null>";

        var major = MajorTypeName(mediaType.majorType);
        var sub = SubTypeName(mediaType.subType);

        var detail = string.Empty;
        if (mediaType.formatType == FormatType.VideoInfo && mediaType.formatPtr != IntPtr.Zero)
        {
            var videoInfo = new VideoInfoHeader();
            Marshal.PtrToStructure(mediaType.formatPtr, videoInfo);
            detail = $" {videoInfo.BmiHeader.Width}x{videoInfo.BmiHeader.Height}";
        }
        return $"{major}/{sub}{detail}";
    }

    /// <summary>
    /// 创建一个视频媒体类型，确保 AvgTimePerFrame 有效。
    /// 返回的 AMMediaType 由调用者通过 FreeMediaType 释放。
    /// </summary>
    public static AMMediaType CreateVideoInfo(Guid subType, int width, int height, long avgTimePerFrame)
    {
        var bmiHeader = new BitmapInfoHeader();
        bmiHeader.Size = Marshal.SizeOf<BitmapInfoHeader>();
        bmiHeader.Width = width;
        bmiHeader.Height = height;
        bmiHeader.Planes = 1;
        bmiHeader.BitCount = (short)GetBitCount(subType);
        bmiHeader.Compression = 0;
        bmiHeader.XPelsPerMeter = 0;
        bmiHeader.YPelsPerMeter = 0;
        bmiHeader.ClrUsed = 0;
        bmiHeader.ClrImportant = 0;

        var videoInfo = new VideoInfoHeader();
        videoInfo.AvgTimePerFrame = avgTimePerFrame;
        videoInfo.BmiHeader = bmiHeader;

        int size = Marshal.SizeOf<VideoInfoHeader>();
        IntPtr ptr = Marshal.AllocCoTaskMem(size);
        Marshal.StructureToPtr(videoInfo, ptr, false);

        return new AMMediaType
        {
            majorType = MediaType.Video,
            subType = subType,
            formatType = FormatType.VideoInfo,
            formatPtr = ptr,
            fixedSizeSamples = true,
            temporalCompression = false,
            sampleSize = width * height * GetBitCount(subType) / 8,
        };
    }

    /// <summary>
    /// 创建一个 RGB24 视频媒体类型，用于显式连接。
    /// </summary>
    public static AMMediaType CreateRgb24(int width, int height)
        => CreateVideoInfo(MediaSubType.RGB24, width, height, 333333);

    /// <summary>
    /// 释放 AMMediaType 占用的非托管内存。
    /// </summary>
    public static void FreeMediaType(AMMediaType mediaType)
    {
        if (mediaType is null) return;

        if (mediaType.formatPtr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(mediaType.formatPtr);
            mediaType.formatPtr = IntPtr.Zero;
        }
    }

    private static int GetBitCount(Guid subType)
    {
        if (subType == MediaSubType.RGB24) return 24;
        if (subType == MediaSubType.RGB32) return 32;
        if (subType == MediaSubType.YUY2) return 16;
        if (subType == MediaSubType.UYVY) return 16;
        return 24;
    }

    private static string MajorTypeName(Guid majorType)
    {
        if (majorType == MediaType.Video) return "Video";
        if (majorType == MediaType.Audio) return "Audio";
        if (majorType == MediaType.Stream) return "Stream";
        return majorType.ToString();
    }

    private static string SubTypeName(Guid subType)
    {
        if (subType == MediaSubType.RGB24) return "RGB24";
        if (subType == MediaSubType.RGB32) return "RGB32";
        if (subType == MediaSubType.YUY2) return "YUY2";
        if (subType == MediaSubType.MJPG) return "MJPG";
        if (subType == MediaSubType.NV12) return "NV12";
        if (subType == MediaSubType.UYVY) return "UYVY";
        if (subType == MediaSubType.I420) return "I420";
        return subType.ToString();
    }
}