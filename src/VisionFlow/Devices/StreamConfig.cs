using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Infrastructure;

namespace VisionFlow.Devices;

/// <summary>
/// 封装 IAMStreamConfig，用于枚举和设置捕获 Pin 的视频格式。
/// </summary>
public sealed class StreamConfig
{
    private readonly IAMStreamConfig _config;

    private StreamConfig(IAMStreamConfig config)
    {
        _config = config;
    }

    internal static StreamConfig? TryCreate(IPin pin)
    {
        try
        {
            if (pin is IAMStreamConfig config)
            {
                return new StreamConfig(config);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 枚举此 Pin 支持的所有视频格式。
    /// </summary>
    public IReadOnlyList<VideoFormat> GetSupportedFormats()
    {
        var result = new List<VideoFormat>();

        var hr = _config.GetNumberOfCapabilities(out var count, out var size);
        if (HResultHelper.IsFailure(hr))
        {
            return result;
        }

        for (int i = 0; i < count; i++)
        {
            IntPtr capsPtr = Marshal.AllocCoTaskMem(size);
            try
            {
                hr = _config.GetStreamCaps(i, out var mediaType, capsPtr);
                if (HResultHelper.IsFailure(hr) || mediaType is null)
                {
                    continue;
                }

                try
                {
                    if (VideoInfoParser.TryParse(mediaType, out var info))
                    {
                        result.Add(new VideoFormat(
                            info.Width, info.Height, info.FrameRate, mediaType.subType));
                    }
                }
                finally
                {
                    MediaTypeHelper.FreeMediaType(mediaType);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(capsPtr);
            }
        }

        // UVC 相机常返回重复的格式记录，按 (宽度, 高度, 像素格式, 帧率) 去重
        return result
            .GroupBy(f => (f.Width, f.Height, f.SubType, Math.Round(f.FrameRate, 1)))
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// 获取当前配置的媒体类型。返回的 AMMediaType 由调用者负责通过
    /// MediaTypeHelper.FreeMediaType 释放。
    /// </summary>
    public AMMediaType? GetCurrentFormatMediaType()
    {
        var hr = _config.GetFormat(out var mediaType);
        if (HResultHelper.IsFailure(hr) || mediaType is null)
        {
            return null;
        }
        return mediaType;
    }

    /// <summary>
    /// 设置 Pin 的输出格式。
    /// </summary>
    public void SetFormat(VideoFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        var target = FindMatchingMediaType(format);
        if (target is null)
        {
            throw new InvalidOperationException($"该设备不支持格式 {format}。");
        }

        try
        {
            var hr = _config.SetFormat(target);
            HResultHelper.ThrowIfFailed(hr, "设置视频格式 {0}", format);
        }
        finally
        {
            MediaTypeHelper.FreeMediaType(target);
        }
    }

    private AMMediaType? FindMatchingMediaType(VideoFormat format)
    {
        var hr = _config.GetNumberOfCapabilities(out var count, out var size);
        if (HResultHelper.IsFailure(hr))
        {
            return null;
        }

        for (int i = 0; i < count; i++)
        {
            IntPtr capsPtr = Marshal.AllocCoTaskMem(size);
            try
            {
                hr = _config.GetStreamCaps(i, out var mediaType, capsPtr);
                if (HResultHelper.IsFailure(hr) || mediaType is null)
                {
                    continue;
                }

                if (Matches(mediaType, format))
                {
                    return mediaType;
                }

                MediaTypeHelper.FreeMediaType(mediaType);
            }
            finally
            {
                Marshal.FreeCoTaskMem(capsPtr);
            }
        }

        return null;
    }

    private static bool Matches(AMMediaType mediaType, VideoFormat format)
    {
        if (mediaType.subType != format.SubType)
        {
            return false;
        }

        if (!VideoInfoParser.TryParse(mediaType, out var info))
        {
            return false;
        }

        if (info.Width != format.Width || info.Height != format.Height)
        {
            return false;
        }

        if (format.FrameRate > 0 && info.FrameRate > 0)
        {
            if (Math.Abs(info.FrameRate - format.FrameRate) > 0.5)
            {
                return false;
            }
        }

        return true;
    }
}