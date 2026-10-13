using DirectShowLib;
using WrapperVideoRenderer = VisionFlow.Filters.VideoRenderer;

namespace VisionFlow.Tests.Filters;

public class VideoRendererTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            using var renderer = new WrapperVideoRenderer();
        });

        Assert.Null(ex);
    }

    [Fact]
    public void InputPin_IsNotNull()
    {
        using var renderer = new WrapperVideoRenderer();

        Assert.NotNull(renderer.InputPin);
        Assert.Equal(PinDirection.Input, renderer.InputPin!.Direction);
    }
}