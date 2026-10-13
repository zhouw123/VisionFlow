namespace VisionFlow.Streaming.Parameters;

/// <summary>参数变化事件参数。</summary>
public sealed class ParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// 构造事件参数。
    /// </summary>
    /// <param name="key">参数键。</param>
    /// <param name="oldValue">旧值。</param>
    /// <param name="newValue">新值。</param>
    public ParameterChangedEventArgs(string key, object? oldValue, object? newValue)
    {
        Key = key;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>参数键。</summary>
    public string Key { get; }

    /// <summary>旧值。</summary>
    public object? OldValue { get; }

    /// <summary>新值。</summary>
    public object? NewValue { get; }
}