using DirectShowLib;
using VisionFlow.Devices;
using VisionFlow.Filters;

namespace VisionFlow.Tests.Filters;

public class CaptureFilterTests
{
    [Fact]
    public void UsbCameraFilter_Create_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UsbCameraFilter.Create(null!));
    }

    [Fact]
    public void UsbCameraFilter_HasOutputPin()
    {
        var devices = DeviceEnumerator.EnumerateVideoInputDevices();
        try
        {
            if (devices.Count == 0)
            {
                return; // 无相机环境跳过
            }

            using var camera = UsbCameraFilter.Create(devices[0]);

            Assert.NotNull(camera.OutputPin);
            Assert.Equal(PinDirection.Output, camera.OutputPin!.Direction);
        }
        finally
        {
            foreach (var d in devices) d.Dispose();
        }
    }
}