namespace VisionFlow.Transforms;

/// <summary>
/// CannyTransform 输出的结构化分析结果。
/// 通过 PipelineContext.Set 发送到 UI 层。
/// </summary>
public sealed class EdgeStatistics
{
    /// <summary>帧号，与 FrameId.FrameNumber 一致。</summary>
    public long FrameNumber { get; set; }

    /// <summary>检测到的边缘像素总数。</summary>
    public int EdgePixelCount { get; set; }

    /// <summary>本帧处理耗时（毫秒）。</summary>
    public double ProcessingMs { get; set; }

    /// <summary>是否有 ROI 生效。</summary>
    public bool RoiActive { get; set; }

    /// <summary>ROI 起点 X。</summary>
    public int RoiX { get; set; }

    /// <summary>ROI 起点 Y。</summary>
    public int RoiY { get; set; }

    /// <summary>ROI 宽度。</summary>
    public int RoiWidth { get; set; }

    /// <summary>ROI 高度。</summary>
    public int RoiHeight { get; set; }
}