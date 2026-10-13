using System.Windows;
using HandyControl.Controls;
using HandyControl.Data;

namespace VisionFlow.WpfSamples.Parameters.Editors;

/// <summary>
/// Minimal text editor. Used as fallback when HandyControl's
/// PropertyResolver.CreateDefaultEditor returns null.
/// </summary>
public sealed class ParameterPlainTextEditor : PropertyEditorBase
{
    public override FrameworkElement CreateElement(PropertyItem propertyItem)
    {
        var tb = new System.Windows.Controls.TextBox
        {
            IsReadOnly = propertyItem.IsReadOnly,
            MinWidth = 100,
        };
        return tb;
    }

    public override DependencyProperty GetDependencyProperty()
        => System.Windows.Controls.TextBox.TextProperty;
}