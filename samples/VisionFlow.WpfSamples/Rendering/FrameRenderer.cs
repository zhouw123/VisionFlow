using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DirectShowLib;
using VisionFlow.Integration;
using CvMat = OpenCvSharp.Mat;
using CvMatType = OpenCvSharp.MatType;
using Cv = OpenCvSharp.Cv2;
using CvColor = OpenCvSharp.ColorConversionCodes;

namespace VisionFlow.WpfSamples.Rendering;

/// <summary>
/// Renders a stream of FrameData to a WPF Image via a supplied callback.
/// 
/// Responsibilities:
///   - Convert each FrameData to a frozen BitmapSource
///   - Throttle UI updates (only one pending update at a time)
///   - Track frame count and video dimensions
/// 
/// The callback runs on the UI thread (Dispatcher).
/// </summary>
public sealed class FrameRenderer
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<BitmapSource> _onFrameReady;

    private int _updatePending;    // 1 = a UI update is in flight
    private long _frameCount;
    private int _videoWidth;
    private int _videoHeight;

    public FrameRenderer(Dispatcher dispatcher, Action<BitmapSource> onFrameReady)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _onFrameReady = onFrameReady ?? throw new ArgumentNullException(nameof(onFrameReady));
    }

    /// <summary>Number of frames that have been rendered (incremented on UI dispatch).</summary>
    public long FrameCount => Interlocked.Read(ref _frameCount);

    public int VideoWidth => _videoWidth;
    public int VideoHeight => _videoHeight;

    /// <summary>
    /// Convert and display a frame. Safe to call from any thread.
    /// If a UI update is already pending, this frame is dropped (throttling).
    /// </summary>
    public void Render(FrameData frame)
    {
        BitmapSource? bitmap;
        try { bitmap = ToBitmapSource(frame); }
        catch { return; }
        if (bitmap is null) return;

        // Try to take the "pending" slot
        if (Interlocked.CompareExchange(ref _updatePending, 1, 0) != 0)
        {
            // A previous UI update has not completed yet; skip this frame
            return;
        }

        int w = frame.Width;
        int h = frame.Height;
        _videoWidth = w;
        _videoHeight = h;

        _dispatcher.InvokeAsync(() =>
        {
            try
            {
                _onFrameReady(bitmap);
                Interlocked.Increment(ref _frameCount);
            }
            finally
            {
                Interlocked.Exchange(ref _updatePending, 0);
            }
        }, DispatcherPriority.Render);
    }

    // ---------- Format conversion ----------

    /// <summary>
    /// Convert a FrameData to a frozen BitmapSource in BGR24 format.
    /// Returns null if the frame's format is unsupported or empty.
    /// </summary>
    public static BitmapSource? ToBitmapSource(FrameData frame)
    {
        int w = frame.Width, h = frame.Height;
        if (w <= 0 || h <= 0) return null;

        int channels = FrameFormats.GetChannelCount(frame.SubType);
        if (channels == 0) return null;
        if (frame.Data.Length < w * h * channels) return null;

        using var bgr = new CvMat();
        unsafe
        {
            fixed (byte* pSrc = frame.Data)
            {
                var srcType = channels == 1 ? CvMatType.CV_8UC1 : CvMatType.CV_8UC3;
                using var src = CvMat.FromPixelData(h, w, srcType, (IntPtr)pSrc);

                if (frame.SubType == FrameFormats.Gray8)
                    Cv.CvtColor(src, bgr, CvColor.GRAY2BGR);
                else if (frame.SubType == FrameFormats.Rgb8)
                    Cv.CvtColor(src, bgr, CvColor.RGB2BGR);
                else if (frame.SubType == FrameFormats.Hsv8)
                    Cv.CvtColor(src, bgr, CvColor.HSV2BGR);
                else if (frame.SubType == FrameFormats.YCrCb8)
                    Cv.CvtColor(src, bgr, CvColor.YCrCb2BGR);
                else if (frame.SubType == FrameFormats.Yuv8)
                    Cv.CvtColor(src, bgr, CvColor.YUV2BGR);
                else
                    src.CopyTo(bgr);
            }
        }

        if (bgr.Empty()) return null;

        int stride = w * 3;
        var bmp = BitmapSource.Create(
            w, h, 96, 96,
            PixelFormats.Bgr24, null,
            bgr.Data, stride * h, stride);
        bmp.Freeze();
        return bmp;
    }
}