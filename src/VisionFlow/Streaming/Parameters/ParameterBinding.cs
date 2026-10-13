namespace VisionFlow.Streaming.Parameters;

/// <summary>
/// 参数与组件字段之间的绑定。由 FrameComponent 内部维护。
/// 对每个注册的参数：一个描述符 + getter + setter。
/// </summary>
internal sealed class ParameterBinding
{
    public ParameterBinding(
        ParameterDescriptor descriptor,
        Func<object?> getter,
        Action<object?> setter)
    {
        Descriptor = descriptor;
        Getter = getter;
        Setter = setter;
    }

    public ParameterDescriptor Descriptor { get; }

    public Func<object?> Getter { get; }

    public Action<object?> Setter { get; }
}