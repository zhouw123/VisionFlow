using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Filters;

/// <summary>
/// Color Space Converter 过滤器。
/// 用于在不同色彩空间之间转换（如 YUY2 → RGB24）。
/// 是 Windows 系统自带的过滤器，无需额外安装。
/// </summary>
public sealed class ColorSpaceConverterFilter : TransformFilterBase
{
    /// <summary>
    /// 创建 Color Space Converter 实例。
    /// </summary>
    public ColorSpaceConverterFilter() : base(CreateFilter())
    {
    }

    private static IBaseFilter CreateFilter()
    {
        var type = Type.GetTypeFromCLSID(DirectShowClsIds.ColorSpaceConverter)
            ?? throw new InvalidOperationException(
                "无法获取 Color Space Converter 类型。");

        var obj = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                "无法创建 Color Space Converter 实例。");

        return (IBaseFilter)obj;
    }
}