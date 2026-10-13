using System.Runtime.InteropServices;

namespace VisionFlow.Infrastructure;

/// <summary>
/// DirectShow HRESULT 处理的统一入口。
/// 区分成功码与失败码，失败时抛出 COMException。
/// </summary>
public static class HResultHelper
{
    /// <summary>
    /// 判断 HRESULT 是否表示成功。成功包括 S_OK 及各种 S_ 开头的附加信息码。
    /// </summary>
    public static bool IsSuccess(int hr) => hr >= 0;

    /// <summary>
    /// 判断 HRESULT 是否表示失败。
    /// </summary>
    public static bool IsFailure(int hr) => hr < 0;

    /// <summary>
    /// 如果 HRESULT 表示失败，抛出 COMException。
    /// </summary>
    /// <param name="hr">要检查的 HRESULT。</param>
    /// <param name="operation">操作描述，用于异常信息。</param>
    public static void ThrowIfFailed(int hr, string operation)
    {
        if (IsFailure(hr))
        {
            throw new COMException(
                $"DirectShow 操作失败: {operation} (HRESULT=0x{hr:X8})",
                hr);
        }
    }

    /// <summary>
    /// 如果 HRESULT 表示失败，抛出 COMException，支持格式化描述。
    /// </summary>
    public static void ThrowIfFailed(int hr, string format, params object[] args)
    {
        if (IsFailure(hr))
        {
            var operation = string.Format(format, args);
            throw new COMException(
                $"DirectShow 操作失败: {operation} (HRESULT=0x{hr:X8})",
                hr);
        }
    }

    /// <summary>
    /// 把 HRESULT 转换为可读的错误描述。用于日志或诊断输出。
    /// </summary>
    public static string GetErrorMessage(int hr)
    {
        try
        {
            return Marshal.GetExceptionForHR(hr)?.Message
                   ?? $"未知 HRESULT: 0x{hr:X8}";
        }
        catch
        {
            return $"未知 HRESULT: 0x{hr:X8}";
        }
    }
}