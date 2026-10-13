using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Filters;

/// <summary>
/// 对 IPin 的轻量封装。
/// Pin 的生命周期归其所属的 FilterBase 所有，本类不负责释放 IPin。
/// </summary>
public sealed class Pin
{
    private readonly IPin _pin;
    private readonly string _name;

    internal Pin(IPin pin)
    {
        _pin = pin ?? throw new ArgumentNullException(nameof(pin));
        _name = PinHelper.GetName(pin);
    }

    /// <summary>
    /// 底层的 IPin 对象。供封装层内部使用。
    /// </summary>
    internal IPin NativePin => _pin;

    /// <summary>
    /// Pin 名称。可能是空字符串。
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Pin 方向。
    /// </summary>
    public PinDirection Direction
    {
        get
        {
            var hr = _pin.QueryDirection(out var dir);
            HResultHelper.ThrowIfFailed(hr, "查询 Pin 方向");
            return dir;
        }
    }

    /// <summary>
    /// Pin 是否已连接到对方 Pin。
    /// </summary>
    public bool IsConnected => PinHelper.IsConnected(_pin);

    /// <summary>
    /// 获取此 Pin 支持的媒体类型列表。
    /// </summary>
    public IReadOnlyList<AMMediaType> GetMediaTypes()
    {
        var result = new List<AMMediaType>();

        var hr = _pin.EnumMediaTypes(out var enumMediaTypes);
        if (HResultHelper.IsFailure(hr) || enumMediaTypes is null)
        {
            return result;
        }

        try
        {
            var types = new AMMediaType[1];
            while (enumMediaTypes.Next(1, types, IntPtr.Zero) == 0)
            {
                var type = types[0];
                if (type is not null)
                {
                    result.Add(type);
                }
            }
        }
        finally
        {
            ComHelper.SafeRelease(enumMediaTypes);
        }

        return result;
    }

    /// <summary>
    /// 连接此 Pin 到目标 Pin。
    /// 只做直接连接，不做智能插入中间过滤器。
    /// 传入 null 媒体类型，由 DirectShow 协商决定。
    /// </summary>
    public void ConnectTo(Pin target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var hr = _pin.Connect(target._pin, null);
        HResultHelper.ThrowIfFailed(
            hr,
            "连接 Pin {0} → {1}",
            string.IsNullOrEmpty(Name) ? "<unnamed>" : Name,
            string.IsNullOrEmpty(target.Name) ? "<unnamed>" : target.Name);
    }

    /// <summary>
    /// 断开此 Pin 的连接。
    /// </summary>
    public void Disconnect()
    {
        var hr = _pin.Disconnect();
        HResultHelper.ThrowIfFailed(hr, "断开 Pin 连接");
    }

    /// <inheritdoc />
    public override string ToString()

    {
        return $"{Direction}:{Name}";
    }
}