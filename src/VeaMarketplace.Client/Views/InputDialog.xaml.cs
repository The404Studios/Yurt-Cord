using System.Windows;

namespace VeaMarketplace.Client.Views;

public partial class InputDialog : Window
{
    public string ResponseText { get; private set; } = string.Empty;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputTextBox.Text = defaultValue;
        InputTextBox.SelectAll();
        InputTextBox.Focus();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        ResponseText = InputTextBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    public static string? Show(string title, string prompt, string defaultValue = "")
    {
        var dialog = new InputDialog(title, prompt, defaultValue);
        if (dialog.ShowDialog() == true)
        {
            return dialog.ResponseText;
        }
        return null;
    }
}
