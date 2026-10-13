using VisionFlow.Integration;
using VisionFlow.Streaming;

namespace VisionFlow.Sinks;

/// <summary>
/// 空输出。消费帧但什么都不做。
/// 用途：
///   1. 分流场景下作为某个分支的终点（比如只记录流量的分支）
///   2. 测试时让 Source 有下游（否则源会认为无消费者）
/// </summary>
public sealed class NullSink : SinkComponent
{
    /// <summary>
    /// 创建空输出。
    /// </summary>
    /// <param name="name">组件名称。</param>
    /// <param name="queueCapacity">输入队列容量。默认 4。</param>
    /// <param name="policy">背压策略。默认 DropOldest。</param>
    public NullSink(
        string? name = null,
        int queueCapacity = 4,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base(name ?? "NullSink", queueCapacity, policy)
    {
    }

    /// <summary>不做任何处理。</summary>
    protected override void OnFrame(FrameData frame, PipelineContext context)
    {
    }
}