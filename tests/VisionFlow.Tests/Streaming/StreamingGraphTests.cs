using System.Diagnostics;
using VisionFlow.Integration;
using VisionFlow.Infrastructure;
using VisionFlow.Streaming;

namespace VisionFlow.Tests.Streaming;

public class StreamingGraphTests
{
    private static FrameId MakeId(long n)
        => new FrameId(n * 0.1, n, DateTime.Now);

    private static FrameData MakeFrame(long n, byte value = 0)
        => new FrameData(MakeId(n), 4, 4, Guid.Empty, new[] { value });

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            Thread.Sleep(10);
        if (!condition()) throw new TimeoutException("等待条件超时。");
    }

    private sealed class TestSource : SourceComponent
    {
        public int FrameCount { get; set; } = 10;
        public int IntervalMs { get; set; } = 5;
        private Thread? _emitThread;
        public TestSource() : base("TestSource") { }
        protected override void OnStartSource()
        {
            _emitThread = new Thread(() =>
            {
                for (int i = 1; i <= FrameCount; i++)
                {
                    RaiseFrameProduced(MakeFrame(i, (byte)i));
                    Thread.Sleep(IntervalMs);
                }
            }) { IsBackground = true };
            _emitThread.Start();
        }
        protected override void OnStopSource()
        {
            try { _emitThread?.Join(2000); } catch { }
        }
    }

    private sealed class AddTransform : TransformComponent
    {
        private readonly byte _delta;
        public AddTransform(byte delta)
            : base(null, 4, BackpressurePolicy.Block) { _delta = delta; }
        protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
        {
            var output = new byte[input.Data.Length];
            for (int i = 0; i < output.Length; i++)
                output[i] = (byte)(input.Data[i] + _delta);
            return input.WithData(input.Width, input.Height, input.SubType, output);
        }
    }

    private sealed class CollectSink : SinkComponent
    {
        public List<FrameData> Received { get; } = new();
        public CollectSink() : base("CollectSink", 16, BackpressurePolicy.Block) { }
        protected override void OnFrame(FrameData frame, PipelineContext context)
        {
            lock (Received) { Received.Add(frame); }
        }
    }

    [Fact]
    public void From_CollectsAllConnected()
    {
        var source = new TestSource();
        var t1 = new AddTransform(1);
        var t2 = new AddTransform(2);
        var sink = new CollectSink();

        source.LinkTo(t1).LinkTo(t2).LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        Assert.Equal(4, graph.ComponentCount);
    }

    [Fact]
    public void From_WithBranch_CollectsAllBranches()
    {
        var source = new TestSource();
        var t1 = new AddTransform(1);
        var sink1 = new CollectSink();
        var sink2 = new CollectSink();

        source.LinkTo(t1);
        t1.LinkTo(sink1);
        t1.LinkTo(sink2);

        using var graph = StreamingGraph.From(source);
        Assert.Equal(4, graph.ComponentCount);
    }

    [Fact]
    public void Start_InjectsContext()
    {
        var source = new TestSource();
        var sink = new CollectSink();
        source.LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        Assert.Null(graph.Context);

        graph.Start();
        Assert.NotNull(graph.Context);
        Assert.True(source.HasContext);
        Assert.True(sink.HasContext);
        Assert.Same(graph.Context, source.Context);
        Assert.Same(graph.Context, sink.Context);

        graph.Stop();
    }

    [Fact]
    public void EndToEnd_SourceToSink()
    {
        var source = new TestSource { FrameCount = 5 };
        var t1 = new AddTransform(1);
        var t2 = new AddTransform(2);
        var sink = new CollectSink();

        source.LinkTo(t1).LinkTo(t2).LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        graph.Start();

        WaitUntil(() => sink.ConsumedFrames >= 5, 5000);
        graph.Stop();

        lock (sink.Received)
        {
            Assert.True(sink.Received.Count >= 5);
            // 检查数据处理正确
            var first = sink.Received[0];
            Assert.Equal((byte)4, first.Data[0]); // 1 + 1 + 2 = 4
        }
    }

    [Fact]
    public void Branch_TwoSinks_BothReceive()
    {
        var source = new TestSource { FrameCount = 5 };
        var sink1 = new CollectSink();
        var sink2 = new CollectSink();

        source.LinkTo(sink1);
        source.LinkTo(sink2);

        using var graph = StreamingGraph.From(source);
        graph.Start();

        WaitUntil(() => sink1.ConsumedFrames >= 5 && sink2.ConsumedFrames >= 5, 5000);
        graph.Stop();

        Assert.True(sink1.ConsumedFrames >= 5);
        Assert.True(sink2.ConsumedFrames >= 5);
    }

    [Fact]
    public void Start_Twice_IsIdempotent()
    {
        var source = new TestSource();
        var sink = new CollectSink();
        source.LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        graph.Start();
        var ex = Record.Exception(() => graph.Start());
        graph.Stop();

        Assert.Null(ex);
    }

    [Fact]
    public void Stop_Twice_IsIdempotent()
    {
        var source = new TestSource();
        var sink = new CollectSink();
        source.LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        graph.Start();
        graph.Stop();

        var ex = Record.Exception(() => graph.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Start_EmptyGraph_Throws()
    {
        using var graph = new StreamingGraph();
        Assert.Throws<InvalidOperationException>(() => graph.Start());
    }

    [Fact]
    public void Add_AfterStart_Throws()
    {
        var source = new TestSource();
        var sink = new CollectSink();
        source.LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        graph.Start();

        Assert.Throws<InvalidOperationException>(
            () => graph.Add(new CollectSink()));
        graph.Stop();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var source = new TestSource();
        var sink = new CollectSink();
        source.LinkTo(sink);

        var graph = StreamingGraph.From(source);
        graph.Start();

        var ex = Record.Exception(() =>
        {
            graph.Dispose();
            graph.Dispose();
        });
        Assert.Null(ex);
    }
}