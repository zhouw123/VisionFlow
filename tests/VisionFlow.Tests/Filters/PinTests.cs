using DirectShowLib;
using VisionFlow.Filters;

namespace VisionFlow.Tests.Filters;

public class PinTests
{
    private static readonly Guid ColorSpaceConverterClsid =
        new Guid("1643E180-90F5-11CE-97D5-00AA0055595A");

    private static FilterBase CreateColorSpaceConverter()
    {
        var type = Type.GetTypeFromCLSID(ColorSpaceConverterClsid)!;
        var filter = (IBaseFilter)Activator.CreateInstance(type)!;
        return new FilterBase(filter);
    }

    [Fact]
    public void Direction_ForInputPin_IsInput()
    {
        using var filter = CreateColorSpaceConverter();
        var pin = filter.GetFirstPin(PinDirection.Input);

        Assert.NotNull(pin);
        Assert.Equal(PinDirection.Input, pin!.Direction);
    }

    [Fact]
    public void Direction_ForOutputPin_IsOutput()
    {
        using var filter = CreateColorSpaceConverter();
        var pin = filter.GetFirstPin(PinDirection.Output);

        Assert.NotNull(pin);
        Assert.Equal(PinDirection.Output, pin!.Direction);
    }

    [Fact]
    public void IsConnected_ForUnconnectedPin_ReturnsFalse()
    {
        using var filter = CreateColorSpaceConverter();
        var pin = filter.GetFirstPin(PinDirection.Input);

        Assert.NotNull(pin);
        Assert.False(pin!.IsConnected);
    }

    [Fact]
    public void GetMediaTypes_ReturnsNonNullList()
    {
        // 注意：Color Space Converter 的 EnumMediaTypes 可能返回空列表，
        // 因为该过滤器的媒体类型是通过连接时动态协商的。
        // 本测试只验证 API 能正常调用并返回非 null 列表，不依赖具体元素数量。
        using var filter = CreateColorSpaceConverter();
        var pin = filter.GetFirstPin(PinDirection.Output);

        Assert.NotNull(pin);
        var mediaTypes = pin!.GetMediaTypes();

        Assert.NotNull(mediaTypes);
        // 列表中不应包含 null 元素
        Assert.DoesNotContain(mediaTypes, t => t is null);
    }

    [Fact]
    public void ConnectTo_WithNull_Throws()
    {
        using var filter = CreateColorSpaceConverter();
        var pin = filter.GetFirstPin(PinDirection.Output);

        Assert.NotNull(pin);
        Assert.Throws<ArgumentNullException>(() => pin!.ConnectTo(null!));
    }

    [Fact]
    public void ToString_ContainsDirection()
    {
        using var filter = CreateColorSpaceConverter();
        var pin = filter.GetFirstPin(PinDirection.Input);

        Assert.NotNull(pin);
        var str = pin!.ToString();
        Assert.Contains("Input", str);
    }
}