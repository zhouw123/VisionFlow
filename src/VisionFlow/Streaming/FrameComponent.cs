using VisionFlow.Integration;
using VisionFlow.Streaming.Parameters;

namespace VisionFlow.Streaming;

/// <summary>
/// 所有流式组件的抽象基类。
/// 提供名称、状态、上下文、下游链接、生命周期管理等公共能力。
/// </summary>
public abstract class FrameComponent : IFrameComponent, IParameterized, IDisposable
{
    private readonly List<FrameComponent> _downstream = new();
    private readonly List<ParameterBinding> _parameterBindings = new();
    private readonly object _parameterLock = new();
    private IReadOnlyList<ParameterDescriptor>? _parametersCache;
    private readonly object _downstreamLock = new();
    private readonly string _name;
    private PipelineContext? _context;
    private ComponentState _state = ComponentState.Created;
    private bool _disposed;

    /// <summary>
    /// 创建组件。
    /// </summary>
    /// <param name="name">组件名称。未提供时用类型名。</param>
    protected FrameComponent(string? name = null)
    {
        _name = string.IsNullOrEmpty(name) ? GetType().Name : name!;
    }

    /// <summary>组件名称。</summary>
    public string Name => _name;

    /// <summary>当前状态。</summary>
    public ComponentState State => _state;

    /// <summary>共享上下文。Start 之前为 null，Start 之后由 StreamingGraph 注入。</summary>
    public PipelineContext Context
    {
        get
        {
            if (_context is null)
            {
                throw new InvalidOperationException($"组件 {Name} 尚未启动，Context 未初始化。");
            }
            return _context;
        }
    }

    /// <summary>Context 是否已初始化。</summary>
    public bool HasContext => _context is not null;

    /// <summary>组件暴露的所有参数描述。首次访问后缓存。</summary>
    public IReadOnlyList<ParameterDescriptor> Parameters
    {
        get
        {
            lock (_parameterLock)
            {
                if (_parametersCache is null)
                {
                    _parametersCache = _parameterBindings
                        .Select(b => b.Descriptor)
                        .ToArray();
                }
                return _parametersCache;
            }
        }
    }

    /// <summary>参数变化事件。</summary>
    public event EventHandler<ParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Raised when the component encounters an error during its operation.
    /// Handlers run on the component's own thread.
    /// </summary>
    public event EventHandler<ComponentErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Raise the ErrorOccurred event. Safe to call from any thread.
    /// </summary>
    protected void RaiseError(Exception ex, string context)
    {
        try
        {
            ErrorOccurred?.Invoke(this, new ComponentErrorEventArgs(ex, context));
        }
        catch
        {
            // Error handlers must not throw into the component
        }
    }

    /// <summary>
    /// 读取一个参数的当前值。参数不存在时返回 null。
    /// </summary>
    public object? GetParameter(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        ParameterBinding? binding;
        lock (_parameterLock)
        {
            binding = _parameterBindings.FirstOrDefault(b =>
                string.Equals(b.Descriptor.Key, key, StringComparison.Ordinal));
        }

        if (binding is null) return null;

        try { return binding.Getter(); }
        catch { return null; }
    }

    /// <summary>
    /// 修改一个参数。参数不存在时抛异常。
    /// </summary>
    public void SetParameter(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        ParameterBinding? binding;
        lock (_parameterLock)
        {
            binding = _parameterBindings.FirstOrDefault(b =>
                string.Equals(b.Descriptor.Key, key, StringComparison.Ordinal));
        }

        if (binding is null)
        {
            throw new ArgumentException($"参数 {key} 不存在。", nameof(key));
        }

        object? oldValue;
        try { oldValue = binding.Getter(); }
        catch { oldValue = null; }

        try { binding.Setter(value); }
        catch (Exception ex)
        {
            throw new ArgumentException($"设置参数 {key} 失败: {ex.Message}", nameof(value), ex);
        }

        ParameterChanged?.Invoke(this, new ParameterChangedEventArgs(key, oldValue, value));
    }

    /// <summary>
    /// 派生类调用：注册一个参数。
    /// </summary>
    /// <typeparam name="T">参数类型。</typeparam>
    /// <param name="key">唯一标识。</param>
    /// <param name="displayName">显示名。</param>
    /// <param name="getter">读取当前值的委托。</param>
    /// <param name="setter">写入新值的委托。</param>
    /// <param name="kind">参数类型。默认从 T 推断。</param>
    /// <param name="description">描述。可选。</param>
    /// <param name="min">最小值。可选。</param>
    /// <param name="max">最大值。可选。</param>
    /// <param name="step">步进。可选。</param>
    /// <param name="unit">单位。可选。</param>
    /// <param name="enumOptions">枚举选项。仅 Enum 类型用。</param>
    protected void RegisterParameter<T>(
        string key,
        string displayName,
        Func<T> getter,
        Action<T> setter,
        ParameterKind? kind = null,
        string? description = null,
        double? min = null,
        double? max = null,
        double? step = null,
        string? unit = null,
        IReadOnlyList<EnumOption>? enumOptions = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);

        var actualKind = kind ?? InferKind(typeof(T));

        var descriptor = new ParameterDescriptor(
            key, displayName, actualKind,
            description, min, max, step, unit, enumOptions);

