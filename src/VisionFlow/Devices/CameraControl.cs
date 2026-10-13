using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Devices;

/// <summary>
/// 封装 IAMCameraControl，用于控制曝光、对焦、变焦、光圈等。
/// </summary>
public sealed class CameraControl
{
    private readonly IAMCameraControl _control;

    private CameraControl(IAMCameraControl control)
    {
        _control = control;
    }

    /// <summary>
    /// 尝试从过滤器获取 CameraControl。不支持时返回 null。
    /// </summary>
    internal static CameraControl? TryCreate(IBaseFilter filter)
    {
        try
        {
            if (filter is IAMCameraControl control)
            {
                return new CameraControl(control);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 查询指定属性的取值范围。不支持时返回 null。
    /// </summary>
    public PropertyRange? GetRange(CameraControlProperty property)
    {
        var hr = _control.GetRange(property,
            out var min, out var max, out var step, out var def, out var caps);

        if (HResultHelper.IsFailure(hr))
        {
            return null;
        }

        return new PropertyRange(min, max, step, def, Convert.ToInt32(caps));
    }

    /// <summary>
    /// 读取指定属性的当前值与模式。不支持时返回 null。
    /// </summary>
    public (int Value, CameraControlFlags Flags)? Get(CameraControlProperty property)
    {
        var hr = _control.Get(property, out var value, out var flags);
        if (HResultHelper.IsFailure(hr))
        {
            return null;
        }
        return (value, flags);
    }

    /// <summary>
    /// 设置指定属性的值与模式。
    /// </summary>
    public void Set(CameraControlProperty property, int value, CameraControlFlags flags)
    {
        var hr = _control.Set(property, value, flags);
        HResultHelper.ThrowIfFailed(hr,
            "设置相机属性 {0} = {1} ({2})", property, value, flags);
    }
}