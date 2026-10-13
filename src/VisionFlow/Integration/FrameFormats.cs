namespace VisionFlow.Integration;

/// <summary>
/// 框架层自定义的帧格式 GUID。
/// MediaSubType.RGB24 表示 BGR 3 通道（DirectShow 命名历史原因）。
/// 其他格式用本类的常量表示。
/// </summary>
public static class FrameFormats
{
    /// <summary>单通道灰度，8 位/像素。</summary>
    public static readonly Guid Gray8 =
        new Guid("8A4F7B12-C1D2-4E3F-A5B6-7C8D9E0F1A2B");

    /// <summary>3 通道 HSV，8 位/通道。</summary>
    public static readonly Guid Hsv8 =
        new Guid("1B2C3D4E-5F6A-7B8C-9D0E-1F2A3B4C5D6E");

    /// <summary>3 通道 YCrCb，8 位/通道。</summary>
    public static readonly Guid YCrCb8 =
        new Guid("2C3D4E5F-6A7B-8C9D-0E1F-2A3B4C5D6E7F");

    /// <summary>3 通道 YUV，8 位/通道。</summary>
    public static readonly Guid Yuv8 =
        new Guid("3D4E5F6A-7B8C-9D0E-1F2A-3B4C5D6E7F8A");

    /// <summary>3 通道 RGB，8 位/通道。字节顺序 R-G-B。</summary>
    public static readonly Guid Rgb8 =
        new Guid("4E5F6A7B-8C9D-0E1F-2A3B-4C5D6E7F8A9B");

    /// <summary>
    /// 从 SubType 判断每像素字节数。未知格式返回 0。
    /// </summary>
    public static int GetBytesPerPixel(Guid subType)
    {
        if (subType == Gray8) return 1;
        if (subType == DirectShowLib.MediaSubType.RGB24) return 3;  // BGR24
        if (subType == Rgb8) return 3;
        if (subType == Hsv8) return 3;
        if (subType == YCrCb8) return 3;
        if (subType == Yuv8) return 3;
        return 0;
    }

    /// <summary>是否单通道格式。</summary>
    public static bool IsGray(Guid subType) => subType == Gray8;

    /// <summary>是否 3 通道格式（BGR/RGB/HSV/YCrCb/YUV）。</summary>
    public static bool Is3Channel(Guid subType)
        => subType == DirectShowLib.MediaSubType.RGB24
        || subType == Rgb8
        || subType == Hsv8
        || subType == YCrCb8
        || subType == Yuv8;

    /// <summary>
    /// 返回通道数（1 或 3）。未知格式返回 0。
    /// </summary>
    public static int GetChannelCount(Guid subType)
    {
        if (subType == Gray8) return 1;
        if (Is3Channel(subType)) return 3;
        return 0;
    }
}