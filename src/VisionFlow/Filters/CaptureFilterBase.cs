using DirectShowLib;
using VisionFlow.Devices;

namespace VisionFlow.Filters;

/// <summary>
/// 捕获过滤器基类。
/// 派生类实现不同相机类型的初始化逻辑（USB、工业相机、IP 相机等）。
/// </summary>
public abstract class CaptureFilterBase : FilterBase
{
    private StreamConfig? _streamConfig;
    private CameraControl? _cameraControl;
    private VideoProcAmp? _videoProcAmp;

    /// <summary>
    /// 用已有的 IBaseFilter 初始化捕获过滤器。
    /// </summary>
    protected CaptureFilterBase(IBaseFilter filter) : base(filter)
    {
    }

    /// <summary>
    /// 捕获过滤器的预览/采集输出 Pin。
    /// 大多数 UVC 相机只有一个输出 Pin。
    /// </summary>
    public Pin? OutputPin => GetFirstPin(PinDirection.Output);

    /// <summary>
    /// 输出 Pin 的流配置接口。用于枚举和设置分辨率、帧率、像素格式。
    /// 相机不支持时为 null。
    /// </summary>
    public StreamConfig? StreamConfig
    {
        get
        {
            if (_streamConfig is null && OutputPin is not null)
            {
                _streamConfig = StreamConfig.TryCreate(OutputPin.NativePin);
            }
            return _streamConfig;
        }
    }

    /// <summary>
    /// 相机控制接口（曝光、对焦、变焦、光圈）。不支持时为 null。
    /// </summary>
    public CameraControl? CameraControl
        => _cameraControl ??= CameraControl.TryCreate(NativeFilter);

    /// <summary>
    /// 视频属性接口（亮度、对比度、饱和度、白平衡）。不支持时为 null。
    /// </summary>
    public VideoProcAmp? VideoProcAmp
        => _videoProcAmp ??= VideoProcAmp.TryCreate(NativeFilter);

    /// <summary>
    /// 枚举输出 Pin 支持的所有格式。
    /// </summary>
    public IReadOnlyList<VideoFormat> GetSupportedFormats()
        => StreamConfig?.GetSupportedFormats() ?? Array.Empty<VideoFormat>();

    /// <summary>
    /// 从支持格式中选择一个满足条件的并应用。返回是否设置成功。
    /// </summary>
    public bool TrySetFormat(Func<VideoFormat, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (StreamConfig is null)
        {
            return false;
        }

        var formats = StreamConfig.GetSupportedFormats();
        var target = formats.FirstOrDefault(predicate);
        if (target is null)
        {
            return false;
        }

        try
        {
            StreamConfig.SetFormat(target);
            return true;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }
}