        // 包装 getter/setter 为 object 版本
        Func<object?> objectGetter = () => getter();
        Action<object?> objectSetter = v =>
        {
            if (v is T typed) setter(typed);
            else if (v is null && default(T) is null) setter(default!);
            else throw new InvalidCastException(
                $"参数 {key} 期望类型 {typeof(T).Name}，收到 {v?.GetType().Name ?? "null"}。");
        };

        lock (_parameterLock)
        {
            if (_parameterBindings.Any(b =>
                string.Equals(b.Descriptor.Key, key, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"参数 {key} 已注册。");
            }

            _parameterBindings.Add(new ParameterBinding(descriptor, objectGetter, objectSetter));
            _parametersCache = null; // 缓存失效
        }
    }

    private static ParameterKind InferKind(Type type)
    {
        if (type == typeof(bool)) return ParameterKind.Bool;
        if (type == typeof(int)) return ParameterKind.Int;
        if (type == typeof(long)) return ParameterKind.Int;
        if (type == typeof(float) || type == typeof(double)) return ParameterKind.Double;
        if (type == typeof(string)) return ParameterKind.String;
        if (type.IsEnum) return ParameterKind.Enum;
        return ParameterKind.String;
    }

    /// <summary>下游组件列表。只读。</summary>
    public IReadOnlyList<FrameComponent> Downstream
    {
        get
        {
            lock (_downstreamLock) { return _downstream.ToArray(); }
        }
    }

    /// <summary>
    /// 连接到一个下游组件。链式调用：source.LinkTo(a).LinkTo(b) 会先连接 source→a，再连接 a→b。
    /// 必须在 Start 之前调用。
    /// </summary>
    /// <returns>下游组件，用于继续链式连接。</returns>
    public FrameComponent LinkTo(FrameComponent downstream)
    {
        ArgumentNullException.ThrowIfNull(downstream);
        ThrowIfDisposed();

        if (_state != ComponentState.Created)
        {
            throw new InvalidOperationException(
                $"组件 {Name} 已启动，无法再建立连接。");
        }

        if (downstream == this)
        {
            throw new InvalidOperationException("不能连接到自身。");
        }

        lock (_downstreamLock)
        {
            if (_downstream.Contains(downstream))
            {
                throw new InvalidOperationException(
                    $"组件 {Name} 已经连接到 {downstream.Name}。");
            }
            _downstream.Add(downstream);
        }

        OnLinked(downstream);
        return downstream;
    }

    /// <summary>
    /// 由 StreamingGraph 调用，注入共享上下文。
    /// </summary>
    internal void SetContext(PipelineContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// 由 StreamingGraph 调用，仅标记状态为 Running，不启动线程。
    /// 用于两阶段启动：先标记所有组件，再启动各自线程，避免启动时序导致的帧丢失。
    /// </summary>
    internal void MarkRunning()
    {
        ThrowIfDisposed();

        if (_state == ComponentState.Running) return;
        if (_state != ComponentState.Created && _state != ComponentState.Stopped)
        {
            throw new InvalidOperationException(
                $"组件 {Name} 状态为 {_state}，无法启动。");
        }

        _state = ComponentState.Running;
    }

    /// <summary>
    /// 由 StreamingGraph 调用，启动组件线程。应在所有组件 MarkRunning 之后调用。
    /// </summary>
    internal void StartThreads()
    {
        ThrowIfDisposed();

        if (_state != ComponentState.Running) return;

        try
        {
            OnStart();
        }
        catch
        {
            _state = ComponentState.Created;
            throw;
        }
    }

    /// <summary>
    /// 由 StreamingGraph 调用，停止组件。
    /// </summary>
    internal void StopInternal()
    {
        if (_state != ComponentState.Running) return;

        _state = ComponentState.Stopping;
        try
        {
            OnStop();
        }
        finally
        {
            _state = ComponentState.Stopped;
        }
    }

    /// <summary>
    /// 派生类实现：启动内部线程或资源。
    /// </summary>
    protected abstract void OnStart();

    /// <summary>
    /// 派生类实现：停止内部线程或资源。应等待线程退出。
    /// </summary>
    protected abstract void OnStop();

    /// <summary>
    /// 派生类可重写：当连接到下游时被调用。用于注入上下文等。
    /// </summary>
    protected virtual void OnLinked(FrameComponent downstream) { }

    /// <summary>
    /// 把一帧推送给所有下游。
    /// 每个下游的 PushFrame 是线程安全的，内部处理背压。
    /// </summary>
    protected void PushToDownstream(FrameData frame)
    {
        FrameComponent[] targets;
        lock (_downstreamLock)
        {
            if (_downstream.Count == 0) return;
            targets = _downstream.ToArray();
        }

        foreach (var target in targets)
        {
            try { target.PushFrame(frame); }
            catch { /* 单个下游失败不影响其他 */ }
        }
    }

    /// <summary>
    /// 由上游组件调用，把一帧推入本组件。
    /// 默认实现为空（Source 不接受输入）。派生类重写。
    /// </summary>
    internal virtual void PushFrame(FrameData frame)
    {
        // 默认：忽略
    }

    /// <summary>
    /// 抛出异常如果已释放。
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    /// <summary>
    /// 释放资源。会先停止组件。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try { StopInternal(); } catch { }

        Dispose(true);
        GC.SuppressFinalize(this);
        _disposed = true;
        _state = ComponentState.Disposed;
    }

    /// <summary>
    /// 派生类可重写：释放额外的非托管或托管资源。
    /// </summary>
    protected virtual void Dispose(bool disposing) { }

    /// <inheritdoc />
    public override string ToString() => $"{Name} [{_state}]";
}