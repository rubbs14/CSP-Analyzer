using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CspAnalyzer.Desktop.ViewModels;

namespace CspAnalyzer.Desktop.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        DataContext = new HelpViewModel();
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HelpViewModel vm && !string.IsNullOrEmpty(vm.GeneratedCommandText))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(vm.GeneratedCommandText);
            }
        }
    }
}
