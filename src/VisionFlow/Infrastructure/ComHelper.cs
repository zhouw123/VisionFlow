using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace VisionFlow.Infrastructure;

/// <summary>
/// COM 对象生命周期管理的辅助方法。
/// 只提供“安全释放”，不判断对象是否仍被图持有。
/// </summary>
public static class ComHelper
{
    /// <summary>
    /// 安全释放一个 COM 对象。null 或非 COM 对象会被忽略。
    /// 释放过程中的异常会被吞掉，避免影响主流程。
    /// </summary>
    public static void SafeRelease(object? comObject, ILogger? logger = null)
    {
        if (comObject is null)
        {
            return;
        }

        if (!Marshal.IsComObject(comObject))
        {
            return;
        }

        try
        {
            Marshal.ReleaseComObject(comObject);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "释放 COM 对象失败: {Type}", comObject.GetType().Name);
        }
    }

    /// <summary>
    /// 批量安全释放多个 COM 对象。
    /// </summary>
    public static void SafeReleaseAll(ILogger? logger = null, params object?[] comObjects)
    {
        if (comObjects is null)
        {
            return;
        }

        foreach (var obj in comObjects)
        {
            SafeRelease(obj, logger);
        }
    }

    /// <summary>
    /// 判断对象是否为有效的 COM 对象。
    /// </summary>
    public static bool IsComObjectValid(object? comObject)
    {
        return comObject is not null && Marshal.IsComObject(comObject);
    }
}