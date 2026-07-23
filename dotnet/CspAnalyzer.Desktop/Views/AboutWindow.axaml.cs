using System.Reflection;
using Avalonia.Controls;

namespace CspAnalyzer.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {Assembly.GetExecutingAssembly().GetName().Version}";
    }
}
