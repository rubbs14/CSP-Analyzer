using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CspAnalyzer.Desktop.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message) : this()
    {
        Title = title;
        MessageText.Text = message;
    }

    private void OnYesClicked(object? sender, RoutedEventArgs e) => Close(true);

    private void OnNoClicked(object? sender, RoutedEventArgs e) => Close(false);
}
