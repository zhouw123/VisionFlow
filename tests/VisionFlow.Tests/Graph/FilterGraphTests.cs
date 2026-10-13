using DirectShowLib;
using VisionFlow.Filters;
using WrapperFilterGraph = VisionFlow.Graph.FilterGraph;

namespace VisionFlow.Tests.Graph;

public class FilterGraphTests
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
    public void Constructor_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            using var graph = new WrapperFilterGraph();
        });

        Assert.Null(ex);
    }

    [Fact]
    public void AddFilter_AddsToList()
    {
        using var graph = new WrapperFilterGraph();
        using var filter = CreateColorSpaceConverter();

        graph.AddFilter(filter);

        Assert.Single(graph.Filters);
        Assert.Contains(filter, graph.Filters);
    }

    [Fact]
    public void AddFilter_WithNull_Throws()
    {
        using var graph = new WrapperFilterGraph();
        Assert.Throws<ArgumentNullException>(() => graph.AddFilter(null!));
    }

    [Fact]
    public void AddFilter_SameFilterTwice_Throws()
    {
        using var graph = new WrapperFilterGraph();
        using var filter = CreateColorSpaceConverter();

        graph.AddFilter(filter);

        Assert.Throws<InvalidOperationException>(() => graph.AddFilter(filter));
    }

    [Fact]
    public void RemoveFilter_RemovesFromList()
    {
        using var graph = new WrapperFilterGraph();
        using var filter = CreateColorSpaceConverter();

        graph.AddFilter(filter);
        graph.RemoveFilter(filter);

        Assert.Empty(graph.Filters);
    }

    [Fact]
    public void RemoveFilter_NotInGraph_DoesNotThrow()
    {
        using var graph = new WrapperFilterGraph();
        using var filter = CreateColorSpaceConverter();

        var ex = Record.Exception(() => graph.RemoveFilter(filter));
        Assert.Null(ex);
    }

    [Fact]
    public void Connect_WithOutputAsInput_Throws()
    {
        using var graph = new WrapperFilterGraph();
        using var filter = CreateColorSpaceConverter();

        var outputPin = filter.GetFirstPin(PinDirection.Output)!;
        var inputPin = filter.GetFirstPin(PinDirection.Input)!;

        // 把输入 Pin 当输出传，应报错
        Assert.Throws<ArgumentException>(() => graph.Connect(inputPin, inputPin));
    }

    [Fact]
    public void Connect_WithInputAsOutput_Throws()
    {
        using var graph = new WrapperFilterGraph();
        using var filter = CreateColorSpaceConverter();

        var outputPin = filter.GetFirstPin(PinDirection.Output)!;
        var inputPin = filter.GetFirstPin(PinDirection.Input)!;

        // 把输出 Pin 当输入传，应报错
        Assert.Throws<ArgumentException>(() => graph.Connect(outputPin, outputPin));
    }

    [Fact]
    public void Stop_BeforeRun_DoesNotThrow()
    {
        using var graph = new WrapperFilterGraph();

        var ex = Record.Exception(() => graph.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var graph = new WrapperFilterGraph();

        var ex = Record.Exception(() =>
        {
            graph.Dispose();
            graph.Dispose();
        });

        Assert.Null(ex);
    }

    [Fact]
    public void AddFilter_AfterDispose_Throws()
    {
        var graph = new WrapperFilterGraph();
        using var filter = CreateColorSpaceConverter();

        graph.Dispose();

        Assert.Throws<ObjectDisposedException>(() => graph.AddFilter(filter));
    }
}