namespace VisionFlow.Streaming.Parameters;

/// <summary>
/// 参数描述符。UI 层通过它了解一个参数的全部元数据，
/// 无需知道具体组件的类型。
/// </summary>
public sealed class ParameterDescriptor
{
    /// <summary>
    /// 构造参数描述符。通过注册时的参数构造，一般不直接 new。
    /// </summary>
    internal ParameterDescriptor(
        string key,
        string displayName,
        ParameterKind kind,
        string? description = null,
        double? min = null,
        double? max = null,
        double? step = null,
        string? unit = null,
        IReadOnlyList<EnumOption>? enumOptions = null)
    {
        Key = key;
        DisplayName = displayName;
        Kind = kind;
        Description = description;
        Min = min;
        Max = max;
        Step = step;
        Unit = unit;
        EnumOptions = enumOptions;
    }

    /// <summary>唯一标识。GetParameter/SetParameter 时使用。</summary>
    public string Key { get; }

    /// <summary>显示名。UI 中作为标签。</summary>
    public string DisplayName { get; }

    /// <summary>参数类型。</summary>
    public ParameterKind Kind { get; }

    /// <summary>描述。可选，UI 中作为工具提示。</summary>
    public string? Description { get; }

    /// <summary>最小值。Int/Double/Color 有效。</summary>
    public double? Min { get; }

    /// <summary>最大值。Int/Double/Color 有效。</summary>
    public double? Max { get; }

    /// <summary>步进。Int/Double 有效，用于滑块和数字输入框。</summary>
    public double? Step { get; }

    /// <summary>单位。如 "ms"、"px"、"级"。可选。</summary>
    public string? Unit { get; }

    /// <summary>枚举选项。仅 Enum 类型有效。</summary>
    public IReadOnlyList<EnumOption>? EnumOptions { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Key}: {DisplayName} ({Kind})";
}