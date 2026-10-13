using System.ComponentModel;
using System.Globalization;
using VisionFlow.Streaming.Parameters;

namespace VisionFlow.WpfSamples.Parameters;

/// <summary>
/// Proxy for a single parameter. Acts as the Source of a WPF Binding.
/// Exposes a real CLR property "Value" that the binding path "Value" hits.
/// Reads and writes are forwarded to the IParameterized component.
/// </summary>
public sealed class ParameterProxy : INotifyPropertyChanged
{
    private readonly IParameterized _component;
    private readonly ParameterDescriptor _param;

    public ParameterProxy(IParameterized component, ParameterDescriptor param)
    {
        _component = component ?? throw new ArgumentNullException(nameof(component));
        _param = param ?? throw new ArgumentNullException(nameof(param));
        _component.ParameterChanged += OnComponentParameterChanged;
    }

    public ParameterDescriptor Param => _param;
    public IParameterized Component => _component;

    public object? Value
    {
        get => GetCurrentValue();
        set => SetCurrentValue(value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private object? GetCurrentValue()
    {
        var raw = _component.GetParameter(_param.Key);
        if (raw is null) return null;

        switch (_param.Kind)
        {
            case ParameterKind.Enum:
                return GetEnumDisplay(raw);

            case ParameterKind.Int when raw is int i:
                return (double)i;

            case ParameterKind.Double when raw is float f:
                return (double)f;

            case ParameterKind.Double when raw is int i2:
                return (double)i2;

            case ParameterKind.Color when raw is int argb:
                return System.Windows.Media.Color.FromArgb(
                    (byte)((argb >> 24) & 0xFF),
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));

            default:
                return raw;
        }
    }

    private void SetCurrentValue(object? value)
    {
        switch (_param.Kind)
        {
            case ParameterKind.Enum when value is string display:
                if (TryParseEnumDisplay(display, out var ev))
                    _component.SetParameter(_param.Key, ev);
                return;

            case ParameterKind.Int when value is double d:
                _component.SetParameter(_param.Key, (int)Math.Round(d));
                return;

            case ParameterKind.Int when value is int i:
                _component.SetParameter(_param.Key, i);
                return;

            case ParameterKind.Double when value is double d2:
                _component.SetParameter(_param.Key, d2);
                return;

            case ParameterKind.Bool when value is bool b:
                _component.SetParameter(_param.Key, b);
                return;

            case ParameterKind.String when value is string str:
                _component.SetParameter(_param.Key, str);
                return;

            case ParameterKind.Color when value is System.Windows.Media.Color c:
                int argb = (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
                _component.SetParameter(_param.Key, argb);
                return;

            default:
                try { _component.SetParameter(_param.Key, value); }
                catch { }
                return;
        }
    }

    private void OnComponentParameterChanged(object? sender, ParameterChangedEventArgs e)
    {
        if (e.Key != _param.Key) return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }

    private string GetEnumDisplay(object raw)
    {
        if (_param.EnumOptions is not null)
        {
            foreach (var opt in _param.EnumOptions)
            {
                if (opt.Value is null) continue;
                if (opt.Value.Equals(raw)) return opt.Display;
                try
                {
                    if (Convert.ToInt64(opt.Value) == Convert.ToInt64(raw))
                        return opt.Display;
                }
                catch { }
            }
        }
        return raw.ToString() ?? string.Empty;
    }

    private bool TryParseEnumDisplay(string display, out object? value)
    {
        value = null;
        if (_param.EnumOptions is null) return false;
        foreach (var opt in _param.EnumOptions)
        {
            if (string.Equals(opt.Display, display, StringComparison.OrdinalIgnoreCase))
            {
                value = opt.Value;
                return true;
            }
        }
        foreach (var opt in _param.EnumOptions)
        {
            if (opt.Value?.ToString() == display)
            {
                value = opt.Value;
                return true;
            }
        }
        return false;
    }
}