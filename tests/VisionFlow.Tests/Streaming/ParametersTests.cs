using VisionFlow.Integration;
using VisionFlow.Streaming;
using VisionFlow.Streaming.Parameters;

namespace VisionFlow.Tests.Streaming;

public class ParametersTests
{
    private sealed class TestTransform : TransformComponent
    {
        private volatile int _threshold = 50;
        private double _scale = 1.0;
        private volatile bool _enabled = true;
        private string _name = "default";

        public TestTransform() : base("TestTransform")
        {
            RegisterParameter(
                "Threshold", "阈值",
                () => _threshold, v => _threshold = v,
                min: 0, max: 255, step: 1, unit: "级");

            RegisterParameter(
                "Scale", "缩放",
                () => _scale, v => _scale = v,
                min: 0.1, max: 10.0, step: 0.1);

            RegisterParameter(
                "Enabled", "启用",
                () => _enabled, v => _enabled = v);

            RegisterParameter(
                "Name", "名称",
                () => _name, v => _name = v);
        }

        protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
            => input;

        public int Threshold => _threshold;
        public double Scale => _scale;
        public bool Enabled => _enabled;
        public string Name2 => _name;
    }

    [Fact]
    public void Parameters_ReturnsRegistered()
    {
        var t = new TestTransform();
        Assert.Equal(4, t.Parameters.Count);
        Assert.Contains(t.Parameters, p => p.Key == "Threshold");
        Assert.Contains(t.Parameters, p => p.Key == "Scale");
        Assert.Contains(t.Parameters, p => p.Key == "Enabled");
        Assert.Contains(t.Parameters, p => p.Key == "Name");
    }

    [Fact]
    public void Parameters_KindInferredCorrectly()
    {
        var t = new TestTransform();
        Assert.Equal(ParameterKind.Int,
            t.Parameters.First(p => p.Key == "Threshold").Kind);
        Assert.Equal(ParameterKind.Double,
            t.Parameters.First(p => p.Key == "Scale").Kind);
        Assert.Equal(ParameterKind.Bool,
            t.Parameters.First(p => p.Key == "Enabled").Kind);
        Assert.Equal(ParameterKind.String,
            t.Parameters.First(p => p.Key == "Name").Kind);
    }

    [Fact]
    public void Parameters_MetadataPreserved()
    {
        var t = new TestTransform();
        var p = t.Parameters.First(x => x.Key == "Threshold");
        Assert.Equal(0, p.Min);
        Assert.Equal(255, p.Max);
        Assert.Equal(1, p.Step);
        Assert.Equal("级", p.Unit);
    }

    [Fact]
    public void GetParameter_ReturnsCurrentValue()
    {
        var t = new TestTransform();
        Assert.Equal(50, t.GetParameter("Threshold"));
        Assert.Equal(1.0, t.GetParameter("Scale"));
        Assert.Equal(true, t.GetParameter("Enabled"));
        Assert.Equal("default", t.GetParameter("Name"));
    }

    [Fact]
    public void GetParameter_UnknownKey_ReturnsNull()
    {
        var t = new TestTransform();
        Assert.Null(t.GetParameter("NoSuchKey"));
    }

    [Fact]
    public void SetParameter_UpdatesField()
    {
        var t = new TestTransform();
        t.SetParameter("Threshold", 100);
        Assert.Equal(100, t.Threshold);
        Assert.Equal(100, t.GetParameter("Threshold"));
    }

    [Fact]
    public void SetParameter_TriggersEvent()
    {
        var t = new TestTransform();
        ParameterChangedEventArgs? captured = null;
        t.ParameterChanged += (s, e) => captured = e;

        t.SetParameter("Threshold", 100);

        Assert.NotNull(captured);
        Assert.Equal("Threshold", captured!.Key);
        Assert.Equal(50, captured.OldValue);
        Assert.Equal(100, captured.NewValue);
    }

    [Fact]
    public void SetParameter_UnknownKey_Throws()
    {
        var t = new TestTransform();
        Assert.Throws<ArgumentException>(() => t.SetParameter("NoSuchKey", 1));
    }

    [Fact]
    public void SetParameter_WrongType_Throws()
    {
        var t = new TestTransform();
        Assert.Throws<ArgumentException>(() => t.SetParameter("Threshold", "not an int"));
    }

    [Fact]
    public void RegisterParameter_DuplicateKey_Throws()
    {
        var ex = Record.Exception(() => new DuplicateTransform());
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
    }

    private sealed class DuplicateTransform : TransformComponent
    {
        public DuplicateTransform() : base("Dup")
        {
            RegisterParameter("X", "X", () => 1, v => { });
            RegisterParameter("X", "X2", () => 2, v => { });
        }
        protected override FrameData? ProcessFrame(FrameData input, PipelineContext context)
            => input;
    }
}