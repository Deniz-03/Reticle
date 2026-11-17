using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Reticle.App.Views.Controls;

/// <summary>
/// Label + slider + number box. Typed values are clamped to [Minimum, Maximum] (never an error); unparsable
/// text reverts. The slider only writes <see cref="Value"/> on user changes, so showing an out-of-range value
/// never edits it.
/// </summary>
public partial class NumberField : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(NumberField),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((NumberField)d).ShowValue()));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(NumberField));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumberField), new PropertyMetadata(0.0));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumberField), new PropertyMetadata(100.0));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(NumberField), new PropertyMetadata(1.0));
    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
        nameof(Decimals), typeof(int), typeof(NumberField), new PropertyMetadata(0, (d, _) => ((NumberField)d).ShowValue()));

    private bool _showing;

    public NumberField()
    {
        InitializeComponent();
        PartSlider.ValueChanged += (_, e) =>
        {
            if (_showing) return;
            Value = Round(e.NewValue);
        };
        PartText.LostKeyboardFocus += (_, _) => CommitText();
        PartText.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CommitText();
            else if (e.Key == Key.Escape) ShowValue();
        };
        Loaded += (_, _) => ShowValue();
    }

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    private double Round(double v) => Math.Round(v, Math.Clamp(Decimals, 0, 6));

    private void ShowValue()
    {
        _showing = true;
        try
        {
            PartSlider.Value = Value;
            PartText.Text = Value.ToString("F" + Math.Clamp(Decimals, 0, 6), CultureInfo.CurrentCulture);
        }
        finally
        {
            _showing = false;
        }
    }

    private void CommitText()
    {
        var text = PartText.Text.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var v)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
        {
            v = Round(Math.Clamp(v, Minimum, Maximum));
            if (v != Value) Value = v;
        }
        ShowValue();
    }
}
