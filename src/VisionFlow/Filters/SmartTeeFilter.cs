using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Filters;

/// <summary>
/// Smart Tee 过滤器。把一个输入流复制为两路输出：
/// 一路为预览输出（允许丢帧以保证流畅），一路为采集输出（不丢帧）。
/// 
/// 典型用途：一路送渲染器做实时预览，一路送抓帧器做图像处理。
/// </summary>
public sealed class SmartTeeFilter : FilterBase
{
    /// <summary>
    /// 创建 Smart Tee 实例。
    /// </summary>
    public SmartTeeFilter() : base(CreateFilter())
    {
    }

    /// <summary>
    /// 捕获输出 Pin（不丢帧）。
    /// </summary>
    public Pin? CapturePin => FindPinByName("Capture");

    /// <summary>
    /// 预览输出 Pin（允许丢帧）。
    /// </summary>
    public Pin? PreviewPin => FindPinByName("Preview");

    /// <summary>
    /// 输入 Pin。
    /// </summary>
    public Pin? InputPin => GetFirstPin(PinDirection.Input);

    private Pin? FindPinByName(string name)
    {
        foreach (var pin in GetPins(PinDirection.Output))
        {
            if (pin.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return pin;
            }
        }
        return null;
    }

    private static IBaseFilter CreateFilter()
    {
        var type = Type.GetTypeFromCLSID(DirectShowClsIds.SmartTee)
            ?? throw new InvalidOperationException(
                "无法获取 Smart Tee 类型。");

        var obj = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                "无法创建 Smart Tee 实例。");

        return (IBaseFilter)obj;
    }
}