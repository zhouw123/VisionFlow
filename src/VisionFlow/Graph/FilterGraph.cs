using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Filters;
using VisionFlow.Infrastructure;

namespace VisionFlow.Graph;

/// <summary>
/// 过滤器图管理器。
/// 封装 IFilterGraph2 / IMediaControl / IMediaEventEx，
/// 提供过滤器添加、显式连接、运行控制与事件订阅。
/// </summary>
public sealed class FilterGraph : IDisposable
{
    private readonly IFilterGraph2 _graph;
    private readonly IMediaControl _mediaControl;
    private readonly IMediaEventEx _mediaEventEx;
    private readonly MediaEventPump _eventPump;
    private readonly List<FilterBase> _filters = new();
    private bool _disposed;

    /// <summary>
    /// 创建过滤器图并启动媒体事件泵。
    /// </summary>
    public FilterGraph()
    {
        var graphType = Type.GetTypeFromCLSID(DirectShowClsIds.FilterGraph)
            ?? throw new InvalidOperationException("无法获取 FilterGraph 的 COM 类型。");

        var instance = Activator.CreateInstance(graphType)
            ?? throw new InvalidOperationException("无法创建 FilterGraph 实例。");

        _graph = (IFilterGraph2)instance;
        _mediaControl = (IMediaControl)instance;
        _mediaEventEx = (IMediaEventEx)instance;
        _eventPump = new MediaEventPump(_mediaEventEx);
    }

    /// <summary>
    /// 当前已加入图的过滤器列表。
    /// </summary>
    public IReadOnlyList<FilterBase> Filters => _filters;

    /// <summary>图播放完成事件。</summary>
    public event EventHandler? Complete
    {
        add => _eventPump.Complete += value;
        remove => _eventPump.Complete -= value;
    }

    /// <summary>图运行出错事件。</summary>
    public event EventHandler<Exception>? ErrorAbort
    {
        add => _eventPump.ErrorAbort += value;
        remove => _eventPump.ErrorAbort -= value;
    }

    /// <summary>设备丢失事件。</summary>
    public event EventHandler? DeviceLost
    {
        add => _eventPump.DeviceLost += value;
        remove => _eventPump.DeviceLost -= value;
    }

    /// <summary>
    /// 将过滤器加入图。同一过滤器不能重复加入。
    /// </summary>
    public void AddFilter(FilterBase filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ThrowIfDisposed();

        if (_filters.Contains(filter))
        {
            throw new InvalidOperationException($"过滤器 \"{filter.Name}\" 已在图中。");
        }

        var hr = _graph.AddFilter(filter.NativeFilter, filter.Name);
        HResultHelper.ThrowIfFailed(hr, "添加过滤器 \"{0}\" 到图", filter.Name);

        _filters.Add(filter);
    }

    /// <summary>
    /// 从图中移除过滤器。
    /// </summary>
    public void RemoveFilter(FilterBase filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ThrowIfDisposed();

        if (!_filters.Contains(filter)) return;

        var hr = _graph.RemoveFilter(filter.NativeFilter);
        HResultHelper.ThrowIfFailed(hr, "从图移除过滤器 \"{0}\"", filter.Name);

        _filters.Remove(filter);
    }

    /// <summary>
    /// 显式直接连接两个 Pin，媒体类型由两端协商。
    /// </summary>
    public void Connect(Pin output, Pin input)
        => Connect(output, input, null);

