using VisionFlow.Integration;
using VisionFlow.Infrastructure;

namespace VisionFlow.Tests.Integration;

public class FrameDataTests
{
    private static FrameId MakeId(long n = 1)
        => new FrameId(n * 0.1, n, new DateTime(2026, 1, 1).AddSeconds(n * 0.1));

    [Fact]
    public void Constructor_SetsProperties()
    {
        var id = MakeId(5);
        var data = new byte[100];
        var frame = new FrameData(id, 640, 480, Guid.Empty, data);

        Assert.Equal(id, frame.Id);
        Assert.Equal(640, frame.Width);
        Assert.Equal(480, frame.Height);
        Assert.Same(data, frame.Data);
    }

    [Fact]
    public void Constructor_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new FrameData(MakeId(), 640, 480, Guid.Empty, null!));
    }

    [Fact]
    public void WithData_PreservesId()
    {
        var id = MakeId(7);
        var frame = new FrameData(id, 640, 480, Guid.Empty, new byte[10]);

        var newFrame = frame.WithData(320, 240, Guid.Empty, new byte[5]);

        Assert.Equal(id, newFrame.Id);
        Assert.Equal(320, newFrame.Width);
        Assert.Equal(240, newFrame.Height);
    }
}