using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Reticle.App.ViewModels;

namespace Reticle.App.Views.Controls;

/// <summary>Color as hex text (<c>#RRGGBB</c> / <c>#RRGGBBAA</c>) with a swatch that opens the system color picker.</summary>
public partial class ColorField : UserControl
{
    public static readonly DependencyProperty HexProperty = DependencyProperty.Register(
        nameof(Hex), typeof(string), typeof(ColorField),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorField)d).ShowValue()));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ColorField));

    public ColorField()
    {
        InitializeComponent();
        PartText.LostKeyboardFocus += (_, _) => CommitText();
        PartText.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CommitText();
            else if (e.Key == Key.Escape) ShowValue();
        };
        PartSwatch.Click += (_, _) => PickColor();
        Loaded += (_, _) => ShowValue();
    }

    public string Hex { get => (string)GetValue(HexProperty); set => SetValue(HexProperty, value); }
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private void ShowValue()
    {
        PartText.Text = Hex;
        PartChip.Background = ColorText.TryParse(Hex, out var c)
            ? new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B))
            : Brushes.Transparent;
    }

    private void CommitText()
    {
        var text = PartText.Text.Trim();
        if (text != Hex) Hex = text;
        ShowValue();
    }

    private void PickColor()
    {
        ColorText.TryParse(Hex, out var current);
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var p = dialog.Color;
        // The system dialog has no alpha channel: keep the current alpha (a fully transparent color becomes opaque).
        byte alpha = current.A == 0 ? (byte)255 : current.A;
        Hex = $"#{p.R:X2}{p.G:X2}{p.B:X2}{alpha:X2}";
        ShowValue();
    }
}
