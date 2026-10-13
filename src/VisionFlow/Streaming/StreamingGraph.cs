using VisionFlow.Integration;

namespace VisionFlow.Streaming;

/// <summary>
/// 流式图。统一管理一组连通组件的生命周期。
/// 用法：
///   var graph = StreamingGraph.From(source);  // 从 source 出发自动发现所有下游
///   graph.Start();
///   ...
///   graph.Stop();
/// 或者手动添加：
///   var graph = new StreamingGraph();
///   graph.Add(source);
///   graph.Add(decode);
///   graph.AddSink(uiSink);
///   graph.Start();
/// </summary>
public sealed class StreamingGraph : IDisposable
{
    private readonly List<FrameComponent> _components = new();
    private readonly List<FrameComponent> _startOrder = new();
    private readonly object _lock = new();
    private PipelineContext? _context;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// 创建一个空的流式图。
    /// </summary>
    public StreamingGraph()
    {
    }

    /// <summary>已加入图的组件数。</summary>
    public int ComponentCount => _components.Count;

    /// <summary>共享的流水线上下文。Start 之前为 null。</summary>
    public PipelineContext? Context => _context;

    /// <summary>是否已启动。</summary>
    public bool IsStarted => _started;

    /// <summary>
    /// 从起始组件出发，自动发现所有连通的下游组件，构建图。
    /// </summary>
    public static StreamingGraph From(FrameComponent root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var graph = new StreamingGraph();
        var visited = new HashSet<FrameComponent>();
        graph.CollectFrom(root, visited);
        graph.ComputeStartOrder();
        return graph;
    }

    /// <summary>
    /// 手动添加一个组件。
    /// </summary>
    public StreamingGraph Add(FrameComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        ThrowIfDisposed();

        if (_started)
        {
            throw new InvalidOperationException("已启动的图不能再添加组件。");
        }

        lock (_lock)
        {
            if (!_components.Contains(component))
            {
                _components.Add(component);
            }
        }

        return this;
    }

    private void CollectFrom(FrameComponent root, HashSet<FrameComponent> visited)
    {
        if (!visited.Add(root)) return;

        _components.Add(root);

        foreach (var downstream in root.Downstream)
        {
            CollectFrom(downstream, visited);
        }
    }

    private void ComputeStartOrder()
    {
        // 计算每个组件的"深度"：源为 0，下游递增
        // 用 BFS 保证深度正确
        var depth = new Dictionary<FrameComponent, int>();
        var queue = new Queue<FrameComponent>();

        // 找出所有入度为 0 的节点（没有上游的组件）作为起点
        var hasUpstream = new HashSet<FrameComponent>();
        foreach (var c in _components)
        {
            foreach (var d in c.Downstream)
            {
                hasUpstream.Add(d);
            }
        }

        foreach (var c in _components)
        {
            if (!hasUpstream.Contains(c))
            {
                depth[c] = 0;
                queue.Enqueue(c);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int currentDepth = depth[current];

            foreach (var downstream in current.Downstream)
            {
                if (!depth.ContainsKey(downstream))
                {
                    depth[downstream] = currentDepth + 1;
                    queue.Enqueue(downstream);
                }
            }
        }

        // 按深度排序（浅的先启动）
        _startOrder.Clear();
        _startOrder.AddRange(
            _components.OrderBy(c => depth.TryGetValue(c, out var d) ? d : int.MaxValue)
        );
    }

    /// <summary>
    /// 启动所有组件。创建一个共享 PipelineContext 注入所有组件，
    /// 然后按拓扑顺序依次启动。
    /// </summary>
    public void Start()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_started) return;
            if (_components.Count == 0)
            {
                throw new InvalidOperationException("没有添加任何组件。");
            }

            // 若未通过 From 构建，需要计算启动顺序
            if (_startOrder.Count == 0)
            {
                ComputeStartOrder();
            }

            // 创建共享 Context 并注入
            _context = new PipelineContext();
            foreach (var c in _components)
            {
                c.SetContext(_context);
            }

            // 两阶段启动：
            // 第一阶段：标记所有组件为 Running（不启动线程）
            // 第二阶段：依次启动组件线程
            // 这样任何组件线程运行时，所有下游状态都已就绪，不会丢帧。

            var marked = new List<FrameComponent>();
            var started = new List<FrameComponent>();

            try
            {
                // 第一阶段：全部标记为 Running
                foreach (var c in _startOrder)
                {
                    c.MarkRunning();
                    marked.Add(c);
                }

                // 第二阶段：依次启动线程
                foreach (var c in _startOrder)
                {
                    c.StartThreads();
                    started.Add(c);
                }

                _started = true;
            }
            catch
            {
                // 启动中途失败，反向停止已启动的线程
                for (int i = started.Count - 1; i >= 0; i--)
                {
                    try { started[i].StopInternal(); } catch { }
                }
                throw;
            }
        }
    }

    /// <summary>
    /// 停止所有组件。按启动的反向顺序停止。
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_started) return;

            // 反向停止：下游先停，上游后停
            for (int i = _startOrder.Count - 1; i >= 0; i--)
            {
                try { _startOrder[i].StopInternal(); }
                catch { }
            }

            _started = false;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(StreamingGraph));
        }
    }

    /// <summary>
    /// 停止并释放所有组件。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try { Stop(); } catch { }

        foreach (var c in _components)
        {
            try { c.Dispose(); } catch { }
        }
        _components.Clear();
        _startOrder.Clear();

        _context?.Dispose();
        _context = null;

        _disposed = true;
    }
}