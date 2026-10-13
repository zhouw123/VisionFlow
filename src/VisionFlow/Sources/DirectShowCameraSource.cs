using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Devices;
using VisionFlow.Filters;
using VisionFlow.Graph;
using VisionFlow.Infrastructure;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using WrapperFilterGraph = VisionFlow.Graph.FilterGraph;

namespace VisionFlow.Sources;

/// <summary>
/// 基于 DirectShow 的相机帧源。
/// 内部使用 FilterGraph + UsbCameraFilter + SampleGrabberFilter + NullRendererFilter，
/// 从 SampleGrabber 的回调里拿到原始字节流，推入分发队列。
/// 输出格式由相机决定（通常是 MJPG 或 YUY2），下游负责解码。
/// 用法：创建实例，可选 TrySetFormat 设置格式，加入 StreamingGraph 后 Start。
/// </summary>
public sealed class DirectShowCameraSource : SourceComponent
{
    private readonly DeviceInfo _deviceInfo;

    // DirectShow 资源
    private WrapperFilterGraph? _graph;
    private UsbCameraFilter? _camera;
    private SampleGrabberFilter? _grabber;
    private NullRendererFilter? _nullRenderer;

    // 请求格式（用户在 Start 之前设置）
    private Guid _requestedSubType = MediaSubType.MJPG;
    private int _requestedWidth = 640;
    private int _requestedHeight = 480;

    // 实际协商后的格式
    private Guid _actualSubType = MediaSubType.MJPG;
    private int _actualWidth;
    private int _actualHeight;

    /// <summary>
    /// 用设备信息创建相机源。
    /// </summary>
    public DirectShowCameraSource(DeviceInfo deviceInfo, int queueCapacity = 4)
        : base($"Camera:{deviceInfo?.Name ?? "unknown"}", queueCapacity)
    {
        _deviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
    }

    /// <summary>相机设备的名称。</summary>
    public string DeviceName => _deviceInfo.Name;

    /// <summary>实际协商后的媒体子类型（Start 之后有效）。</summary>
    public Guid ActualSubType => _actualSubType;

    /// <summary>实际宽度（Start 之后有效）。</summary>
    public int ActualWidth => _actualWidth;

    /// <summary>实际高度（Start 之后有效）。</summary>
    public int ActualHeight => _actualHeight;

    /// <summary>
    /// 请求相机输出格式。必须在 Start 之前调用。
    /// 实际能否设置成功取决于相机支持。
    /// </summary>
    public void TrySetFormat(Guid subType, int width, int height)
    {
        if (State != ComponentState.Created)
        {
            throw new InvalidOperationException("只能在 Start 之前设置格式。");
        }
        _requestedSubType = subType;
        _requestedWidth = width;
        _requestedHeight = height;
    }

    /// <summary>
    /// 打开相机、建图、连接、Run。
    /// </summary>
    protected override void OnStartSource()
    {
        // 1. 打开相机
        _camera = UsbCameraFilter.Create(_deviceInfo);

        // 2. 设置格式（尽力而为，失败则用默认）
        try
        {
            _camera.TrySetFormat(f =>
                f.SubType == _requestedSubType &&
                f.Width == _requestedWidth &&
                f.Height == _requestedHeight);
        }
        catch { /* 保持默认格式 */ }

        // 3. 创建图组件
        _graph = new WrapperFilterGraph();
        _grabber = new SampleGrabberFilter();
        _nullRenderer = new NullRendererFilter();

        // 4. 配置 SampleGrabber 的媒体类型
        var mediaType = BuildMediaType(_requestedSubType, _requestedWidth, _requestedHeight);
        try { _grabber.SetMediaType(mediaType); }
        finally { MediaTypeHelper.FreeMediaType(mediaType); }

        // 5. 订阅帧事件
        _grabber.FrameReceived += OnFrameReceived;

        // 6. 加入图
        _graph.AddFilter(_camera);
        _graph.AddFilter(_grabber);
        _graph.AddFilter(_nullRenderer);

        // 7. 连接
        var camOut = _camera.OutputPin
            ?? throw new InvalidOperationException("相机没有输出 Pin。");
        var grabIn = _grabber.InputPin
            ?? throw new InvalidOperationException("SampleGrabber 没有输入 Pin。");
        var grabOut = _grabber.OutputPin
            ?? throw new InvalidOperationException("SampleGrabber 没有输出 Pin。");
        var nullIn = _nullRenderer.InputPin
            ?? throw new InvalidOperationException("NullRenderer 没有输入 Pin。");

        _graph.Connect(camOut, grabIn);
        _graph.ConnectAuto(grabOut, nullIn);

        // 8. 使用请求的格式作为实际格式。
        // 原因：GetConnectedMediaType 返回的 subType 在部分 fork 上不可靠，
        // 而用户通过 TrySetFormat 请求的格式 + SampleGrabber 的 SetMediaType
        // 已经和上游协商成功，实际格式就是请求的格式。
        _actualSubType = _requestedSubType;
        _actualWidth = _requestedWidth;
        _actualHeight = _requestedHeight;

        // 9. 启动图
        _graph.Run();
    }

