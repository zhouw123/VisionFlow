using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Controls;

namespace VisionFlow.WpfSamples.Controls;

/// <summary>
/// Compact color picker: a small color block. Clicking it opens a Popup
/// containing HandyControl's full ColorPicker.
/// 
/// Exposes SelectedColor (Color typed) as a DP for HandyControl binding.
/// </summary>
public sealed class CompactColorPicker : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(CompactColorPicker),
            new FrameworkPropertyMetadata(
                Colors.LimeGreen,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private readonly Border _colorBlock;
    private readonly Popup _popup;

    public CompactColorPicker()
    {
        _colorBlock = new Border
        {
            Width = 48,
            Height = 22,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(SelectedColor),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var picker = new ColorPicker();

        _popup = new Popup
        {
            Child = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Child = picker,
            },
            PlacementTarget = _colorBlock,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
        };

        // 每次打开时，把当前颜色同步给调色板
        _popup.Opened += (s, e) =>
        {
            try
            {
                picker.SelectedBrush = new SolidColorBrush(SelectedColor);
            }
            catch { }
        };

        _colorBlock.MouseLeftButtonUp += (s, e) =>
        {
            _popup.IsOpen = true;
            e.Handled = true;
        };

        // 用户在调色板点击"确定"
        picker.Confirmed += (s, e) =>
        {
            try
            {
                if (picker.SelectedBrush is SolidColorBrush b)
                {
                    SelectedColor = b.Color;
                }
            }
            catch { }
            _popup.IsOpen = false;
        };

        // 用户点击"取消"
        picker.Canceled += (s, e) =>
        {
            _popup.IsOpen = false;
        };

        Content = _colorBlock;
    }

    private static void OnSelectedColorChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CompactColorPicker picker)
        {
            picker.UpdateVisual((Color)e.NewValue);
        }
    }

    private void UpdateVisual(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _colorBlock.Background = brush;
    }
}