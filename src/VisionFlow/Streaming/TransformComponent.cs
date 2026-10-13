using System.Collections.Concurrent;
using VisionFlow.Integration;

namespace VisionFlow.Streaming;

/// <summary>
/// 转换组件的基类。
/// 内部有输入队列 + 处理线程。上游通过 PushFrame 推入帧，
/// 处理线程消费后调用派生类的 ProcessFrame，把结果推送给下游。
/// </summary>
public abstract class TransformComponent : FrameComponent, IFrameTransform
{
    private readonly BlockingCollection<FrameData> _inputQueue;
    private readonly BackpressurePolicy _policy;
    private Thread? _workerThread;

    /// <summary>
    /// 创建 Transform 组件。
    /// </summary>
    /// <param name="name">组件名称。</param>
    /// <param name="queueCapacity">输入队列容量。默认 2。</param>
    /// <param name="policy">背压策略。默认 DropOldest。</param>
    protected TransformComponent(
        string? name = null,
        int queueCapacity = 2,
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

    /// <summary>已处理的帧数。</summary>
    public long ProcessedFrames => Interlocked.Read(ref _processed);
    private long _processed;

    /// <summary>因队列满而丢弃的帧数。</summary>
    public long DroppedFrames => Interlocked.Read(ref _dropped);
    private long _dropped;

    /// <summary>
    /// 上游推入一帧。由 SourceComponent 或上游 TransformComponent 调用。
    /// 可能在外部线程上执行（比如 Source 的分发线程）。
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
    /// 启动处理线程。
    /// </summary>
    protected override void OnStart()
    {
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = $"Transform.{Name}"
        };
        _workerThread.Start();
    }

    /// <summary>
    /// 停止处理线程。
    /// </summary>
    protected override void OnStop()
    {
        try { _inputQueue.CompleteAdding(); } catch { }
        try { _workerThread?.Join(2000); } catch { }
        _workerThread = null;
    }

    private void WorkerLoop()
    {
        try
        {
            foreach (var frame in _inputQueue.GetConsumingEnumerable())
            {
                FrameData? output = null;
                try
                {
                    output = ProcessFrame(frame, Context);
                }
                catch (Exception ex)
                {
                    RaiseError(ex, "ProcessFrame");
                    continue;
                }

                if (output is null) continue;
                Interlocked.Increment(ref _processed);
                PushToDownstream(output);
            }
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
        catch (Exception ex) { RaiseError(ex, "WorkerLoop"); }
    }

    /// <summary>
    /// 派生类实现：处理一帧。
    /// 可以返回新的 FrameData（改变尺寸/格式），也可以返回 null 表示丢弃。
    /// 本方法在独立线程上调用，不应阻塞。
    /// </summary>
    protected abstract FrameData? ProcessFrame(FrameData input, PipelineContext context);

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