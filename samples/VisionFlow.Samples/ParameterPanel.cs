using System.Drawing;
using System.Windows.Forms;
using VisionFlow.Streaming.Parameters;

namespace VisionFlow.Samples;

/// <summary>
/// 参数面板控件。自动为 IParameterized 组件的参数生成控件。
/// 用户修改控件时，调用 SetParameter，立即生效。
/// </summary>
public sealed class ParameterPanel : UserControl
{
    private const int GroupBoxWidth = 300;
    private const int LabelWidth = 70;
    private const int ControlLeft = 76;
    private const int TrackBarWidth = 130;
    private const int ValueLabelLeft = 210;
    private const int ValueLabelWidth = 80;
    private const int RowHeight = 34;

    private readonly IReadOnlyList<IParameterized> _components;

    public ParameterPanel(IReadOnlyList<IParameterized> components)
    {
        _components = components ?? throw new ArgumentNullException(nameof(components));
        AutoScroll = true;
        BackColor = Color.WhiteSmoke;
        Build();
    }

    private void Build()
    {
        int y = 6;
        foreach (var component in _components)
        {
            if (component.Parameters.Count == 0) continue;

            var componentName = component.GetType().Name;

            int groupHeight = 24 + component.Parameters.Count * RowHeight;

            var group = new GroupBox
            {
                Text = componentName,
                Left = 4,
                Top = y,
                Width = GroupBoxWidth,
                Height = groupHeight,
                Font = new Font("Microsoft YaHei UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };

            int innerY = 35;
            foreach (var param in component.Parameters)
            {
                BuildParameterRow(group, component, param, innerY);
                innerY += RowHeight;
            }

            Controls.Add(group);
            y += group.Height + 8;
        }
    }

    private void BuildParameterRow(GroupBox group, IParameterized component,
        ParameterDescriptor param, int top)
    {
        var label = new Label
        {
            Text = param.DisplayName,
            Left = 6,
            Top = top + 8,
            Width = LabelWidth,
            Height = 20,
            Font = new Font("Microsoft YaHei UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        group.Controls.Add(label);

        switch (param.Kind)
        {
            case ParameterKind.Int:
                BuildIntControl(group, component, param, top);
                break;
            case ParameterKind.Double:
                BuildDoubleControl(group, component, param, top);
                break;
            case ParameterKind.Bool:
                BuildBoolControl(group, component, param, top);
                break;
            case ParameterKind.Color:
                BuildColorControl(group, component, param, top);
                break;
            case ParameterKind.String:
                BuildStringControl(group, component, param, top);
                break;
            case ParameterKind.Enum:
                BuildEnumControl(group, component, param, top);
                break;
        }
    }

    private void BuildIntControl(GroupBox group, IParameterized component,
        ParameterDescriptor param, int top)
    {
        int min = (int)(param.Min ?? 0);
        int max = (int)(param.Max ?? 100);
        int step = (int)(param.Step ?? 1);
        if (step < 1) step = 1;
        if (max <= min) max = min + 1;

        var slider = new TrackBar
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = Math.Max(1, (max - min) / 8),
            SmallChange = step,
            LargeChange = step * 5,
            Left = ControlLeft,
            Top = top,
            Width = TrackBarWidth,
            Height = 28,
        };

        var valueLabel = new Label
        {
            Left = ValueLabelLeft,
            Top = top + 6,
            Width = ValueLabelWidth,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 8.5F),
        };

        var initValue = component.GetParameter(param.Key);
        if (initValue is int iv)
        {
            slider.Value = Math.Max(min, Math.Min(max, iv));
            valueLabel.Text = FormatWithUnit(iv, param.Unit);
        }

        slider.ValueChanged += (s, e) =>
        {
            try
            {
                component.SetParameter(param.Key, slider.Value);
                valueLabel.Text = FormatWithUnit(slider.Value, param.Unit);
            }
            catch { }
        };

        group.Controls.Add(slider);
        group.Controls.Add(valueLabel);
    }

    private void BuildDoubleControl(GroupBox group, IParameterized component,
        ParameterDescriptor param, int top)
    {
        double min = param.Min ?? 0;
        double max = param.Max ?? 1;
        const int scale = 100;

        var slider = new TrackBar
        {
            Minimum = (int)(min * scale),
            Maximum = (int)(max * scale),
            TickFrequency = Math.Max(1, (int)((max - min) * scale / 8)),
            SmallChange = 1,
            LargeChange = 10,
            Left = ControlLeft,
            Top = top,
            Width = TrackBarWidth,
            Height = 28,
        };

        var valueLabel = new Label
        {
            Left = ValueLabelLeft,
            Top = top + 6,
            Width = ValueLabelWidth,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 8.5F),
        };

        var initValue = component.GetParameter(param.Key);
        if (initValue is double dv)
        {
            int iv = (int)(dv * scale);
            slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, iv));
            valueLabel.Text = dv.ToString("F2");
        }

