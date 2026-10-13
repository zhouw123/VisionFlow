using VisionFlow.Devices;

namespace VisionFlow.Tests.Devices;

public class DeviceEnumeratorTests
{
    [Fact]
    public void EnumerateVideoInputDevices_ReturnsNonNullList()
    {
        var devices = DeviceEnumerator.EnumerateVideoInputDevices();

        Assert.NotNull(devices);
        // 释放所有设备
        foreach (var d in devices) d.Dispose();
    }

    [Fact]
    public void EnumerateAudioInputDevices_ReturnsNonNullList()
    {
        var devices = DeviceEnumerator.EnumerateAudioInputDevices();

        Assert.NotNull(devices);
        foreach (var d in devices) d.Dispose();
    }

    [Fact]
    public void EnumerateVideoInputDevices_ReturnsDevicesWithName()
    {
        var devices = DeviceEnumerator.EnumerateVideoInputDevices();
        try
        {
            foreach (var d in devices)
            {
                Assert.False(string.IsNullOrWhiteSpace(d.Name));
            }
        }
        finally
        {
            foreach (var d in devices) d.Dispose();
        }
    }

    [Fact]
    public void DeviceInfo_CreateFilter_ReturnsFilter()
    {
        var devices = DeviceEnumerator.EnumerateVideoInputDevices();
        try
        {
            if (devices.Count == 0)
            {
                // 无相机环境，跳过
                return;
            }

            var filter = devices[0].CreateFilter();
            Assert.NotNull(filter);

            VisionFlow.Infrastructure.ComHelper.SafeRelease(filter);
        }
        finally
        {
            foreach (var d in devices) d.Dispose();
        }
    }
}