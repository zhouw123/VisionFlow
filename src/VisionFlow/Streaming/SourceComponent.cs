using System.Collections.Concurrent;
using VisionFlow.Integration;

namespace VisionFlow.Streaming;

/// <summary>
/// 帧源组件的基类。
/// 内部有一个分发队列 + 分发线程：外部通过 RaiseFrameProduced 推入帧，
/// 分发线程从队列取出，推送给所有下游。
/// 这样外部生产者（比如 DirectShow 流线程）不会被下游的慢速处理阻塞。
/// </summary>
public abstract class SourceComponent : FrameComponent, IFrameSource
{
    private readonly BlockingCollection<FrameData> _dispatchQueue;
    private Thread? _dispatchThread;

    /// <summary>
    /// 创建 Source 组件。
    /// </summary>
    /// <param name="name">组件名称。</param>
    /// <param name="queueCapacity">分发队列容量。默认 4。</param>
    protected SourceComponent(string? name = null, int queueCapacity = 4)
        : base(name)
    {
        if (queueCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }
        _dispatchQueue = new BlockingCollection<FrameData>(queueCapacity);
    }

    /// <summary>已推入的帧总数。</summary>
    public long TotalFramesProduced => Interlocked.Read(ref _totalProduced);
    private long _totalProduced;

    /// <summary>因分发队列满而丢弃的帧数。</summary>
    public long DroppedFrames => Interlocked.Read(ref _dropped);
    private long _dropped;

    /// <summary>
    /// 由派生类在产生一帧时调用。
    /// 通常从外部流线程调用，因此不能阻塞：队列满时丢旧帧。
    /// </summary>
    protected void RaiseFrameProduced(FrameData frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (State != ComponentState.Running) return;

        Interlocked.Increment(ref _totalProduced);

        if (_dispatchQueue.IsAddingCompleted) return;

        try
        {
            if (_dispatchQueue.TryAdd(frame)) return;

            // 队列满：丢一个最旧的
            if (_dispatchQueue.TryTake(out _))
            {
                Interlocked.Increment(ref _dropped);
                if (_dispatchQueue.TryAdd(frame)) return;
            }
            Interlocked.Increment(ref _dropped);
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// 启动分发线程。
    /// </summary>
    protected override void OnStart()
    {
        OnStartSource();

        _dispatchThread = new Thread(DispatchLoop)
        {
            IsBackground = true,
            Name = $"Source.{Name}"
        };
        _dispatchThread.Start();
    }

    /// <summary>
    /// 停止分发线程。
    /// </summary>
    protected override void OnStop()
    {
        try { OnStopSource(); } catch { }

        try { _dispatchQueue.CompleteAdding(); } catch { }
        try { _dispatchThread?.Join(2000); } catch { }
        _dispatchThread = null;
    }

    private void DispatchLoop()
    {
        try
        {
            foreach (var frame in _dispatchQueue.GetConsumingEnumerable())
            {
                PushToDownstream(frame);
            }
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
        catch (Exception ex) { RaiseError(ex, "DispatchLoop"); }
    }

    /// <summary>
    /// 派生类实现：启动外部帧源（比如打开相机）。
    /// </summary>
    protected abstract void OnStartSource();

    /// <summary>
    /// 派生类实现：停止外部帧源。
    /// </summary>
    protected abstract void OnStopSource();

    /// <summary>
    /// 释放资源。
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _dispatchQueue.Dispose(); } catch { }
        }
    }
}