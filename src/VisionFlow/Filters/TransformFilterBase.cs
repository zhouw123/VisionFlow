using DirectShowLib;

namespace VisionFlow.Filters;

/// <summary>
/// 转换过滤器基类。通常有一个输入 Pin 和一个输出 Pin。
/// </summary>
public abstract class TransformFilterBase : FilterBase
{
    /// <summary>
    /// 用已有的 IBaseFilter 初始化转换过滤器。
    /// </summary>
    protected TransformFilterBase(IBaseFilter filter) : base(filter)
    {
    }

    /// <summary>输入 Pin。</summary>
    public Pin? InputPin => GetFirstPin(PinDirection.Input);

    /// <summary>输出 Pin。</summary>
    public Pin? OutputPin => GetFirstPin(PinDirection.Output);
}