    /// <summary>
    /// 停止图并释放资源。
    /// </summary>
    protected override void OnStopSource()
    {
        if (_grabber is not null)
        {
            try { _grabber.FrameReceived -= OnFrameReceived; } catch { }
        }

        if (_graph is not null)
        {
            try { _graph.Stop(); } catch { }
            try { _graph.Dispose(); } catch { }
            _graph = null;
        }

        if (_camera is not null) { try { _camera.Dispose(); } catch { } _camera = null; }
        if (_grabber is not null) { try { _grabber.Dispose(); } catch { } _grabber = null; }
        if (_nullRenderer is not null) { try { _nullRenderer.Dispose(); } catch { } _nullRenderer = null; }
    }

    /// <summary>
    /// SampleGrabber 的帧回调。在 DirectShow 流线程上执行，不能阻塞。
    /// 拷贝字节到新数组，构造 FrameData 推入分发队列。
    /// </summary>
    private void OnFrameReceived(object? sender, FrameEventArgs e)
    {
        try
        {
            if (e.BufferPtr == IntPtr.Zero || e.BufferLength <= 0) return;

            var data = new byte[e.BufferLength];
            Marshal.Copy(e.BufferPtr, data, 0, e.BufferLength);

            var frame = new FrameData(
                e.Id,
                _actualWidth,
                _actualHeight,
                _actualSubType,
                data);

            RaiseFrameProduced(frame);
        }
        catch
        {
            // 流线程上的异常不能向外传播
        }
    }

    /// <summary>
    /// 弹出相机的原生属性页（由驱动提供），用于调整亮度、对比度等。
    /// 必须在 Start 之后调用（相机已打开）。
    /// </summary>
    /// <param name="ownerHwnd">所有者窗口句柄。</param>
    /// <returns>是否成功弹出。</returns>
    public bool ShowPropertyPage(IntPtr ownerHwnd)
    {
        if (_camera is null) return false;

        try
        {
            return Devices.PropertyPage.Show(_camera.NativeFilter, ownerHwnd, "相机属性");
        }
        catch
        {
            return false;
        }
    }

    private static AMMediaType BuildMediaType(Guid subType, int width, int height)
    {
        bool isMjpg = subType == MediaSubType.MJPG;
        int bitCount = subType == MediaSubType.YUY2 ? 16 : 24;
        int bytesPerPixel = bitCount / 8;

        var bmi = new BitmapInfoHeader();
        bmi.Size = Marshal.SizeOf<BitmapInfoHeader>();
        bmi.Width = width;
        bmi.Height = height;
        bmi.Planes = 1;
        bmi.BitCount = (short)bitCount;
        bmi.Compression = isMjpg ? 0x47504A4D : 0;

        var vih = new VideoInfoHeader();
        vih.AvgTimePerFrame = 333333;
        vih.BmiHeader = bmi;

        int size = Marshal.SizeOf<VideoInfoHeader>();
        IntPtr ptr = Marshal.AllocCoTaskMem(size);
        Marshal.StructureToPtr(vih, ptr, false);

        return new AMMediaType
        {
            majorType = MediaType.Video,
            subType = subType,
            formatType = DirectShowLib.FormatType.VideoInfo,
            formatPtr = ptr,
            fixedSizeSamples = !isMjpg,
            temporalCompression = false,
            sampleSize = isMjpg ? 0 : width * height * bytesPerPixel,
        };
    }
}