namespace VisionFlow.Streaming;

/// <summary>
/// 帧源接口。
/// 实现类负责产生帧，推送给下游。
/// 典型实现：DirectShowCameraSource、VideoFileSource 等。
/// </summary>
public interface IFrameSource : IFrameComponent
{
}