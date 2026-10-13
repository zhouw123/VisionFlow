namespace VisionFlow.Infrastructure;

/// <summary>
/// 帧身份标识。贯穿从采集到处理的整条链路，用于图像帧和结构化数据的精确配对。
/// 
/// 以 FrameNumber 为唯一键——它由我们自己的计数器生成，严格递增且唯一。
/// SampleTime 来自 DirectShow，用于录像回放时对齐时间轴。
/// WallClock 用于日志和持久化。
/// </summary>
public readonly struct FrameId : IEquatable<FrameId>
{
    /// <summary>
    /// 构造一个 FrameId。
    /// </summary>
    /// <param name="sampleTime">DirectShow 采样时间（秒）。</param>
    /// <param name="frameNumber">帧序号，从 1 开始递增。</param>
    /// <param name="wallClock">真实世界时间。</param>
    public FrameId(double sampleTime, long frameNumber, DateTime wallClock)
    {
        SampleTime = sampleTime;
        FrameNumber = frameNumber;
        WallClock = wallClock;
    }

    /// <summary>DirectShow 采样时间（秒）。</summary>
    public double SampleTime { get; }

    /// <summary>帧序号，从 1 开始递增。唯一标识一帧。</summary>
    public long FrameNumber { get; }

    /// <summary>真实世界时间。用于日志和持久化。</summary>
    public DateTime WallClock { get; }

    /// <summary>
    /// 相等比较以 FrameNumber 为唯一键。
    /// </summary>
    public bool Equals(FrameId other) => FrameNumber == other.FrameNumber;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FrameId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => FrameNumber.GetHashCode();

    /// <summary>== 运算符。</summary>
    public static bool operator ==(FrameId left, FrameId right) => left.Equals(right);

    /// <summary>!= 运算符。</summary>
    public static bool operator !=(FrameId left, FrameId right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString()
        => $"#{FrameNumber} @ {SampleTime:F3}s";
}