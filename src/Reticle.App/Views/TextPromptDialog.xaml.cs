using System.Windows;
using System.Windows.Automation;

namespace Reticle.App.Views;

/// <summary>Single-line text input (rename).</summary>
public partial class TextPromptDialog : Window
{
    public TextPromptDialog(string title, string label, string initial)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        Title = title;
        LabelText.Text = label;
        InputBox.Text = initial;
        AutomationProperties.SetName(InputBox, label);
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string Value => InputBox.Text;

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