        slider.ValueChanged += (s, e) =>
        {
            try
            {
                double val = slider.Value / (double)scale;
                component.SetParameter(param.Key, val);
                valueLabel.Text = val.ToString("F2");
            }
            catch { }
        };

        group.Controls.Add(slider);
        group.Controls.Add(valueLabel);
    }

    private void BuildBoolControl(GroupBox group, IParameterized component,
        ParameterDescriptor param, int top)
    {
        var check = new CheckBox
        {
            Left = ControlLeft,
            Top = top + 4,
            Width = 60,
            Height = 22,
        };

        var initValue = component.GetParameter(param.Key);
        if (initValue is bool bv) check.Checked = bv;

        check.CheckedChanged += (s, e) =>
        {
            try { component.SetParameter(param.Key, check.Checked); }
            catch { }
        };

        group.Controls.Add(check);
    }

    private void BuildColorControl(GroupBox group, IParameterized component,
        ParameterDescriptor param, int top)
    {
        var button = new Button
        {
            Left = ControlLeft,
            Top = top + 2,
            Width = 60,
            Height = 24,
            Text = "选择...",
        };

        var preview = new Panel
        {
            Left = ControlLeft + 68,
            Top = top + 2,
            Width = 40,
            Height = 24,
            BorderStyle = BorderStyle.FixedSingle,
        };

        int currentArgb = unchecked((int)0xFF00FF00);
        var initValue = component.GetParameter(param.Key);
        if (initValue is int iv) currentArgb = iv;

        preview.BackColor = Color.FromArgb(
            (currentArgb >> 16) & 0xFF,
            (currentArgb >> 8) & 0xFF,
            currentArgb & 0xFF);

        var previewLocal = preview;
        var componentLocal = component;
        var paramKey = param.Key;

        button.Click += (s, e) =>
        {
            using var dialog = new ColorDialog();
            dialog.Color = previewLocal.BackColor;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                int argb = unchecked((int)0xFF000000)
                    | (dialog.Color.R << 16)
                    | (dialog.Color.G << 8)
                    | dialog.Color.B;
                try
                {
                    componentLocal.SetParameter(paramKey, argb);
                    previewLocal.BackColor = dialog.Color;
                }
                catch { }
            }
        };

        group.Controls.Add(button);
        group.Controls.Add(preview);
    }

    private void BuildStringControl(GroupBox group, IParameterized component,
        ParameterDescriptor param, int top)
    {
        var textBox = new TextBox
        {
            Left = ControlLeft,
            Top = top + 4,
            Width = 210,
        };

        var initValue = component.GetParameter(param.Key);
        if (initValue is string sv) textBox.Text = sv;

        textBox.TextChanged += (s, e) =>
        {
            try { component.SetParameter(param.Key, textBox.Text); }
            catch { }
        };

        group.Controls.Add(textBox);
    }

    private void BuildEnumControl(GroupBox group, IParameterized component,
        ParameterDescriptor param, int top)
    {
        var combo = new ComboBox
        {
            Left = ControlLeft,
            Top = top + 4,
            Width = 210,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };

        if (param.EnumOptions is not null)
        {
            foreach (var opt in param.EnumOptions) combo.Items.Add(opt);
        }

        combo.SelectedIndexChanged += (s, e) =>
        {
            if (combo.SelectedItem is EnumOption opt)
            {
                try { component.SetParameter(param.Key, opt.Value); }
                catch { }
            }
        };

        group.Controls.Add(combo);
    }

    private static string FormatWithUnit(int value, string? unit)
        => string.IsNullOrEmpty(unit) ? value.ToString() : $"{value}{unit}";
}