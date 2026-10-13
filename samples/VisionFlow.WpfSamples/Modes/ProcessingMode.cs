using VisionFlow.Streaming;

namespace VisionFlow.WpfSamples.Modes;

/// <summary>
/// Describes one processing mode: a name plus a factory that builds
/// the list of Transform components for that mode.
/// </summary>
public sealed class ProcessingMode
{
    public ProcessingMode(string name,
        Func<IReadOnlyList<FrameComponent>> build)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Build = build ?? throw new ArgumentNullException(nameof(build));
    }

    public string Name { get; }
    public Func<IReadOnlyList<FrameComponent>> Build { get; }

    public override string ToString() => Name;
}