    /// <summary>
    /// 显式直接连接两个 Pin，可指定媒体类型。
    /// </summary>
    public void Connect(Pin output, Pin input, AMMediaType? mediaType)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);
        ThrowIfDisposed();

        if (output.Direction != PinDirection.Output)
            throw new ArgumentException($"第一个参数必须是输出 Pin，实际方向为 {output.Direction}。", nameof(output));

        if (input.Direction != PinDirection.Input)
            throw new ArgumentException($"第二个参数必须是输入 Pin，实际方向为 {input.Direction}。", nameof(input));

        var hr = _graph.ConnectDirect(output.NativePin, input.NativePin, mediaType);
        HResultHelper.ThrowIfFailed(hr, "连接 {0} → {1}", output, input);
    }

    /// <summary>
    /// 断开指定 Pin 的连接。
    /// </summary>
    public void Disconnect(Pin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ThrowIfDisposed();
        pin.Disconnect();
    }

    /// <summary>
    /// 把图的视频输出绑定到一个窗口句柄。必须在 Run 之前调用。
    /// 适用于支持 IVideoWindow 的渲染器（如 VMR-9 窗口模式）。
    /// </summary>
    public void SetVideoWindow(IntPtr hwnd)
    {
        ThrowIfDisposed();

        if (_graph is not IVideoWindow videoWindow)
        {
            throw new InvalidOperationException(
                "当前图不支持 IVideoWindow 接口。请确认使用的渲染器支持窗口模式。");
        }

        HResultHelper.ThrowIfFailed(
            videoWindow.put_Owner(hwnd), "设置视频窗口 Owner");

        HResultHelper.ThrowIfFailed(
            videoWindow.put_WindowStyle(
                WindowStyle.Child | WindowStyle.ClipSiblings | WindowStyle.ClipChildren),
            "设置视频窗口样式");

        HResultHelper.ThrowIfFailed(
            videoWindow.put_Visible(OABool.True), "显示视频窗口");
    }

    /// <summary>
    /// 设置视频窗口在父窗口内的位置和大小。
    /// </summary>
    /// 
    public void SetVideoPosition(int left, int top, int width, int height)
    {
        ThrowIfDisposed();

        if (_graph is IVideoWindow videoWindow)
        {
            videoWindow.SetWindowPosition(left, top, width, height);
        }
    }

    /// <summary>运行图。</summary>
    public void Run()
    {
        ThrowIfDisposed();
        var hr = _mediaControl.Run();
        HResultHelper.ThrowIfFailed(hr, "运行过滤器图");
    }

    /// <summary>暂停图。</summary>
    public void Pause()
    {
        ThrowIfDisposed();
        var hr = _mediaControl.Pause();
        HResultHelper.ThrowIfFailed(hr, "暂停过滤器图");
    }

    /// <summary>停止图。</summary>
    public void Stop()
    {
        ThrowIfDisposed();
        var hr = _mediaControl.Stop();
        HResultHelper.ThrowIfFailed(hr, "停止过滤器图");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FilterGraph));
    }

    /// <summary>
    /// 停止图、释放事件泵、从图移除所有过滤器并释放 COM 对象。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try { _mediaControl.Stop(); } catch { }

        _eventPump.Dispose();

        foreach (var filter in _filters)
        {
            try { _graph.RemoveFilter(filter.NativeFilter); } catch { }
        }
        _filters.Clear();

        ComHelper.SafeRelease(_graph);
        _disposed = true;
    }

    /// <summary>
    /// 按顺序连接一组过滤器。第 i 个过滤器的输出 Pin 连接到第 i+1 个过滤器的输入 Pin。
    /// 每个过滤器必须至少有一个输出 Pin 和一个输入 Pin。
    /// </summary>
    public void ConnectChain(params FilterBase[] filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Length < 2)
        {
            throw new ArgumentException("至少需要两个过滤器。", nameof(filters));
        }

        for (int i = 0; i < filters.Length - 1; i++)
        {
            var upstream = filters[i];
            var downstream = filters[i + 1];

            var outputPin = upstream.GetFirstPin(PinDirection.Output)
                ?? throw new InvalidOperationException(
                    $"过滤器 \"{upstream.Name}\" 没有输出 Pin。");
            var inputPin = downstream.GetFirstPin(PinDirection.Input)
                ?? throw new InvalidOperationException(
                    $"过滤器 \"{downstream.Name}\" 没有输入 Pin。");

            Connect(outputPin, inputPin);
        }
    }

    /// <summary>
    /// 自动连接两个 Pin。由 DirectShow 自动寻找路径并插入必要的中间过滤器。
    /// 使用 IFilterGraph2.Connect 进行智能连接。
    /// </summary>
    public void ConnectAuto(Pin output, Pin input)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);
        ThrowIfDisposed();

        if (output.Direction != PinDirection.Output)
            throw new ArgumentException($"第一个参数必须是输出 Pin，实际方向为 {output.Direction}。", nameof(output));

        if (input.Direction != PinDirection.Input)
            throw new ArgumentException($"第二个参数必须是输入 Pin，实际方向为 {input.Direction}。", nameof(input));

        // 使用 IFilterGraph2.Connect 进行智能连接
        var hr = _graph.Connect(output.NativePin, input.NativePin);
        HResultHelper.ThrowIfFailed(hr, "自动连接 {0} → {1}", output, input);
    }
}
