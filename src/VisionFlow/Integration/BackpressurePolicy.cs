namespace VisionFlow.Integration;

/// <summary>
/// 流水线的背压策略。决定队列满时的行为。
/// </summary>
public enum BackpressurePolicy
{
    /// <summary>
    /// 丢弃最旧的帧，保证队列里始终是最新的帧。
    /// 适用于实时预览：流畅优先，允许丢帧。
    /// 也是流线程入队时的唯一安全选择（不阻塞）。
    /// </summary>
    DropOldest,

    /// <summary>
    /// 阻塞等待队列有空位。
    /// 适用于保证不丢帧的场景，但会拖慢上游。
    /// 不要在流线程上使用。
    /// </summary>
    Block,
}