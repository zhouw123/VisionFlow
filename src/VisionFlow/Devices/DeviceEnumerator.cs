using DirectShowLib;

namespace VisionFlow.Devices;

/// <summary>
/// 枚举 DirectShow 系统设备。
/// 内部使用 DsDevice.GetDevicesOfCat，将设备生命周期封装为 DeviceInfo。
/// </summary>
public static class DeviceEnumerator
{
    /// <summary>
    /// 枚举所有视频输入设备（USB 相机、采集卡等）。
    /// </summary>
    public static IReadOnlyList<DeviceInfo> EnumerateVideoInputDevices()
        => EnumerateByCategory(FilterCategory.VideoInputDevice);

    /// <summary>
    /// 枚举所有音频输入设备（麦克风、线路输入等）。
    /// </summary>
    public static IReadOnlyList<DeviceInfo> EnumerateAudioInputDevices()
        => EnumerateByCategory(FilterCategory.AudioInputDevice);

    private static IReadOnlyList<DeviceInfo> EnumerateByCategory(Guid category)
    {
        var result = new List<DeviceInfo>();

        DsDevice[] devices;
        try
        {
            devices = DsDevice.GetDevicesOfCat(category);
        }
        catch
        {
            return result;
        }

        if (devices is null || devices.Length == 0)
        {
            return result;
        }

        foreach (var dsDevice in devices)
        {
            try
            {
                result.Add(new DeviceInfo(dsDevice));
            }
            catch
            {
                // 构造失败时释放该设备的资源
                try { dsDevice.Dispose(); } catch { /* 忽略 */ }
            }
        }

        return result;
    }
}