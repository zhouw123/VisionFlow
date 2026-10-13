using VisionFlow.Infrastructure;

namespace VisionFlow.Integration;

/// <summary>
/// 结构化数据的载体。由流水线的某一级产生，携带帧身份和结果值。
/// 与图像数据独立：图像走帧队列（可丢），结构化数据走事件或持久化队列（不丢）。
/// </summary>
public sealed class PipelineResult
{
    /// <summary>
    /// 构造一个 PipelineResult。
    /// </summary>
    /// <param name="id">帧身份标识。</param>
    /// <param name="key">结果的键。通常为产生该结果的类型名。</param>
    /// <param name="value">结果值。类型由产生方和消费方约定。</param>
    public PipelineResult(FrameId id, string key, object value)
    {
        Id = id;
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>帧身份标识。</summary>
    public FrameId Id { get; }

    /// <summary>结果的键。通常为产生该结果的类型名。</summary>
    public string Key { get; }

    /// <summary>结果值。类型由产生方和消费方约定。</summary>
    public object Value { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"{Id} [{Key}]";
}