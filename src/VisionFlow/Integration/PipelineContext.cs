using System.Collections.Concurrent;
using VisionFlow.Infrastructure;

namespace VisionFlow.Integration;

/// <summary>
/// 流水线共享上下文。
/// 各级可以在此写入/读取结构化数据，供其他级或 UI 层消费。
/// 提供两条路径：
///   1. 实时更新：Set&lt;T&gt; + ResultProduced 事件（可覆盖，可丢）
///   2. 持久化：PersistentQueue（不丢，独立消费者）
/// </summary>
public sealed class PipelineContext : IDisposable
{
    // 实时结果：按类型存储最新值
    private readonly ConcurrentDictionary<Type, object> _latest = new();

    // 持久化队列：容量可配，满了 TryAdd 返回 false（不阻塞）
    private readonly BlockingCollection<PipelineResult> _persistentQueue;

    private bool _disposed;

    /// <summary>
    /// 创建 PipelineContext。
    /// </summary>
    /// <param name="persistentQueueCapacity">持久化队列容量。默认 1000。</param>
    public PipelineContext(int persistentQueueCapacity = 1000)
    {
        _persistentQueue = new BlockingCollection<PipelineResult>(persistentQueueCapacity);
    }

    /// <summary>
    /// 结构化结果产生时触发。在产生该结果的线程上执行。
    /// 订阅者负责切换到 UI 线程（如果需要）。
    /// </summary>
    public event EventHandler<PipelineResult>? ResultProduced;

    /// <summary>
    /// 持久化队列。不丢结果，供独立消费者处理（日志、数据库、报警等）。
    /// </summary>
    public BlockingCollection<PipelineResult> PersistentQueue => _persistentQueue;

    /// <summary>
    /// 写入一个结构化结果。类型 T 作为键。
    /// 会覆盖同类型的旧值，并触发 ResultProduced 事件。
    /// </summary>
    public void Set<T>(T value, FrameId id) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ThrowIfDisposed();

        _latest[typeof(T)] = value;

        var result = new PipelineResult(id, typeof(T).Name, value);
        ResultProduced?.Invoke(this, result);
    }

    /// <summary>
    /// 写入一个结构化结果，并同时加入持久化队列（不丢）。
    /// 队列满时返回 false，不阻塞。
    /// </summary>
    /// <returns>是否成功加入持久化队列。</returns>
    public bool SetAndPersist<T>(T value, FrameId id) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ThrowIfDisposed();

        _latest[typeof(T)] = value;

        var result = new PipelineResult(id, typeof(T).Name, value);
        ResultProduced?.Invoke(this, result);

        return _persistentQueue.TryAdd(result);
    }

    /// <summary>
    /// 读取指定类型的最新值。未设置过时返回 null。
    /// </summary>
    public T? Get<T>() where T : class
    {
        ThrowIfDisposed();
        return _latest.TryGetValue(typeof(T), out var v) ? (T)v : null;
    }

    /// <summary>
    /// 标记持久化队列不再接受新数据。消费者在消费完现有数据后退出。
    /// </summary>
    public void CompletePersistentQueue()
    {
        if (!_persistentQueue.IsAddingCompleted)
        {
            _persistentQueue.CompleteAdding();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PipelineContext));
        }
    }

    /// <summary>
    /// 释放资源。停止持久化队列。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _persistentQueue.CompleteAdding(); } catch { }
        _persistentQueue.Dispose();
    }
}