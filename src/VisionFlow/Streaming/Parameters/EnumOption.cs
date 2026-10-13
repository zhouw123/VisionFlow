namespace VisionFlow.Streaming.Parameters;

/// <summary>枚举型参数的一个选项。</summary>
public sealed class EnumOption
{
    /// <summary>
    /// 创建一个枚举选项。
    /// </summary>
    /// <param name="value">选项的值。UI 层在 SetParameter 时传入。</param>
    /// <param name="display">显示名。</param>
    public EnumOption(object value, string display)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Display = display ?? throw new ArgumentNullException(nameof(display));
    }

    /// <summary>选项的值。通常是枚举成员、字符串或整数。</summary>
    public object Value { get; }

    /// <summary>选项的显示文本。</summary>
    public string Display { get; }

    /// <inheritdoc />
    public override string ToString() => Display;
}