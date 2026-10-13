using VisionFlow.Integration;
using VisionFlow.Infrastructure;

namespace VisionFlow.Tests.Integration;

public class PipelineContextTests
{
    private static FrameId MakeId(long n = 1)
        => new FrameId(n * 0.1, n, DateTime.Now);

    private sealed class DetectionResult
    {
        public int Count { get; set; }
    }

    [Fact]
    public void Set_ThenGet_ReturnsSameValue()
    {
        using var ctx = new PipelineContext();
        var detection = new DetectionResult { Count = 3 };

        ctx.Set(detection, MakeId());

        Assert.Same(detection, ctx.Get<DetectionResult>());
    }

    [Fact]
    public void Set_SameTypeTwice_KeepsLatest()
    {
        using var ctx = new PipelineContext();

        var first = new DetectionResult { Count = 1 };
        var second = new DetectionResult { Count = 2 };

        ctx.Set(first, MakeId(1));
        ctx.Set(second, MakeId(2));

        Assert.Same(second, ctx.Get<DetectionResult>());
    }

    [Fact]
    public void Set_TriggersResultProduced()
    {
        using var ctx = new PipelineContext();
        PipelineResult? captured = null;
        ctx.ResultProduced += (s, e) => captured = e;

        var id = MakeId(42);
        var detection = new DetectionResult { Count = 7 };
        ctx.Set(detection, id);

        Assert.NotNull(captured);
        Assert.Equal(id, captured!.Id);
        Assert.Equal(nameof(DetectionResult), captured.Key);
        Assert.Same(detection, captured.Value);
    }

    [Fact]
    public void Get_UnsetType_ReturnsNull()
    {
        using var ctx = new PipelineContext();
        Assert.Null(ctx.Get<DetectionResult>());
    }

    [Fact]
    public void SetAndPersist_AddsToQueue()
    {
        using var ctx = new PipelineContext();
        var detection = new DetectionResult { Count = 5 };

        bool added = ctx.SetAndPersist(detection, MakeId());

        Assert.True(added);
        Assert.Single(ctx.PersistentQueue);
    }

    [Fact]
    public void PersistentQueue_Full_ReturnsFalse()
    {
        using var ctx = new PipelineContext(persistentQueueCapacity: 2);

        Assert.True(ctx.SetAndPersist(new DetectionResult { Count = 1 }, MakeId(1)));
        Assert.True(ctx.SetAndPersist(new DetectionResult { Count = 2 }, MakeId(2)));
        Assert.False(ctx.SetAndPersist(new DetectionResult { Count = 3 }, MakeId(3)));
    }

    [Fact]
    public void Dispose_ThenSet_Throws()
    {
        var ctx = new PipelineContext();
        ctx.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => ctx.Set(new DetectionResult(), MakeId()));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var ctx = new PipelineContext();
        var ex = Record.Exception(() =>
        {
            ctx.Dispose();
            ctx.Dispose();
        });
        Assert.Null(ex);
    }
}