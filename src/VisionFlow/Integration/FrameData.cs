using VisionFlow.Infrastructure;

namespace VisionFlow.Integration;

/// <summary>
/// 流水线中流动的数据单元。携带帧身份、尺寸、格式和原始字节。
/// 每一级可以改变尺寸、格式、甚至内容，但必须保留 Id。
/// </summary>
public sealed class FrameData
{
    /// <summary>
    /// 构造一个 FrameData。
    /// </summary>
    /// <param name="id">帧身份标识。</param>
    /// <param name="width">宽度（像素）。</param>
    /// <param name="height">高度（像素）。</param>
    /// <param name="subType">媒体子类型（MediaSubType 常量）。</param>
    /// <param name="data">原始字节数据。</param>
    public FrameData(FrameId id, int width, int height, Guid subType, byte[] data)
    {
        Id = id;
        Width = width;
        Height = height;
        SubType = subType;
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>帧身份标识。贯穿整条流水线，不可变。</summary>
    public FrameId Id { get; }

    /// <summary>宽度（像素）。</summary>
    public int Width { get; }

    /// <summary>高度（像素）。</summary>
    public int Height { get; }

    /// <summary>媒体子类型（MediaSubType 常量）。</summary>
    public Guid SubType { get; }

    /// <summary>原始字节数据。</summary>
    public byte[] Data { get; }

    /// <summary>
    /// 基于当前 FrameData 创建一个"改变尺寸或格式"的新 FrameData。
    /// 保留原来的 Id，用于流水线中格式转换级。
    /// </summary>
    public FrameData WithData(int width, int height, Guid subType, byte[] data)
        => new FrameData(Id, width, height, subType, data);

    /// <inheritdoc />
    public override string ToString()
        => $"{Id} {Width}x{Height} ({Data.Length} bytes)";
}