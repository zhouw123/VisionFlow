using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Filters;

/// <summary>
/// 视频渲染器，基于 Video Mixing Renderer 9 (VMR-9) 的 Windowed 模式。
/// 
/// Windowed 模式下 VMR-9 创建自己的子窗口，通过 IVideoWindow 接口控制。
/// 这是传统 WinForms 应用的默认选择。
/// 
/// 注意：Windowless 模式（视频直接画到父窗口）在当前系统上对 YUY2 输入
/// 不支持——系统缺少 YUY2→RGB 转换器。详见 docs/notes/vmr9-windowless.md。
/// </summary>
public sealed class VideoRenderer : RendererFilterBase
{
    /// <summary>
    /// 创建基于 VMR-9 Windowed 模式的视频渲染器。
    /// </summary>
    public VideoRenderer() : base(CreateFilter())
    {
    }

    private static IBaseFilter CreateFilter()
    {
        var type = Type.GetTypeFromCLSID(DirectShowClsIds.VideoMixingRenderer9)
            ?? throw new InvalidOperationException(
                "无法获取 VMR-9 类型。系统可能不支持。");

        var obj = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                "无法创建 VMR-9 实例。");

        var filter = (IBaseFilter)obj;

        // 配置为 Windowed 模式
        if (filter is IVMRFilterConfig9 config)
        {
            try
            {
                config.SetRenderingMode(VMR9Mode.Windowed);
                config.SetNumberOfStreams(1);
            }
            catch
            {
                // 配置失败时不阻止创建，某些系统可能不支持
            }
        }

        return filter;
    }
}