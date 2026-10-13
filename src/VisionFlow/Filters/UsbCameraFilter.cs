using DirectShowLib;
using VisionFlow.Devices;
using VisionFlow.Infrastructure;

namespace VisionFlow.Filters;

/// <summary>
/// USB 相机捕获过滤器。
/// 标准 UVC 相机可直接使用此类；特定品牌/型号的特殊初始化可在派生类中实现。
/// </summary>
public class UsbCameraFilter : CaptureFilterBase
{
    /// <summary>
    /// 用已有的 IBaseFilter 创建实例。供派生类使用。
    /// </summary>
    protected UsbCameraFilter(IBaseFilter filter) : base(filter)
    {
    }

    /// <summary>
    /// 从设备信息创建 USB 相机过滤器。
    /// </summary>
    public static UsbCameraFilter Create(DeviceInfo deviceInfo)
    {
        ArgumentNullException.ThrowIfNull(deviceInfo);

        var filter = deviceInfo.CreateFilter();
        HResultHelper.ThrowIfFailed(0, ""); // 占位，防止空 if
        return new UsbCameraFilter(filter);
    }
}