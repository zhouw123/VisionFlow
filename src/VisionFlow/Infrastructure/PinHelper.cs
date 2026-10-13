using DirectShowLib;

namespace VisionFlow.Infrastructure;

/// <summary>
/// Pin 操作的辅助方法。
/// </summary>
public static class PinHelper
{
    /// <summary>
    /// 枚举指定过滤器上所有指定方向的 Pin。
    /// 返回的 IPin 引用由调用者负责释放。
    /// </summary>
    public static List<IPin> EnumPins(IBaseFilter filter, PinDirection direction)
    {
        var result = new List<IPin>();

        var hr = filter.EnumPins(out var enumPins);
        HResultHelper.ThrowIfFailed(hr, "枚举过滤器 Pin");

        if (enumPins is null)
        {
            return result;
        }

        try
        {
            var pins = new IPin[1];
            while (enumPins.Next(1, pins, IntPtr.Zero) == 0)
            {
                var pin = pins[0];
                if (pin is null)
                {
                    continue;
                }

                var dirHr = pin.QueryDirection(out var pinDir);
                if (HResultHelper.IsSuccess(dirHr) && pinDir == direction)
                {
                    result.Add(pin);
                }
                else
                {
                    ComHelper.SafeRelease(pin);
                }
            }
        }
        finally
        {
            ComHelper.SafeRelease(enumPins);
        }

        return result;
    }

    /// <summary>
    /// 查询 Pin 是否已连接。
    /// </summary>
    public static bool IsConnected(IPin pin)
    {
        var hr = pin.ConnectedTo(out var connectedPin);
        if (hr == 0 && connectedPin is not null)
        {
            ComHelper.SafeRelease(connectedPin);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 查询 Pin 的友好名称。失败时返回空字符串。
    /// </summary>
    public static string GetName(IPin pin)
    {
        try
        {
            var hr = pin.QueryPinInfo(out var info);
            if (HResultHelper.IsSuccess(hr) && info.name is not null)
            {
                return info.name;
            }
        }
        catch
        {
            // 忽略
        }
        return string.Empty;
    }
}