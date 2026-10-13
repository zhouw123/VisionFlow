using System.Runtime.InteropServices.ComTypes;
using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Devices;

/// <summary>
/// 描述一个 DirectShow 设备（相机、麦克风等）。
/// 内部持有 DsDevice，Moniker 生命周期由 DsDevice 管理。
/// </summary>
public sealed class DeviceInfo : IDisposable
{
    private readonly DsDevice _dsDevice;
    private bool _disposed;

    internal DeviceInfo(DsDevice dsDevice)
    {
        _dsDevice = dsDevice ?? throw new ArgumentNullException(nameof(dsDevice));
        Name = dsDevice.Name ?? string.Empty;
        DevicePath = dsDevice.DevicePath ?? string.Empty;
    }

    /// <summary>设备友好名称。</summary>
    public string Name { get; }

    /// <summary>设备路径。可用于区分同名设备。</summary>
    public string DevicePath { get; }

    /// <summary>
    /// 从该设备创建过滤器。返回的 IBaseFilter 由调用者负责释放。
    /// </summary>
    public IBaseFilter CreateFilter()
    {
        ThrowIfDisposed();

        var moniker = _dsDevice.Mon;
        if (moniker is null)
        {
            throw new InvalidOperationException(
                $"设备 \"{Name}\" 没有有效的 Moniker。");
        }

        var iid = typeof(IBaseFilter).GUID;
        moniker.BindToObject(null!, null!, ref iid, out var obj);

        if (obj is not IBaseFilter filter)
        {
            throw new InvalidOperationException(
                $"无法从设备 \"{Name}\" 创建 IBaseFilter。");
        }

        return filter;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DeviceInfo));
        }
    }

    /// <summary>
    /// 释放底层设备对象。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _dsDevice.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}