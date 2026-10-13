using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Filters;

/// <summary>
/// Null Renderer。不显示也不播放，仅消费数据，用于后台处理场景。
/// </summary>
public sealed class NullRendererFilter : RendererFilterBase
{
    /// <summary>
    /// 创建 Null Renderer 实例。
    /// </summary>
    public NullRendererFilter() : base(CreateFilter())
    {
    }

    private static IBaseFilter CreateFilter()
    {
        var type = Type.GetTypeFromCLSID(DirectShowClsIds.NullRenderer)
            ?? throw new InvalidOperationException(
                "无法获取 Null Renderer 类型。");

        var obj = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                "无法创建 Null Renderer 实例。");

        return (IBaseFilter)obj;
    }
}