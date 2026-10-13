namespace VisionFlow.Infrastructure;

/// <summary>
/// 集中管理 DirectShow 常用系统对象的 CLSID。
/// </summary>
public static class DirectShowClsIds
{
    /// <summary>FilterGraph 的 CLSID。</summary>
    public static readonly Guid FilterGraph =
        new Guid("E436EBB3-524F-11CE-9F53-0020AF0BA770");

    /// <summary>CaptureGraphBuilder2 的 CLSID。</summary>
    public static readonly Guid CaptureGraphBuilder2 =
        new Guid("BF87B6E1-8C27-11D0-B3F0-00AA003761C5");

    /// <summary>SystemDeviceEnum 的 CLSID。</summary>
    public static readonly Guid SystemDeviceEnum =
        new Guid("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");

    /// <summary>VideoRendererDefault 的 CLSID。</summary>
    public static readonly Guid VideoRendererDefault =
        new Guid("6BC1CFFA-8FC1-4261-AC22-CFB4CC38DB50");

    /// <summary>EnhancedVideoRenderer (EVR) 的 CLSID。</summary>
    public static readonly Guid EnhancedVideoRenderer =
        new Guid("FA10746C-9B63-4B6C-BC49-FC300EA5F256");

    /// <summary>Video Mixing Renderer 9 (VMR-9) 的 CLSID。</summary>
    public static readonly Guid VideoMixingRenderer9 =
        new Guid("51B4ABF3-748F-4E3B-A276-C828330E926A");

    /// <summary>NullRenderer 的 CLSID。</summary>
    public static readonly Guid NullRenderer =
        new Guid("C1F400A4-3F08-11D3-9F0B-006008039E37");

    /// <summary>AVI Decompressor 的 CLSID。用于 MJPG 等压缩视频的解码。</summary>
    public static readonly Guid AviDecompressor =
        new Guid("CF49D4E0-1115-11CE-B03A-0020AF0BA770");
    /// <summary>Color Space Converter 的 CLSID。</summary>
    public static readonly Guid ColorSpaceConverter =
        new Guid("1643E180-90F5-11CE-97D5-00AA0055595A");

    /// <summary>SampleGrabber 的 CLSID。</summary>
    public static readonly Guid SampleGrabber =
        new Guid("C1F400A0-3F08-11D3-9F0B-006008039E37");

    /// <summary>SmartTee 的 CLSID。</summary>
    public static readonly Guid SmartTee =
        new Guid("CC7BFB41-F175-11D1-A392-00E0291F3959");
}