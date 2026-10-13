using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Devices;

/// <summary>
/// 封装 IAMVideoProcAmp，用于控制亮度、对比度、饱和度、白平衡等。
/// </summary>
public sealed class VideoProcAmp
{
    private readonly IAMVideoProcAmp _amp;

    private VideoProcAmp(IAMVideoProcAmp amp)
    {
        _amp = amp;
    }

    /// <summary>
    /// 尝试从过滤器获取 VideoProcAmp。不支持时返回 null。
    /// </summary>
    internal static VideoProcAmp? TryCreate(IBaseFilter filter)
    {
        try
        {
            if (filter is IAMVideoProcAmp amp)
            {
                return new VideoProcAmp(amp);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 查询指定属性的取值范围。不支持时返回 null。
    /// </summary>
    public PropertyRange? GetRange(VideoProcAmpProperty property)
    {
        var hr = _amp.GetRange(property,
            out var min, out var max, out var step, out var def, out var caps);

        if (HResultHelper.IsFailure(hr))
        {
            return null;
        }

        return new PropertyRange(min, max, step, def, Convert.ToInt32(caps));
    }

    /// <summary>
    /// 读取指定属性的当前值与模式。不支持时返回 null。
    /// </summary>
    public (int Value, VideoProcAmpFlags Flags)? Get(VideoProcAmpProperty property)
    {
        var hr = _amp.Get(property, out var value, out var flags);
        if (HResultHelper.IsFailure(hr))
        {
            return null;
        }
        return (value, flags);
    }

    /// <summary>
    /// 设置指定属性的值与模式。
    /// </summary>
    public void Set(VideoProcAmpProperty property, int value, VideoProcAmpFlags flags)
    {
        var hr = _amp.Set(property, value, flags);
        HResultHelper.ThrowIfFailed(hr,
            "设置视频属性 {0} = {1} ({2})", property, value, flags);
    }
}