using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class AboutWindowTests
{
    [AvaloniaFact]
    public void AboutWindow_ShowsAppNameAndDeveloperCredit()
    {
        var window = new AboutWindow();
        window.Show();

        string[] texts = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToArray();

        Assert.Contains(texts, t => t.Contains("CSP Analyzer"));
        Assert.Contains(texts, t => t.Contains("R. Byrne and R. Fino"));
    }
}
