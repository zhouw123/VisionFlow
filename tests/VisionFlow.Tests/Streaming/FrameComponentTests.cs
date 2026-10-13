using System.Diagnostics;
using VisionFlow.Integration;
using VisionFlow.Infrastructure;
using VisionFlow.Streaming;

namespace VisionFlow.Tests.Streaming;

public class FrameComponentTests
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

    /// <summary>简单 Source：产生固定数量的帧。</summary>
    private sealed class TestSource : SourceComponent
    {
        public int FrameCount { get; set; } = 5;
        public int IntervalMs { get; set; } = 10;
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
            })
            { IsBackground = true };
            _emitThread.Start();
        }

        protected override void OnStopSource()
        {
            try { _emitThread?.Join(2000); } catch { }
        }
    }

    /// <summary>简单 Transform：字节值 +delta。</summary>
    private sealed class AddTransform : TransformComponent
    {
        private readonly byte _delta;
        public AddTransform(byte delta, int capacity = 4,
            BackpressurePolicy policy = BackpressurePolicy.Block)
            : base(null, capacity, policy) { _delta = delta; }

        protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
        {
            var output = new byte[input.Data.Length];
            for (int i = 0; i < output.Length; i++)
                output[i] = (byte)(input.Data[i] + _delta);
            return input.WithData(input.Width, input.Height, input.SubType, output);
        }
    }

    /// <summary>简单 Sink：记录所有帧。</summary>
    private sealed class CollectSink : SinkComponent
    {
        public List<FrameData> Received { get; } = new();

        public CollectSink(int capacity = 16)
            : base("CollectSink", capacity, BackpressurePolicy.Block) { }

        protected override void OnFrame(FrameData frame, PipelineContext context)
        {
            lock (Received) { Received.Add(frame); }
        }
    }

    [Fact]
    public void Component_InitialState_IsCreated()
    {
        var source = new TestSource();
        Assert.Equal(ComponentState.Created, source.State);
    }

    [Fact]
    public void LinkTo_Self_Throws()
    {
        var source = new TestSource();
        Assert.Throws<InvalidOperationException>(() => source.LinkTo(source));
    }

    [Fact]
    public void LinkTo_Duplicate_Throws()
    {
        var source = new TestSource();
        var sink = new CollectSink();
        source.LinkTo(sink);
        Assert.Throws<InvalidOperationException>(() => source.LinkTo(sink));
    }

    [Fact]
    public void LinkTo_Chain_Works()
    {
        var source = new TestSource();
        var t1 = new AddTransform(1);
        var t2 = new AddTransform(2);
        var sink = new CollectSink();

        source.LinkTo(t1).LinkTo(t2).LinkTo(sink);

        Assert.Contains(t1, source.Downstream);
        Assert.Contains(t2, t1.Downstream);
        Assert.Contains(sink, t2.Downstream);
    }

    [Fact]
    public void Context_BeforeStart_Throws()
    {
        var source = new TestSource();
        Assert.Throws<InvalidOperationException>(() => { var _ = source.Context; });
    }
}