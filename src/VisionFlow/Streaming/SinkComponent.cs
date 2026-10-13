using System.Collections.Concurrent;
using VisionFlow.Integration;

namespace VisionFlow.Streaming;

/// <summary>
/// 输出组件的基类。
/// 内部有输入队列 + 消费线程。上游推入帧，消费线程调用派生类的 OnFrame。
/// </summary>
public abstract class SinkComponent : FrameComponent, IFrameSink
{
    private readonly BlockingCollection<FrameData> _inputQueue;
    private readonly BackpressurePolicy _policy;
    private Thread? _workerThread;

    /// <summary>
    /// 创建 Sink 组件。
    /// </summary>
    /// <param name="name">组件名称。</param>
    /// <param name="queueCapacity">输入队列容量。默认 8。</param>
    /// <param name="policy">背压策略。默认 DropOldest。
    /// Sink 通常承载重要操作（保存、上报），建议用 Block 或大容量队列。</param>
    protected SinkComponent(
        string? name = null,
        int queueCapacity = 8,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base(name)
    {
        if (queueCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }
        _inputQueue = new BlockingCollection<FrameData>(queueCapacity);
        _policy = policy;
    }

    /// <summary>已消费的帧数。</summary>
    public long ConsumedFrames => Interlocked.Read(ref _consumed);
    private long _consumed;

    /// <summary>因队列满而丢弃的帧数。</summary>
    public long DroppedFrames => Interlocked.Read(ref _dropped);
    private long _dropped;

    /// <summary>
    /// 上游推入一帧。
    /// </summary>
    internal override void PushFrame(FrameData frame)
    {
        if (State != ComponentState.Running) return;
        if (_inputQueue.IsAddingCompleted) return;

        try
        {
            if (_policy == BackpressurePolicy.DropOldest)
            {
                if (_inputQueue.TryAdd(frame)) return;

                if (_inputQueue.TryTake(out _))
                {
                    Interlocked.Increment(ref _dropped);
                    if (_inputQueue.TryAdd(frame)) return;
                }
                Interlocked.Increment(ref _dropped);
            }
            else
            {
                _inputQueue.Add(frame);
            }
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// 启动消费线程。
    /// </summary>
    protected override void OnStart()
    {
        OnStartSink();

        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = $"Sink.{Name}"
        };
        _workerThread.Start();
    }

    /// <summary>
    /// 停止消费线程。
    /// </summary>
    protected override void OnStop()
    {
        try { _inputQueue.CompleteAdding(); } catch { }
        try { _workerThread?.Join(2000); } catch { }
        _workerThread = null;

        try { OnStopSink(); } catch { }
    }

    private void WorkerLoop()
    {
        try
        {
            foreach (var frame in _inputQueue.GetConsumingEnumerable())
            {
                try
                {
                    OnFrame(frame, Context);
                    Interlocked.Increment(ref _consumed);
                }
                catch (Exception ex)
                {
                    RaiseError(ex, "OnFrame");
                }
            }
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
        catch (Exception ex) { RaiseError(ex, "WorkerLoop"); }
    }

    /// <summary>
    /// 派生类实现：处理一帧。在独立线程上调用。
    /// </summary>
    protected abstract void OnFrame(FrameData frame, PipelineContext context);

    /// <summary>
    /// 派生类可重写：启动时的额外初始化。
    /// </summary>
    protected virtual void OnStartSink() { }

    /// <summary>
    /// 派生类可重写：停止时的额外清理。
    /// </summary>
    protected virtual void OnStopSink() { }

    /// <summary>
    /// 释放资源。
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _inputQueue.Dispose(); } catch { }
        }
    }
}