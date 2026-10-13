using DirectShowLib;

namespace VisionFlow.Devices;

/// <summary>
/// 描述一种视频格式：分辨率、帧率、像素格式。
/// 是 AMMediaType 的轻量值对象表示，不持有 COM 资源。
/// </summary>
public sealed class VideoFormat
{
    internal VideoFormat(int width, int height, double frameRate, Guid subType)
    {
        Width = width;
        Height = height;
        FrameRate = frameRate;
        SubType = subType;
    }

    /// <summary>画面宽度（像素）。</summary>
    public int Width { get; }

    /// <summary>画面高度（像素）。</summary>
    public int Height { get; }

    /// <summary>帧率（fps）。未知时为 0。</summary>
    public double FrameRate { get; }

    /// <summary>像素格式的 SubType GUID。</summary>
    public Guid SubType { get; }

    /// <summary>
    /// 像素格式的可读名称，如 "RGB24"、"YUY2"、"MJPG"。
    /// 未知格式返回 GUID 字符串。
    /// </summary>
    public string PixelFormatName
    {
        get
        {
            if (SubType == MediaSubType.RGB24) return "RGB24";
            if (SubType == MediaSubType.RGB32) return "RGB32";
            if (SubType == MediaSubType.YUY2) return "YUY2";
            if (SubType == MediaSubType.MJPG) return "MJPG";
            if (SubType == MediaSubType.NV12) return "NV12";
            if (SubType == MediaSubType.UYVY) return "UYVY";
            if (SubType == MediaSubType.I420) return "I420";
            return SubType.ToString();
        }
    }

    /// <inheritdoc />
    public override string ToString()
        => $"{Width}x{Height} {PixelFormatName} @ {FrameRate:F1}fps";
}