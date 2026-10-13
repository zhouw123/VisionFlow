using System.Diagnostics;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Infrastructure;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;
using VisionFlow.Transforms;

namespace VisionFlow.Tests.Transforms;

public class TransformsTests
{
    private static FrameId MakeId(long n)
        => new FrameId(n * 0.1, n, DateTime.Now);

    private static FrameData MakeBgrFrame(long n, int w, int h)
        => new FrameData(MakeId(n), w, h, MediaSubType.RGB24, new byte[w * h * 3]);

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            Thread.Sleep(10);
        if (!condition()) throw new TimeoutException("等待条件超时。");
    }

    private sealed class CollectSink : SinkComponent
    {
        public List<FrameData> Received { get; } = new();
        public CollectSink() : base("Sink", 16, BackpressurePolicy.Block) { }
        protected override void OnFrame(FrameData frame, PipelineContext context)
        {
            lock (Received) { Received.Add(frame); }
        }
    }

    private sealed class SimpleSource : SourceComponent
    {
        public FrameData Frame { get; set; } = null!;
        public SimpleSource() : base("Src") { }
        protected override void OnStartSource()
        {
            RaiseFrameProduced(Frame);
        }
        protected override void OnStopSource() { }
    }

    [Fact]
    public void ResizeTransform_HasParameters()
    {
        var t = new ResizeTransform();
        Assert.Equal(2, t.Parameters.Count);
        Assert.Contains(t.Parameters, p => p.Key == "ScaleX");
        Assert.Contains(t.Parameters, p => p.Key == "ScaleY");
        Assert.Equal(ParameterKind.Double,
            t.Parameters.First(p => p.Key == "ScaleX").Kind);
    }

    [Fact]
    public void ResizeTransform_SetScale_Works()
    {
        var t = new ResizeTransform();
        t.SetParameter("ScaleX", 2.0);
        t.SetParameter("ScaleY", 1.5);
        Assert.Equal(2.0, t.ScaleX);
        Assert.Equal(1.5, t.ScaleY);
    }

    [Fact]
    public void ResizeTransform_ScalesFrame()
    {
        var t = new ResizeTransform();
        t.SetParameter("ScaleX", 0.5);
        t.SetParameter("ScaleY", 0.5);

        var source = new SimpleSource { Frame = MakeBgrFrame(1, 100, 80) };
        var sink = new CollectSink();
        source.LinkTo(t).LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        graph.Start();
        WaitUntil(() => sink.ConsumedFrames >= 1, 3000);
        graph.Stop();

        lock (sink.Received)
        {
            var outFrame = sink.Received[0];
            Assert.Equal(50, outFrame.Width);
            Assert.Equal(40, outFrame.Height);
        }
    }

    [Fact]
    public void CannyTransform_HasParameters()
    {
        var t = new CannyTransform();
        Assert.True(t.Parameters.Count >= 5);
        Assert.Contains(t.Parameters, p => p.Key == "Threshold1");
        Assert.Contains(t.Parameters, p => p.Key == "Threshold2");
        Assert.Contains(t.Parameters, p => p.Key == "EdgeColor");
        Assert.Equal(ParameterKind.Color,
            t.Parameters.First(p => p.Key == "EdgeColor").Kind);
    }

    [Fact]
    public void CannyTransform_SetThreshold_Works()
    {
        var t = new CannyTransform();
        t.SetParameter("Threshold1", 80);
        t.SetParameter("Threshold2", 200);
        Assert.Equal(80, t.Threshold1);
        Assert.Equal(200, t.Threshold2);
    }

    [Fact]
    public void CannyTransform_ProcessesFrame()
    {
        var t = new CannyTransform();
        t.SetParameter("Threshold1", 50);
        t.SetParameter("Threshold2", 150);

        var source = new SimpleSource { Frame = MakeBgrFrame(1, 100, 80) };
        var sink = new CollectSink();
        source.LinkTo(t).LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        graph.Start();
        WaitUntil(() => sink.ConsumedFrames >= 1, 3000);
        graph.Stop();

        lock (sink.Received)
        {
            var outFrame = sink.Received[0];
            Assert.Equal(100, outFrame.Width);
            Assert.Equal(80, outFrame.Height);
            Assert.Equal(MediaSubType.RGB24, outFrame.SubType);
        }
    }

    [Fact]
    public void Yuy2ToBgrTransform_IgnoresNonYuy2()
    {
        var t = new Yuy2ToBgrTransform();
        var source = new SimpleSource { Frame = MakeBgrFrame(1, 100, 80) };
        var sink = new CollectSink();
        source.LinkTo(t).LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        graph.Start();
        WaitUntil(() => sink.ConsumedFrames >= 1, 3000);
        graph.Stop();

        lock (sink.Received)
        {
            // 输入不是 YUY2，原样透传
            var outFrame = sink.Received[0];
            Assert.Equal(MediaSubType.RGB24, outFrame.SubType);
            Assert.Equal(100, outFrame.Width);
        }
    }
}