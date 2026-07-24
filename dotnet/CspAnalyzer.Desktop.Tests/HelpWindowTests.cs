using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class HelpWindowTests
{
    [AvaloniaFact]
    public void HelpWindow_ShowsRewordedTipsAndTricksContent()
    {
        var window = new HelpWindow();
        window.Show();

        string[] texts = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToArray();

        Assert.Contains(texts, t => t.Contains("No actives found after analysis"));
        Assert.Contains(texts, t => t.Contains("csp_modern conda environment"));
        Assert.Contains(texts, t => t.Contains("Peak lists extractor"));
        Assert.DoesNotContain(texts, t => t.Contains("SMOTE-ENN"));
        Assert.DoesNotContain(texts, t => t.Contains("PPMPNUM"));
    }

    private static void FillValidGeneratorInputs(HelpWindow window)
    {
        window.FindControl<TextBox>("NMaxTextBox")!.Text = "135";
        window.FindControl<TextBox>("HMaxTextBox")!.Text = "11";
        window.FindControl<TextBox>("NMinTextBox")!.Text = "105";
        window.FindControl<TextBox>("HMinTextBox")!.Text = "6";
        window.FindControl<TextBox>("MiTextBox")!.Text = "0.0001";
        window.FindControl<TextBox>("PpNumTextBox")!.Text = "90";
    }

    [AvaloniaFact]
    public void Generate_WithValidInputs_PopulatesGeneratedCommandTextBox()
    {
        var window = new HelpWindow();
        window.Show();
        FillValidGeneratorInputs(window);

        Button generateButton = window.GetVisualDescendants().OfType<Button>()
            .Single(b => (string?)b.Content == "Generate");
        generateButton.Command!.Execute(null);

        var output = window.FindControl<TextBox>("GeneratedCommandTextBox")!;
        Assert.Equal(
            "1 F1P 135; 2 F1P 11; 1 F2P 105; 2 F2P 6; MI 0.0001; PPNUM 90; pp2d nodia",
            output.Text);
    }

    [AvaloniaFact]
    public async Task CopyClicked_WithGeneratedText_SetsClipboard()
    {
        var window = new HelpWindow();
        window.Show();
        FillValidGeneratorInputs(window);
        window.GetVisualDescendants().OfType<Button>()
            .Single(b => (string?)b.Content == "Generate").Command!.Execute(null);

        Button copyButton = window.GetVisualDescendants().OfType<Button>()
            .Single(b => (string?)b.Content == "Copy");
        copyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // HelpWindow's Click handler is `async void` - RaiseEvent doesn't wait
        // for it, so reading the clipboard immediately races its SetTextAsync
        // continuation. Usually wins locally; flaked intermittently on macOS
        // CI runners. Poll briefly instead of asserting on the first read.
        string? clipboardText = await PollClipboardTextAsync(window);
        Assert.Equal(
            "1 F1P 135; 2 F1P 11; 1 F2P 105; 2 F2P 6; MI 0.0001; PPNUM 90; pp2d nodia",
            clipboardText);
    }

    private static async Task<string?> PollClipboardTextAsync(HelpWindow window, int maxAttempts = 20, int delayMs = 10)
    {
        string? clipboardText = null;
        for (int attempt = 0; attempt < maxAttempts && string.IsNullOrEmpty(clipboardText); attempt++)
        {
            clipboardText = await TopLevel.GetTopLevel(window)!.Clipboard!.GetTextAsync();
            if (string.IsNullOrEmpty(clipboardText))
            {
                await Task.Delay(delayMs);
            }
        }
        return clipboardText;
    }

    [AvaloniaFact]
    public async Task CopyClicked_WithEmptyGeneratedText_DoesNotThrowOrSetClipboard()
    {
        var window = new HelpWindow();
        window.Show();

        Button copyButton = window.GetVisualDescendants().OfType<Button>()
            .Single(b => (string?)b.Content == "Copy");
        copyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        string? clipboardText = await TopLevel.GetTopLevel(window)!.Clipboard!.GetTextAsync();
        Assert.True(string.IsNullOrEmpty(clipboardText));
    }
}
