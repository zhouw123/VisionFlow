using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using OpenCvSharp;

namespace VisionFlow.Sinks;

/// <summary>
/// 抓图输出。通过 RequestSnapshot 触发，把下一帧保存为图片文件。
/// 只在请求时保存，避免持续写盘。
/// 输入格式：BGR（MediaSubType.RGB24）。
/// 参数：输出目录、文件名前缀、图片格式。
/// </summary>
public sealed class SnapshotSink : SinkComponent
{
    private readonly object _snapshotLock = new();
    private readonly object _paramLock = new();
    private volatile bool _requested;
    private string _outputDirectory;
    private string _prefix = "snapshot";
    private string _format = "png";
    private long _savedCount;

    /// <summary>
    /// 创建抓图输出。
    /// </summary>
    public SnapshotSink(
        string? outputDirectory = null,
        int queueCapacity = 4,
        BackpressurePolicy policy = BackpressurePolicy.Block)
        : base("SnapshotSink", queueCapacity, policy)
    {
        _outputDirectory = outputDirectory
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Snapshots");

        RegisterParameter(
            "OutputDirectory", "输出目录",
            () => { lock (_paramLock) return _outputDirectory; },
            v => { lock (_paramLock) _outputDirectory = v; },
            description: "图片保存目录。不存在时自动创建。");

        RegisterParameter(
            "Prefix", "文件名前缀",
            () => { lock (_paramLock) return _prefix; },
            v => { lock (_paramLock) _prefix = v; },
            description: "文件名格式：前缀_时间戳_序号.扩展名");

        RegisterParameter(
            "Format", "图片格式",
            () => { lock (_paramLock) return _format; },
            v => { lock (_paramLock) _format = v; },
            kind: ParameterKind.Enum,
            enumOptions: new[]
            {
                new EnumOption("png", "PNG"),
                new EnumOption("jpg", "JPEG"),
                new EnumOption("bmp", "BMP"),
            });
    }

    /// <summary>已保存的图片总数。</summary>
    public long SavedCount => Interlocked.Read(ref _savedCount);

    /// <summary>最近一次保存的完整路径。未保存过时为 null。</summary>
    public string? LastSavedPath { get; private set; }

    /// <summary>图片保存成功时触发。参数为完整路径。</summary>
    public event EventHandler<string>? SnapshotSaved;

    /// <summary>
    /// 请求抓取下一帧。线程安全，可在任意线程调用。
    /// 只影响"下一帧"，不会连续保存。
    /// </summary>
    public void RequestSnapshot()
    {
        _requested = true;
    }

    /// <summary>
    /// 处理一帧。仅当收到请求时保存。
    /// </summary>
    protected override void OnFrame(FrameData frame, PipelineContext context)
    {
        if (!_requested) return;

        // 只处理 BGR
        if (frame.SubType != MediaSubType.RGB24) return;

        // 加锁并清除请求（避免重复触发）
        lock (_snapshotLock)
        {
            if (!_requested) return;
            _requested = false;
        }

        string dir, prefix, format;
        lock (_paramLock)
        {
            dir = _outputDirectory;
            prefix = _prefix;
            format = _format;
        }

        try
        {
            Directory.CreateDirectory(dir);

            var fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Interlocked.Increment(ref _savedCount):D4}.{format}";
            var fullPath = Path.Combine(dir, fileName);

            using var mat = new Mat(frame.Height, frame.Width, MatType.CV_8UC3);
            Marshal.Copy(frame.Data, 0, mat.Data, frame.Data.Length);
            Cv2.ImWrite(fullPath, mat);

            LastSavedPath = fullPath;
            SnapshotSaved?.Invoke(this, fullPath);
        }
        catch
        {
            // 单帧保存失败不影响流水线
        }
    }
}