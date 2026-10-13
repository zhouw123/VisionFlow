namespace VisionFlow.Devices;

/// <summary>
/// 相机参数（CameraControl / VideoProcAmp）的取值范围与能力信息。
/// </summary>
public readonly struct PropertyRange
{
    internal PropertyRange(int min, int max, int step, int defaultValue, int capabilities)
    {
        Min = min;
        Max = max;
        Step = step;
        Default = defaultValue;
        Capabilities = capabilities;
    }

    /// <summary>最小值。</summary>
    public int Min { get; }

    /// <summary>最大值。</summary>
    public int Max { get; }

    /// <summary>步进值。</summary>
    public int Step { get; }

    /// <summary>驱动建议的默认值。</summary>
    public int Default { get; }

    /// <summary>
    /// 能力标志位。低两位对应 Auto / Manual 的位掩码。
    /// 值为 0 表示该参数不可用。
    /// </summary>
    public int Capabilities { get; }

    /// <summary>是否支持自动模式。</summary>
    public bool SupportsAuto => (Capabilities & 0x0001) != 0;

    /// <summary>是否支持手动模式。</summary>
    public bool SupportsManual => (Capabilities & 0x0002) != 0;

    /// <inheritdoc />
    public override string ToString()
        => $"Range [{Min}, {Max}] step={Step} default={Default} caps=0x{Capabilities:X}";
}