using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Filters;

/// <summary>
/// SampleGrabber 过滤器。
/// 通过 FrameReceived 事件把每帧原始数据交给订阅者。
/// 事件在 DirectShow 流线程上触发，订阅者不能阻塞。
/// 上游输出什么格式就传什么，不做任何处理。
/// </summary>
public class SampleGrabberFilter : TransformFilterBase
{
    private readonly ISampleGrabber _grabber;
    private readonly SampleGrabberCallback _callback;
    private int _frameWidth;
    private int _frameHeight;
    private long _frameNumber;

    /// <summary>帧到达事件。在流线程上触发。</summary>
    public event EventHandler<FrameEventArgs>? FrameReceived;

    /// <summary>已接收的帧总数。</summary>
    public long TotalFrames => Interlocked.Read(ref _frameNumber);

    /// <summary>创建 SampleGrabber 实例。</summary>
    public SampleGrabberFilter() : base(CreateFilter(out var grabber))
    {
        _grabber = grabber;

        HResultHelper.ThrowIfFailed(
            _grabber.SetBufferSamples(false), "关闭内部缓冲");
        HResultHelper.ThrowIfFailed(
            _grabber.SetOneShot(false), "关闭单次模式");

        _callback = new SampleGrabberCallback(OnBufferCB);

        HResultHelper.ThrowIfFailed(
            _grabber.SetCallback(_callback, 1), "设置回调模式");
    }

    /// <summary>设置 SampleGrabber 的媒体类型。必须在连接之前调用。</summary>
    public void SetMediaType(AMMediaType mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        HResultHelper.ThrowIfFailed(
            _grabber.SetMediaType(mediaType), "设置 SampleGrabber 媒体类型");

        if (VideoInfoParser.TryParse(mediaType, out var info))
        {
            _frameWidth = info.Width;
            _frameHeight = info.Height;
        }
    }

    /// <summary>获取实际协商后的媒体类型。</summary>
    public AMMediaType? GetConnectedMediaType()
    {
        var mt = new AMMediaType();
        var hr = _grabber.GetConnectedMediaType(mt);
        if (HResultHelper.IsFailure(hr))
        {
            return null;
        }

        if (VideoInfoParser.TryParse(mt, out var info))
        {
            _frameWidth = info.Width;
            _frameHeight = info.Height;
        }

        return mt;
    }

    private int OnBufferCB(double sampleTime, IntPtr bufferPtr, int bufferLen)
    {
        try
        {
            if (bufferPtr == IntPtr.Zero || bufferLen <= 0)
            {
                return 0;
            }

            var frameId = new FrameId(
                sampleTime,
                Interlocked.Increment(ref _frameNumber),
                DateTime.Now);

            var handler = FrameReceived;
            if (handler is not null)
            {
                var args = new FrameEventArgs(bufferPtr, bufferLen, frameId);
                handler(this, args);
            }
        }
        catch
        {
            // 回调中的异常不能传播到 COM
        }

        return 0;
    }

    private static IBaseFilter CreateFilter(out ISampleGrabber grabber)
    {
        var type = Type.GetTypeFromCLSID(DirectShowClsIds.SampleGrabber)
            ?? throw new InvalidOperationException("无法获取 SampleGrabber 的 COM 类型。");

        var obj = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("无法创建 SampleGrabber 实例。");

        grabber = (ISampleGrabber)obj;
        return (IBaseFilter)obj;
    }

    [ComVisible(true)]
    private sealed class SampleGrabberCallback : ISampleGrabberCB
    {
        private readonly Func<double, IntPtr, int, int> _bufferCB;

        public SampleGrabberCallback(Func<double, IntPtr, int, int> bufferCB)
        {
            _bufferCB = bufferCB;
        }

        public int SampleCB(double sampleTime, IMediaSample sample) => 0;

        public int BufferCB(double sampleTime, IntPtr bufferPtr, int bufferLen)
            => _bufferCB(sampleTime, bufferPtr, bufferLen);
    }
}