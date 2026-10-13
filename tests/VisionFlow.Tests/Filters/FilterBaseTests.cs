using DirectShowLib;
using VisionFlow.Filters;

namespace VisionFlow.Tests.Filters;

public class FilterBaseTests
{
    private static readonly Guid ColorSpaceConverterClsid =
        new Guid("1643E180-90F5-11CE-97D5-00AA0055595A");

    private static IBaseFilter CreateColorSpaceConverter()
    {
        var type = Type.GetTypeFromCLSID(ColorSpaceConverterClsid)!;
        return (IBaseFilter)Activator.CreateInstance(type)!;
    }

    [Fact]
    public void Constructor_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FilterBase(null!));
    }

    [Fact]
    public void GetPins_ForColorSpaceConverter_ReturnsInputAndOutput()
    {
        using var filter = new FilterBase(CreateColorSpaceConverter());

        var inputPins = filter.GetPins(PinDirection.Input);
        var outputPins = filter.GetPins(PinDirection.Output);

        Assert.Single(inputPins);
        Assert.Single(outputPins);
    }

    [Fact]
    public void GetFirstPin_ForInput_ReturnsInputPin()
    {
        using var filter = new FilterBase(CreateColorSpaceConverter());

        var pin = filter.GetFirstPin(PinDirection.Input);

        Assert.NotNull(pin);
        Assert.Equal(PinDirection.Input, pin!.Direction);
    }

    [Fact]
    public void GetFirstPin_ReturnsCachedInstance()
    {
        using var filter = new FilterBase(CreateColorSpaceConverter());

        var pin1 = filter.GetFirstPin(PinDirection.Input);
        var pin2 = filter.GetFirstPin(PinDirection.Input);

        Assert.Same(pin1, pin2);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var filter = new FilterBase(CreateColorSpaceConverter());

        var ex = Record.Exception(() =>
        {
            filter.Dispose();
            filter.Dispose();
        });

        Assert.Null(ex);
    }
}