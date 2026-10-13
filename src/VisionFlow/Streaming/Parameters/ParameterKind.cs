namespace VisionFlow.Streaming.Parameters;

/// <summary>参数的数据类型。UI 层根据此选择控件类型。</summary>
public enum ParameterKind
{
    /// <summary>整数。UI 用滑块或数字输入框。</summary>
    Int,

    /// <summary>浮点数。UI 用滑块或数字输入框。</summary>
    Double,

    /// <summary>布尔值。UI 用复选框。</summary>
    Bool,

    /// <summary>枚举。UI 用下拉框。需提供 EnumOptions。</summary>
    Enum,

    /// <summary>字符串。UI 用文本输入框。</summary>
    String,

    /// <summary>颜色（0xAARRGGBB）。UI 用颜色选择器。</summary>
    Color,
}