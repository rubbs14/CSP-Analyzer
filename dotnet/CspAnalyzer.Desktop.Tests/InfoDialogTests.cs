using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class InfoDialogTests
{
    [AvaloniaFact]
    public void InfoDialog_displays_title_and_message()
    {
        var dialog = new InfoDialog("Corrupted Peaklist Experiments", "2\n3\n7");
        dialog.Show();

        Assert.Equal("Corrupted Peaklist Experiments", dialog.Title);
        Assert.Contains(
            dialog.GetVisualDescendants().OfType<TextBlock>(),
            t => (t.Text ?? "").Contains("2") && t.Text!.Contains("3") && t.Text!.Contains("7"));
    }

    [Fact]
    public async System.Threading.Tasks.Task NullInfoDialogService_completes_without_showing_anything()
    {
        var service = new NullInfoDialogService();

        await service.ShowAsync("title", "message");
    }
}
