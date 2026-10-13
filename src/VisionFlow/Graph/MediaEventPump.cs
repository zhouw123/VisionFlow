using DirectShowLib;

namespace VisionFlow.Graph;

/// <summary>
/// 从 IMediaEventEx 队列中取出事件并分发到 .NET 事件。
/// 使用后台线程轮询，线程随 Dispose 停止。
/// </summary>
public sealed class MediaEventPump : IDisposable
{
    private readonly IMediaEventEx _eventEx;
    private readonly Thread _thread;
    private volatile bool _running;
    private bool _disposed;

    /// <summary>
    /// 图播放完成时触发。
    /// </summary>
    public event EventHandler? Complete;

    /// <summary>
    /// 图运行出错时触发，参数为描述异常的 Exception。
    /// </summary>
    public event EventHandler<Exception>? ErrorAbort;

    /// <summary>
    /// 设备丢失时触发（如相机被拔出）。
    /// </summary>
    public event EventHandler? DeviceLost;

    internal MediaEventPump(IMediaEventEx eventEx)
    {
        _eventEx = eventEx ?? throw new ArgumentNullException(nameof(eventEx));
        _running = true;
        _thread = new Thread(PumpLoop)
        {
            IsBackground = true,
            Name = "DirectShow.MediaEventPump"
        };
        _thread.Start();
    }

    private void PumpLoop()
    {
        while (_running)
        {
            try
            {
                // 超时 100ms，便于及时响应停止信号
                var hr = _eventEx.GetEvent(out var code, out var p1, out var p2, 100);

                if (hr < 0)
                {
                    // 超时或无事件，继续循环
                    continue;
                }

                try
                {
                    HandleEvent((EventCode)code, p1, p2);
                }
                finally
                {
                    _eventEx.FreeEventParams((EventCode)code, p1, p2);
                }
            }
            catch
            {
                // 避免后台线程因意外异常终止
            }
        }
    }

    private void HandleEvent(EventCode code, IntPtr p1, IntPtr p2)
    {
        switch (code)
        {
            case EventCode.Complete:
                Complete?.Invoke(this, EventArgs.Empty);
                break;

            case EventCode.ErrorAbort:
                ErrorAbort?.Invoke(this, new Exception(
                    $"过滤器图运行出错，HRESULT=0x{p1.ToInt32():X8}"));
                break;

            case EventCode.DeviceLost:
                DeviceLost?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>
    /// 停止事件泵线程。
    /// </summary>
    public void Dispose()

    {
        if (_disposed)
        {
            return;
        }

        _running = false;

        // 等待后台线程退出，最多 1 秒
        if (!_thread.Join(1000))
        {
            // 超时则放弃等待，线程是后台线程，不影响进程退出
        }

        _disposed = true;
    }
}