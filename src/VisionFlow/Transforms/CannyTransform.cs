using System.Diagnostics;
using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Transforms;

/// <summary>
/// Canny 边缘检测 + 在原图上叠加。
/// 支持 BGR 或 GRAY 输入，输出保持输入格式。
/// 支持 ROI（Region of Interest）：只在指定矩形内做 Canny。
/// 每次处理完成后，把边缘统计写入 PipelineContext。
/// </summary>
public sealed class CannyTransform : TransformComponent
{
    private volatile int _threshold1 = 50;
    private volatile int _threshold2 = 150;
    private volatile int _edgeColor = unchecked((int)0xFF00FF00);
    private volatile int _edgeThickness = 2;
    private volatile int _sleepMs = 0;

    private volatile int _roiX = 0;
    private volatile int _roiY = 0;
    private volatile int _roiWidth = 0;
    private volatile int _roiHeight = 0;

    /// <summary>创建 Canny 边缘检测。</summary>
    public CannyTransform(
        int queueCapacity = 2,
        BackpressurePolicy policy = BackpressurePolicy.DropOldest)
        : base("Canny", queueCapacity, policy)
    {
        RegisterParameter("Threshold1", "低阈值",
            () => _threshold1, v => _threshold1 = v,
            min: 0, max: 500, step: 1);

        RegisterParameter("Threshold2", "高阈值",
            () => _threshold2, v => _threshold2 = v,
            min: 0, max: 500, step: 1);

        RegisterParameter("EdgeColor", "边缘颜色",
            () => _edgeColor, v => _edgeColor = v,
            kind: ParameterKind.Color,
            description: "ARGB 颜色（GRAY 输入时只使用强度）");

        RegisterParameter("EdgeThickness", "边缘厚度",
            () => _edgeThickness, v => _edgeThickness = v,
            min: 1, max: 10, step: 1);

        RegisterParameter("SleepMs", "模拟耗时",
            () => _sleepMs, v => _sleepMs = v,
            min: 0, max: 100, step: 1, unit: "ms");

        RegisterParameter("RoiX", "ROI.X",
            () => _roiX, v => _roiX = v,
            min: 0, max: 10000, step: 1);

        RegisterParameter("RoiY", "ROI.Y",
            () => _roiY, v => _roiY = v,
            min: 0, max: 10000, step: 1);

        RegisterParameter("RoiWidth", "ROI.宽",
            () => _roiWidth, v => _roiWidth = v,
            min: 0, max: 10000, step: 1);

        RegisterParameter("RoiHeight", "ROI.高",
            () => _roiHeight, v => _roiHeight = v,
            min: 0, max: 10000, step: 1);
    }

    /// <summary>当前低阈值。</summary>
    public int Threshold1 => _threshold1;

    /// <summary>当前高阈值。</summary>
    public int Threshold2 => _threshold2;

    /// <summary>
    /// 设置 ROI。传 0 表示全图。
    /// 走 SetParameter 路径，触发 ParameterChanged 事件，让 UI 保持同步。
    /// </summary>
    public void SetRoi(int x, int y, int width, int height)
    {
        SetParameter("RoiX", x);
        SetParameter("RoiY", y);
        SetParameter("RoiWidth", width);
        SetParameter("RoiHeight", height);
    }

    /// <summary>清除 ROI，恢复全图处理。</summary>
    public void ClearRoi()
    {
        SetParameter("RoiX", 0);
        SetParameter("RoiY", 0);
        SetParameter("RoiWidth", 0);
        SetParameter("RoiHeight", 0);
    }

    /// <summary>处理一帧。</summary>
    protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
    {
        var sub = input.SubType;
        int channels = FrameFormats.GetChannelCount(sub);
        if (channels == 0) return input;

        int w = input.Width, h = input.Height;
        if (input.Data.Length < w * h * channels) return null;

        var sw = Stopwatch.StartNew();
        int sleep = _sleepMs;
        if (sleep > 0) Thread.Sleep(sleep);

        // ROI
        int roiX = _roiX, roiY = _roiY, roiW = _roiWidth, roiH = _roiHeight;
        bool roiActive = roiW > 0 && roiH > 0;
        if (roiActive)
        {
            roiX = Math.Max(0, Math.Min(w - 1, roiX));
            roiY = Math.Max(0, Math.Min(h - 1, roiY));
            roiW = Math.Min(w - roiX, roiW);
            roiH = Math.Min(h - roiY, roiH);
            if (roiW <= 0 || roiH <= 0) roiActive = false;
        }

        int argb = _edgeColor;
        byte b = (byte)((argb >> 0) & 0xFF);
        byte g = (byte)((argb >> 8) & 0xFF);
        byte r = (byte)((argb >> 16) & 0xFF);
        int thickness = _edgeThickness;

        int edgeCount = 0;
        byte[] outBytes;

        unsafe
        {
            fixed (byte* pSrc = input.Data)
            {
                var srcType = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
                using var src = Mat.FromPixelData(h, w, srcType, (IntPtr)pSrc);

                // 输出图：复制输入（在此基础上叠加边缘）
                using var dst = new Mat();
                src.CopyTo(dst);

                // 灰度图（用于 Canny）
                Mat gray;
                bool disposeGray = false;
                if (channels == 1) gray = src;
                else
                {
                    gray = new Mat();
                    Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                    disposeGray = true;
                }

                try
                {
                    if (roiActive)
                    {
                        var roiRect = new Rect(roiX, roiY, roiW, roiH);
                        using var grayRoi = new Mat(gray, roiRect);
                        using var dstRoi = new Mat(dst, roiRect);
                        edgeCount = ProcessRegion(grayRoi, dstRoi, channels, b, g, r, thickness);
                    }
                    else
                    {
                        edgeCount = ProcessRegion(gray, dst, channels, b, g, r, thickness);
                    }
                }
                finally
                {
                    if (disposeGray) gray.Dispose();
                }

                outBytes = new byte[dst.Total() * dst.ElemSize()];
                Marshal.Copy(dst.Data, outBytes, 0, outBytes.Length);
            }
        }

        sw.Stop();

        // 结构化数据
        var stats = new EdgeStatistics
        {
            FrameNumber = input.Id.FrameNumber,
            EdgePixelCount = edgeCount,
            ProcessingMs = sw.Elapsed.TotalMilliseconds,
            RoiActive = roiActive,
            RoiX = roiX,
            RoiY = roiY,
            RoiWidth = roiActive ? roiW : w,
            RoiHeight = roiActive ? roiH : h,
        };
        try { context.Set(stats, input.Id); } catch { }

        return input.WithData(w, h, sub, outBytes);
    }

    /// <summary>在灰度区域上做 Canny，把边缘叠加到颜色/灰度目标上。</summary>
    private int ProcessRegion(Mat grayRegion, Mat dstRegion, int channels,
        byte b, byte g, byte r, int thickness)
    {
        using var edges = new Mat();
        Cv2.Canny(grayRegion, edges, _threshold1, _threshold2);

        int count = Cv2.CountNonZero(edges);

        if (thickness > 1)
        {
            using var kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new Size(thickness, thickness));
            Cv2.Dilate(edges, edges, kernel);
        }

        Scalar color;
        if (channels == 1)
        {
            // 灰度：用亮度值
            byte v = (byte)((b + g + r) / 3);
            color = new Scalar(v);
        }
        else
        {
            color = new Scalar(b, g, r);
        }

        dstRegion.SetTo(color, edges);
        return count;
    }
}