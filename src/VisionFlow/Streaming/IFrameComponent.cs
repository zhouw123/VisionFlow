using VisionFlow.Integration;

namespace VisionFlow.Streaming;

/// <summary>所有流式组件的公共接口。</summary>
public interface IFrameComponent
{
    /// <summary>组件名称。用于日志和诊断。</summary>
    string Name { get; }

    /// <summary>当前状态。</summary>
    ComponentState State { get; }

    /// <summary>共享的流水线上下文。结构化数据通过它传递。</summary>
    PipelineContext Context { get; }
}