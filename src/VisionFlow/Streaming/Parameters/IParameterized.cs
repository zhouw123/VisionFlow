namespace VisionFlow.Streaming.Parameters;

/// <summary>
/// 参数化接口。所有支持运行时参数调整的组件都实现此接口。
/// UI 层通过此接口发现、读取和修改参数，无需知道组件的具体类型。
/// </summary>
public interface IParameterized
{
    /// <summary>当前组件暴露的所有参数描述。</summary>
    IReadOnlyList<ParameterDescriptor> Parameters { get; }

    /// <summary>
    /// 读取一个参数的当前值。参数不存在时返回 null。
    /// </summary>
    object? GetParameter(string key);

    /// <summary>
    /// 修改一个参数。会触发 ParameterChanged 事件。
    /// 参数不存在或类型不匹配时抛异常。
    /// </summary>
    void SetParameter(string key, object? value);

    /// <summary>参数值变化时触发。可能在任意线程上触发。</summary>
    event EventHandler<ParameterChangedEventArgs>? ParameterChanged;
}