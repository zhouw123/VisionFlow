using System.Diagnostics;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Infrastructure;
using VisionFlow.Streaming;

namespace VisionFlow.Tests.Streaming;

public class ErrorEventTests
{
    private static FrameId MakeId(long n)
        => new FrameId(n * 0.1, n, DateTime.Now);

    private static FrameData MakeFrame(long n)
        => new FrameData(MakeId(n), 4, 4, MediaSubType.RGB24, new byte[48]);

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            Thread.Sleep(10);
        if (!condition()) throw new TimeoutException("等待条件超时。");
    }

    private sealed class ThrowingTransform : TransformComponent
    {
        public ThrowingTransform() : base("Thrower") { }

        protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
        {
            throw new InvalidOperationException("intentional test error");
        }
    }

    private sealed class ThrowingSink : SinkComponent
    {
        public ThrowingSink() : base("ThrowSink") { }

        protected override void OnFrame(FrameData frame, PipelineContext context)
        {
            throw new InvalidOperationException("sink test error");
        }
    }

    private sealed class OneShotSource : SourceComponent
    {
        public OneShotSource() : base("OneShot") { }
        protected override void OnStartSource()
        {
            RaiseFrameProduced(MakeFrame(1));
        }
        protected override void OnStopSource() { }
    }

    private sealed class QuietSink : SinkComponent
    {
        public QuietSink() : base("Quiet") { }
        protected override void OnFrame(FrameData frame, PipelineContext context) { }
    }

    [Fact]
    public void Transform_Error_RaisesErrorOccurred()
    {
        var source = new OneShotSource();
        var throwing = new ThrowingTransform();
        var sink = new QuietSink();
        source.LinkTo(throwing).LinkTo(sink);

        using var graph = StreamingGraph.From(source);

        ComponentErrorEventArgs? captured = null;
        throwing.ErrorOccurred += (s, e) => captured = e;

        graph.Start();

        WaitUntil(() => captured is not null, 3000);
        graph.Stop();

        Assert.NotNull(captured);
        Assert.Equal("ProcessFrame", captured!.Context);
        Assert.IsType<InvalidOperationException>(captured.Exception);
        Assert.Contains("intentional test error", captured.Exception.Message);
    }

    [Fact]
    public void Sink_Error_RaisesErrorOccurred()
    {
        var source = new OneShotSource();
        var throwing = new ThrowingSink();
        source.LinkTo(throwing);

        using var graph = StreamingGraph.From(source);

        ComponentErrorEventArgs? captured = null;
        throwing.ErrorOccurred += (s, e) => captured = e;

        graph.Start();

        WaitUntil(() => captured is not null, 3000);
        graph.Stop();

        Assert.NotNull(captured);
        Assert.Equal("OnFrame", captured!.Context);
    }

    [Fact]
    public void NoError_NoEvent()
    {
        var source = new OneShotSource();
        var sink = new QuietSink();
        source.LinkTo(sink);

        using var graph = StreamingGraph.From(source);

        int errorCount = 0;
        sink.ErrorOccurred += (s, e) => Interlocked.Increment(ref errorCount);

        graph.Start();
        Thread.Sleep(300);
        graph.Stop();

        Assert.Equal(0, errorCount);
    }
}