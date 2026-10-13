using HandyControl.Data;
using VisionFlow.Streaming.Parameters;
using HandyControl.Controls;

namespace VisionFlow.WpfSamples.Parameters;

/// <summary>
/// Custom PropertyItem carrying the parameter metadata.
/// Used by custom editors to access enum options etc.
/// </summary>
public sealed class ParameterPropertyItem : PropertyItem
{
    /// <summary>The parameter metadata. Null for non-parameter items.</summary>
    public ParameterDescriptor? Parameter { get; set; }
}