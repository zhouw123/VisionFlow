namespace VisionFlow.Streaming;

/// <summary>
/// 帧转换接口。
/// 实现类接收上游的帧，处理后输出到下游。
/// 典型实现：MjpegDecodeTransform、Yuy2ToBgrTransform、CannyTransform 等。
/// </summary>
public interface IFrameTransform : IFrameComponent
{
}