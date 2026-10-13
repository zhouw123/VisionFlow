using System.Diagnostics;
using System.Runtime.InteropServices;
using DirectShowLib;
using VisionFlow.Integration;
using VisionFlow.Infrastructure;
using VisionFlow.Sinks;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;

namespace VisionFlow.Tests.Sinks;

public class SinksTests
{
    private static FrameId MakeId(long n)
        => new FrameId(n * 0.1, n, DateTime.Now);

    private static FrameData MakeBgrFrame(long n, int w = 16, int h = 12)
        => new FrameData(MakeId(n), w, h, MediaSubType.RGB24, new byte[w * h * 3]);

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            Thread.Sleep(10);
        if (!condition()) throw new TimeoutException("等待条件超时。");
    }

    private sealed class TestSource : SourceComponent
    {
        public FrameData Frame { get; set; } = null!;
        public TestSource() : base("Src") { }
        protected override void OnStartSource() => RaiseFrameProduced(Frame);
        protected override void OnStopSource() { }
    }

    [Fact]
    public void NullSink_ConsumesFrames()
    {
        var source = new TestSource { Frame = MakeBgrFrame(1) };
        var sink = new NullSink();
        source.LinkTo(sink);

        using var graph = StreamingGraph.From(source);
        graph.Start();
        WaitUntil(() => sink.ConsumedFrames >= 1, 2000);
        graph.Stop();

        Assert.True(sink.ConsumedFrames >= 1);
    }

    [Fact]
    public void SnapshotSink_HasParameters()
    {
        var sink = new SnapshotSink();
        Assert.Equal(3, sink.Parameters.Count);
        Assert.Contains(sink.Parameters, p => p.Key == "OutputDirectory");
        Assert.Contains(sink.Parameters, p => p.Key == "Prefix");
        Assert.Contains(sink.Parameters, p => p.Key == "Format");
        Assert.Equal(ParameterKind.Enum,
            sink.Parameters.First(p => p.Key == "Format").Kind);
    }

    [Fact]
    public void SnapshotSink_RequestSnapshot_SavesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dsw_test_snapshot_" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new SnapshotSink(tempDir);

            var savedPath = (string?)null;
            sink.SnapshotSaved += (s, p) => savedPath = p;

            var source = new TestSource { Frame = MakeBgrFrame(1) };
            source.LinkTo(sink);

            using var graph = StreamingGraph.From(source);
            graph.Start();

            // 等源发出一帧，然后请求
            Thread.Sleep(50);
            sink.RequestSnapshot();

            // 再补发几帧让请求被消费
            Thread.Sleep(200);
            graph.Stop();

            // 由于 TestSource 只发一帧，可能请求太晚，需要重跑
            if (sink.SavedCount == 0)
            {
                // 重新测试：请求在先，源在后
                var sink2 = new SnapshotSink(tempDir);
                var source2 = new TestSource { Frame = MakeBgrFrame(2) };
                source2.LinkTo(sink2);
                using var graph2 = StreamingGraph.From(source2);

                sink2.RequestSnapshot();
                graph2.Start();

                WaitUntil(() => sink2.SavedCount >= 1, 3000);
                graph2.Stop();

                Assert.True(sink2.SavedCount >= 1);
                Assert.NotNull(sink2.LastSavedPath);
                Assert.True(File.Exists(sink2.LastSavedPath!));
            }
            else
            {
                Assert.NotNull(savedPath);
                Assert.True(File.Exists(savedPath!));
            }
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Branch_OneSource_TwoSinks_BothReceive()
    {
        var source = new TestSource { Frame = MakeBgrFrame(1) };
        var displaySink = new NullSink("Display");
        var snapshotSink = new NullSink("Snapshot");

        source.LinkTo(displaySink);
        source.LinkTo(snapshotSink);

        using var graph = StreamingGraph.From(source);
        graph.Start();
        WaitUntil(() => displaySink.ConsumedFrames >= 1 && snapshotSink.ConsumedFrames >= 1, 3000);
        graph.Stop();

        Assert.True(displaySink.ConsumedFrames >= 1);
        Assert.True(snapshotSink.ConsumedFrames >= 1);
    }
}