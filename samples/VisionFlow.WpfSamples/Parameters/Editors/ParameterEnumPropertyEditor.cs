using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using HandyControl.Controls;
using HandyControl.Data;

namespace VisionFlow.WpfSamples.Parameters.Editors;

/// <summary>
/// Custom editor for Enum parameters.
/// Creates a ComboBox populated with the option display names.
/// 
/// The binding is set up by HandyControl using:
///   Path = PropertyItem.PropertyName (which is "Value")
///   Source = PropertyItem.Value (which is our ParameterProxy)
///   Target DP = Selector.SelectedItemProperty (this method)
/// 
/// Since ParameterProxy exposes a CLR property "Value", the binding works.
/// </summary>
public sealed class ParameterEnumPropertyEditor : PropertyEditorBase
{
    public override FrameworkElement CreateElement(PropertyItem propertyItem)
    {
        var combo = new System.Windows.Controls.ComboBox
        {
            IsEnabled = !propertyItem.IsReadOnly,
            IsEditable = false,
            MinWidth = 100,
        };

        if (propertyItem is ParameterPropertyItem pi && pi.Parameter?.EnumOptions is not null)
        {
            foreach (var opt in pi.Parameter.EnumOptions)
            {
                combo.Items.Add(opt.Display);
            }
        }

        return combo;
    }

    public override DependencyProperty GetDependencyProperty()
        => Selector.SelectedItemProperty;
}