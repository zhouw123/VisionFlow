namespace VisionFlow.Streaming;

/// <summary>组件的生命周期状态。</summary>
public enum ComponentState
{
    /// <summary>已创建，未启动。</summary>
    Created,

    /// <summary>运行中。</summary>
    Running,

    /// <summary>正在停止（队列已 CompleteAdding，等待线程退出）。</summary>
    Stopping,

    /// <summary>已停止。</summary>
    Stopped,

    /// <summary>已释放。</summary>
    Disposed,
}