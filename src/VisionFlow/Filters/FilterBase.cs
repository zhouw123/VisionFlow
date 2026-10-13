using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Filters;

/// <summary>
/// 所有过滤器的基类，持有 IBaseFilter 并管理其生命周期。
/// </summary>
public class FilterBase : IDisposable
{
    private readonly IBaseFilter _filter;
    private List<Pin>? _cachedPins;
    private bool _disposed;

    /// <summary>
    /// 从已有的 IBaseFilter 创建 FilterBase。
    /// FilterBase 将接管该 IBaseFilter 的引用释放责任。
    /// </summary>
    public FilterBase(IBaseFilter filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    /// <summary>
    /// 底层 IBaseFilter 对象。供封装层内部使用。
    /// </summary>
    /// <summary>
    /// 底层 IBaseFilter 对象。用于访问封装层未覆盖的 COM 接口。
    /// 使用方需自行管理其生命周期，不应主动释放此对象。
    /// </summary>
    public IBaseFilter NativeFilter => _filter;

    /// <summary>
    /// 过滤器名称。失败时返回空字符串。
    /// </summary>
    public string Name
    {
        get
        {
            try
            {
                var hr = _filter.QueryFilterInfo(out var info);
                if (HResultHelper.IsSuccess(hr) && info.achName is not null)
                {
                    return info.achName;
                }
            }
            catch
            {
                // 忽略
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取指定方向的全部 Pin。结果会被缓存。
    /// </summary>
    public IReadOnlyList<Pin> GetPins(PinDirection direction)
    {
        EnsurePinsLoaded();
        return _cachedPins!.Where(p => p.Direction == direction).ToList();
    }

    /// <summary>
    /// 获取指定方向的第一个 Pin。找不到时返回 null。
    /// </summary>
    public Pin? GetFirstPin(PinDirection direction)
    {
        EnsurePinsLoaded();
        return _cachedPins!.FirstOrDefault(p => p.Direction == direction);
    }

    private void EnsurePinsLoaded()
    {
        if (_cachedPins is not null)
        {
            return;
        }

        var allPins = new List<Pin>();

        var hr = _filter.EnumPins(out var enumPins);
        HResultHelper.ThrowIfFailed(hr, "枚举过滤器 Pin");

        if (enumPins is null)
        {
            _cachedPins = allPins;
            return;
        }

        try
        {
            var pins = new IPin[1];
            while (enumPins.Next(1, pins, IntPtr.Zero) == 0)
            {
                var nativePin = pins[0];
                if (nativePin is not null)
                {
                    allPins.Add(new Pin(nativePin));
                }
            }
        }
        finally
        {
            ComHelper.SafeRelease(enumPins);
        }

        _cachedPins = allPins;
    }

    /// <summary>
    /// 释放持有的 IBaseFilter 与所有缓存的 Pin。
    /// </summary>
    public void Dispose()

    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的可重写实现。
    /// </summary>
    protected virtual void Dispose(bool disposing)

    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            if (_cachedPins is not null)
            {
                foreach (var pin in _cachedPins)
                {
                    ComHelper.SafeRelease(pin.NativePin);
                }
                _cachedPins.Clear();
                _cachedPins = null;
            }

            ComHelper.SafeRelease(_filter);
        }

        _disposed = true;
    }
}