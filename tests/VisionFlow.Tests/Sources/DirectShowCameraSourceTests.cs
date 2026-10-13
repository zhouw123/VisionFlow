using VisionFlow.Devices;
using VisionFlow.Sources;
using VisionFlow.Streaming;

namespace VisionFlow.Tests.Sources;

public class DirectShowCameraSourceTests
{
    private static DeviceInfo? GetFirstCamera()
    {
        var devices = DeviceEnumerator.EnumerateVideoInputDevices();
        if (devices.Count == 0) return null;
        // 保留第一个，释放其余的
        var first = devices[0];
        for (int i = 1; i < devices.Count; i++) devices[i].Dispose();
        return first;
    }

    [Fact]
    public void Constructor_NullDevice_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DirectShowCameraSource(null!));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var device = GetFirstCamera();
        if (device is null) return;

        try
        {
            using var source = new DirectShowCameraSource(device);
            Assert.Contains(device.Name, source.Name);
        }
        finally { device.Dispose(); }
    }

    [Fact]
    public void TrySetFormat_BeforeStart_DoesNotThrow()
    {
        var device = GetFirstCamera();
        if (device is null) return;

        try
        {
            using var source = new DirectShowCameraSource(device);
            var ex = Record.Exception(() =>
                source.TrySetFormat(DirectShowLib.MediaSubType.MJPG, 640, 480));
            Assert.Null(ex);
        }
        finally { device.Dispose(); }
    }

    [Fact]
    public void InitialState_IsCreated()
    {
        var device = GetFirstCamera();
        if (device is null) return;

        try
        {
            using var source = new DirectShowCameraSource(device);
            Assert.Equal(ComponentState.Created, source.State);
        }
        finally { device.Dispose(); }
    }
}