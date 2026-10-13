using VisionFlow.Infrastructure;

namespace VisionFlow.Tests.Infrastructure;

public class ComHelperTests
{
    [Fact]
    public void SafeRelease_ForNull_DoesNotThrow()
    {
        var ex = Record.Exception(() => ComHelper.SafeRelease(null));
        Assert.Null(ex);
    }

    [Fact]
    public void SafeRelease_ForNonComObject_DoesNotThrow()
    {
        var ex = Record.Exception(() => ComHelper.SafeRelease("not a com object"));
        Assert.Null(ex);
    }

    [Fact]
    public void SafeReleaseAll_ForNullArray_DoesNotThrow()
    {
        var ex = Record.Exception(() => ComHelper.SafeReleaseAll(null));
        Assert.Null(ex);
    }

    [Fact]
    public void SafeReleaseAll_ForMixedArray_DoesNotThrow()
    {
        var ex = Record.Exception(() => ComHelper.SafeReleaseAll(null, "string", 42, null));
        Assert.Null(ex);
    }

    [Fact]
    public void IsComObjectValid_ForNull_ReturnsFalse()
    {
        Assert.False(ComHelper.IsComObjectValid(null));
    }

    [Fact]
    public void IsComObjectValid_ForNonComObject_ReturnsFalse()
    {
        Assert.False(ComHelper.IsComObjectValid("not a com object"));
    }
}