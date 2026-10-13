namespace VisionFlow.Streaming;

/// <summary>
/// 帧输出接口。
/// 实现类是流水线的终点，消费帧但不产生新帧。
/// 典型实现：ImageSink、VideoFileSink、NullSink 等。
/// </summary>
public interface IFrameSink : IFrameComponent
{
}