using VisionFlow.Infrastructure;
using System.Runtime.InteropServices;

namespace VisionFlow.Tests.Infrastructure;

public class HResultHelperTests
{
    [Fact]
    public void IsSuccess_ForZero_ReturnsTrue()
    {
        Assert.True(HResultHelper.IsSuccess(0));
    }

    [Fact]
    public void IsSuccess_ForPositiveValue_ReturnsTrue()
    {
        // S_FALSE = 1，是成功码
        Assert.True(HResultHelper.IsSuccess(1));
    }

    [Fact]
    public void IsSuccess_ForNegativeValue_ReturnsFalse()
    {
        Assert.False(HResultHelper.IsSuccess(unchecked((int)0x80004005)));
    }

    [Fact]
    public void IsFailure_ForNegativeValue_ReturnsTrue()
    {
        Assert.True(HResultHelper.IsFailure(unchecked((int)0x80004005)));
    }

    [Fact]
    public void ThrowIfFailed_ForSuccess_DoesNotThrow()
    {
        var ex = Record.Exception(() => HResultHelper.ThrowIfFailed(0, "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void ThrowIfFailed_ForSFALSE_DoesNotThrow()
    {
        // 关键：S_FALSE 是成功码，不应抛异常
        var ex = Record.Exception(() => HResultHelper.ThrowIfFailed(1, "test"));
        Assert.Null(ex);
    }

    [Fact]
    public void ThrowIfFailed_ForFailure_ThrowsCOMException()
    {
        var hr = unchecked((int)0x80004005); // E_FAIL
        var ex = Assert.Throws<COMException>(
            () => HResultHelper.ThrowIfFailed(hr, "打开设备"));
        Assert.Equal(hr, ex.HResult);
        Assert.Contains("打开设备", ex.Message);
    }

    [Fact]
    public void ThrowIfFailed_WithFormat_IncludesFormattedMessage()
    {
        var hr = unchecked((int)0x80004005);
        var ex = Assert.Throws<COMException>(
            () => HResultHelper.ThrowIfFailed(hr, "连接 Pin {0} 到 {1}", "输出", "输入"));
        Assert.Contains("连接 Pin 输出 到 输入", ex.Message);
    }

    [Fact]
    public void GetErrorMessage_ForKnownHResult_ReturnsNonEmpty()
    {
        var hr = unchecked((int)0x80004005);
        var message = HResultHelper.GetErrorMessage(hr);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }
}