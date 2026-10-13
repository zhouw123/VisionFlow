using VisionFlow.Integration;
using VisionFlow.Streaming;

namespace VisionFlow.Sinks;

/// <summary>
/// 委托式 Sink。用户传入一个回调，每帧到达时调用。
/// 适合快速原型、单次处理、灵活逻辑。
/// 回调在 Sink 的消费线程上执行，用户负责切换线程（如 UI 线程）。
/// </summary>
public sealed class DelegateSink : SinkComponent
{
    private readonly Action<FrameData, PipelineContext> _handler;

    /// <summary>
    /// 创建委托式 Sink。
    /// </summary>
    /// <param name="handler">每帧的回调。参数为帧数据和上下文。</param>
    /// <param name="name">组件名称。</param>
    /// <param name="queueCapacity">输入队列容量。默认 8。</param>
    /// <param name="policy">背压策略。默认 DropOldest。</param>
    public DelegateSink(
        Action<FrameData, PipelineContext> handler,
        string? name = null,
        int queueCapacity = 8,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base(name ?? "DelegateSink", queueCapacity, policy)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// 每帧调用用户回调。
    /// </summary>
    protected override void OnFrame(FrameData frame, PipelineContext context)
    {
        _handler(frame, context);
    }
}