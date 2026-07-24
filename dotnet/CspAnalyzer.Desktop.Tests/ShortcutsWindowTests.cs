using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class ShortcutsWindowTests
{
    [AvaloniaFact]
    public void ShortcutsWindow_ListsWiredAndNotYetImplementedRows()
    {
        var window = new ShortcutsWindow();
        window.Show();

        string[] texts = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToArray();

        Assert.Contains(texts, t => t.Contains("Next Spectrum"));
        Assert.Contains(texts, t => t.Contains("Right"));
        Assert.Contains(texts, t => t.Contains("Show Auto Actives") && !t.Contains("not yet implemented"));
        Assert.Contains(texts, t => t.Contains("Export To Excel"));
        Assert.Contains(texts, t => t == "H");
        Assert.Contains(texts, t => t.Contains("Show Help Guide") && !t.Contains("not yet implemented"));
        Assert.Contains(texts, t => t.Contains("Show Information Window") && !t.Contains("not yet implemented"));
        Assert.DoesNotContain(texts, t => t.Contains("not yet implemented"));
    }
}
