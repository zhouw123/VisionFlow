namespace VisionFlow.Filters;

/// <summary>
/// 帧数据到达事件的参数。
/// 注意：Buffer 指针仅在事件回调期间有效，回调返回后不得再使用。
/// </summary>
public sealed class FrameEventArgs : EventArgs
{
    internal FrameEventArgs(IntPtr bufferPtr, int bufferLength, Infrastructure.FrameId id)
    {
        BufferPtr = bufferPtr;
        BufferLength = bufferLength;
        Id = id;
    }

    /// <summary>
    /// 帧数据缓冲区指针。可直接在此指针上原地修改数据，
    /// 修改结果会被下游过滤器接收到。
    /// 注意：回调返回后此指针失效，不得保存到外部使用。
    /// </summary>
    public IntPtr BufferPtr { get; }

    /// <summary>帧数据字节长度。</summary>
    public int BufferLength { get; }

    /// <summary>帧身份标识。包含帧号、采样时间和真实时间。</summary>
    public Infrastructure.FrameId Id { get; }

    /// <summary>采样时间（秒）。等价于 Id.SampleTime，保留用于向后兼容。</summary>
    public double SampleTime => Id.SampleTime;

    /// <summary>
    /// 把缓冲区数据复制到一个新的 byte 数组。用于异步场景。
    /// </summary>
    public byte[] CopyToArray()
    {
        var result = new byte[BufferLength];
        System.Runtime.InteropServices.Marshal.Copy(BufferPtr, result, 0, BufferLength);
        return result;
    }

    /// <summary>
    /// 把一个 byte 数组的数据写回缓冲区。用于异步处理后回写。
    /// </summary>
    public void WriteFromArray(byte[] data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (data.Length > BufferLength)
            throw new ArgumentException("数据长度超过缓冲区大小。", nameof(data));

        System.Runtime.InteropServices.Marshal.Copy(data, 0, BufferPtr, data.Length);
    }
}