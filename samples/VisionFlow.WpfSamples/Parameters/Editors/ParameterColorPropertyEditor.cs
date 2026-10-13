using VisionFlow.WpfSamples.Controls;
using System.Windows;
using HandyControl.Controls;
using HandyControl.Data;

namespace VisionFlow.WpfSamples.Parameters.Editors;

/// <summary>
/// Editor for color parameters. Returns CompactColorPicker.
/// Binds to SelectedColorProperty (Color typed),
/// matching ParameterProxy.Value.
/// </summary>
public sealed class ParameterColorPropertyEditor : PropertyEditorBase
{
    public override FrameworkElement CreateElement(PropertyItem propertyItem)
    {
        return new CompactColorPicker
        {
            IsEnabled = !propertyItem.IsReadOnly,
        };
    }

    public override DependencyProperty GetDependencyProperty()
        => CompactColorPicker.SelectedColorProperty;
}