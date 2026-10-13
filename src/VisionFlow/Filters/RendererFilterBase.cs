using DirectShowLib;

namespace VisionFlow.Filters;

/// <summary>
/// 渲染过滤器基类。
/// </summary>
public abstract class RendererFilterBase : FilterBase
{
    /// <summary>
    /// 用已有的 IBaseFilter 初始化渲染过滤器。
    /// </summary>
    protected RendererFilterBase(IBaseFilter filter) : base(filter)
    {
    }

    /// <summary>
    /// 渲染器的输入 Pin。
    /// </summary>
    public Pin? InputPin => GetFirstPin(PinDirection.Input);
}