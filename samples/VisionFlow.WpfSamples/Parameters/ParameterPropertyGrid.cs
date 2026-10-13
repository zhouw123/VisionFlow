using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HandyControl.Controls;
using HandyControl.Data;
using VisionFlow.Streaming.Parameters;
using VisionFlow.WpfSamples.Parameters.Editors;

namespace VisionFlow.WpfSamples.Parameters;

/// <summary>
/// Custom PropertyGrid that displays a list of IParameterized components.
/// Applies HandyControl's PropertyGrid style explicitly in code so derived
/// types inherit the correct template (search bar + items control).
/// </summary>
public sealed class ParameterPropertyGrid : PropertyGrid
{
    private const string PartItemsControl = "PART_ItemsControl";
    private IReadOnlyList<IParameterized> _components = Array.Empty<IParameterized>();

    public ParameterPropertyGrid()
    {
        // Try to apply the base style as soon as we have access to resources
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyBaseStyleIfNeeded();
    }

    /// <summary>
    /// If Style has not been set (no BasedOn in XAML),
    /// look up the PropertyGrid default style from application resources
    /// and apply it to this control.
    /// </summary>
    private void ApplyBaseStyleIfNeeded()
    {
        if (Style is not null) return;
        if (Application.Current is null) return;

        // Implicit styles are keyed by their TargetType
        var baseStyle = Application.Current.TryFindResource(typeof(PropertyGrid)) as Style;

        // Fallback: some apps put the style under PropertyGrid's full type name
        if (baseStyle is null)
        {
            baseStyle = Application.Current.TryFindResource(
                typeof(HandyControl.Controls.PropertyGrid)) as Style;
        }

        if (baseStyle is not null)
        {
            Style = baseStyle;
        }
    }

    public void SetComponents(IReadOnlyList<IParameterized>? components)
    {
        _components = components ?? Array.Empty<IParameterized>();
        Dispatcher.BeginInvoke(new Action(RebuildItems),
            DispatcherPriority.Loaded);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        RebuildItems();
    }

    private void RebuildItems()
    {
        var itemsControl = GetTemplateChild(PartItemsControl) as ItemsControl;
        if (itemsControl is null) return;

        if (_components.Count == 0)
        {
            itemsControl.ItemsSource = null;
            return;
        }

        var items = new List<PropertyItem>();

        foreach (var component in _components)
        {
            var category = component.GetType().Name;

            foreach (var param in component.Parameters)
            {
                var proxy = new ParameterProxy(component, param);

                Type propertyType = param.Kind switch
                {
                    ParameterKind.Int => typeof(double),
                    ParameterKind.Double => typeof(double),
                    ParameterKind.Bool => typeof(bool),
                    ParameterKind.Color => typeof(System.Windows.Media.Color),
                    ParameterKind.String => typeof(string),
                    ParameterKind.Enum => typeof(string),
                    _ => typeof(string),
                };

                PropertyEditorBase? editor = null;
                if (param.Kind == ParameterKind.Enum)
                {
                    editor = new ParameterEnumPropertyEditor();
                }
                else if (param.Kind == ParameterKind.Color)
                {
                    editor = new ParameterColorPropertyEditor();
                }
                else
                {
                    try { editor = PropertyResolver.CreateDefaultEditor(propertyType); }
                    catch { editor = null; }
                }

                if (editor is null)
                {
                    editor = new ParameterPlainTextEditor();
                }

                var item = new ParameterPropertyItem
                {
                    Category = category,
                    DisplayName = param.DisplayName,
                    Description = param.Description ?? string.Empty,
                    IsReadOnly = false,
                    Value = proxy,
                    PropertyName = "Value",
                    PropertyType = propertyType,
                    PropertyTypeName = $"{propertyType.Namespace}.{propertyType.Name}",
                    Parameter = param,
                    Editor = editor,
                };

                TryInitElement(item);
                items.Add(item);
            }
        }

        itemsControl.ItemsSource = items;
    }

    private static void TryInitElement(PropertyItem item)
    {
        try
        {
            var mi = typeof(PropertyItem).GetMethod(
                "InitElement",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mi?.Invoke(item, null);
        }
        catch { }
    }
}