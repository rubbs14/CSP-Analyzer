# S11d: Help Window + TopSpin Command Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port `CSPv2/FormHelp.cs` into a new Avalonia `HelpWindow` — static
Tips/Tricks content (reworded where the legacy text describes the old
stack inaccurately) plus a real TopSpin peak-picking command generator —
opened via the sidebar's existing inert "Help" button and a new guarded
`H` keybinding.

**Architecture:** Follows S11c's `IAboutWindowService`/`IShortcutsWindowService`
pattern exactly: `IHelpWindowService` + `AvaloniaHelpWindowService`/
`NullHelpWindowService`, a 6th `MainViewModel` constructor parameter, an
`OpenHelpWindowCommand` in `MainViewModel.SecondaryWindows.cs`. The
generator's state lives in a new, plain (non-Avalonia) `HelpViewModel`
instantiated directly by `HelpWindow`'s code-behind — it needs no DI since
it's self-contained, the same reasoning `AboutWindow` uses to need no
ViewModel at all (except this one has real state to test in isolation).

**Tech Stack:** .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm, xunit +
Avalonia.Headless.XUnit (`[AvaloniaFact]`, `KeyPressQwerty`/`KeyReleaseQwerty`).

## Global Constraints

- Full design spec: `docs/superpowers/specs/2026-07-23-sub-project-3-s11d-help-window-topspin-generator-design.md`.
- Generated TopSpin command format (fixes legacy's `PPMPNUM` label typo —
  the token is `PPNUM` everywhere, including the label):
  `"1 F1P {NMax}; 2 F1P {HMax}; 1 F2P {NMin}; 2 F2P {HMin}; MI {MI}; PPNUM {PPNUM}; pp2d nodia"`.
- Legacy field→token mapping (from `CSPv2/FormHelp.cs`'s `button_generate`):
  `textBox_1F1P` = NMax, `textBox_2F1P` = HMax, `textBox_1F2P` = NMin,
  `textBox_2F2P` = HMin, `textBox_MI` = MI, `textBox_PPNUM` = PPNUM.
- CanExecute-gated validation, not legacy's keystroke-filtering: Generate
  is only enabled when all six inputs parse (`double.TryParse` for
  NMax/NMin/HMax/HMin/MI, `int.TryParse` for PPNUM), using
  `CultureInfo.InvariantCulture`.
- `H` is a bare-letter keybinding — per this codebase's established
  convention (`MainWindow.axaml.cs`'s `GuardedViewModelCommand`,
  covering `R`/`T`/`N`/`D`/`S`/`A`/`G`/arrows), it MUST be added in
  `MainWindow.axaml.cs`'s constructor via `GuardedViewModelCommand`, NOT
  as a plain `<KeyBinding>` in `MainWindow.axaml`'s
  `<Window.KeyBindings>` block (that block is reserved for Ctrl/Ctrl+Alt
  combos that don't collide with normal typing) — otherwise typing "h"
  into any sidebar `TextBox` would hijack focus and open the Help window.
- Test command: `dotnet test dotnet/CspAnalyzer.sln --filter <ClassName>`
  for a single class, `dotnet test dotnet/CspAnalyzer.sln` for the full
  suite. Run the full suite at the end of every task, not just the
  filtered new tests — this codebase's convention (every prior S11
  session) to catch cross-task regressions immediately.
- Commit only when explicitly instructed to in this plan (end of each
  task) — this matches how S11b/S11c were executed.

---

### Task 1: `HelpViewModel` (TopSpin generator logic, no UI)

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/HelpViewModel.cs`
- Test: `dotnet/CspAnalyzer.Desktop.Tests/HelpViewModelTests.cs`

**Interfaces:**
- Consumes: nothing (no dependencies on other tasks).
- Produces: `HelpViewModel` with public string properties `NMaxText`,
  `NMinText`, `HMaxText`, `HMinText`, `MiText`, `PpNumText`,
  `GeneratedCommandText` (all start as `""`); `GenerateCommand` (an
  `IRelayCommand`, `CanExecute` gated); `ResetCommand` (an
  `IRelayCommand`, always executable). Task 2 (`HelpWindow`) binds
  `TextBox.Text` to the six input properties and `Command="{Binding
  GenerateCommand}"`/`Command="{Binding ResetCommand}"` to the buttons.

- [ ] **Step 1: Write the failing tests**

Create `dotnet/CspAnalyzer.Desktop.Tests/HelpViewModelTests.cs`:

```csharp
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class HelpViewModelTests
{
    private static HelpViewModel MakeValidViewModel() => new()
    {
        NMaxText = "135",
        NMinText = "105",
        HMaxText = "11",
        HMinText = "6",
        MiText = "0.0001",
        PpNumText = "90",
    };

    [Fact]
    public void GenerateCommand_CanExecute_FalseWhenAnyFieldEmpty()
    {
        var vm = MakeValidViewModel();
        vm.PpNumText = "";

        Assert.False(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void GenerateCommand_CanExecute_FalseWhenFieldNonNumeric()
    {
        var vm = MakeValidViewModel();
        vm.PpNumText = "ninety";

        Assert.False(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void GenerateCommand_CanExecute_TrueWhenAllSixFieldsValid()
    {
        var vm = MakeValidViewModel();

        Assert.True(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void Generate_BuildsExpectedTopSpinCommandString()
    {
        var vm = MakeValidViewModel();

        vm.GenerateCommand.Execute(null);

        Assert.Equal(
            "1 F1P 135; 2 F1P 11; 1 F2P 105; 2 F2P 6; MI 0.0001; PPNUM 90; pp2d nodia",
            vm.GeneratedCommandText);
    }

    [Fact]
    public void ResetCommand_ClearsAllInputsAndGeneratedText()
    {
        var vm = MakeValidViewModel();
        vm.GenerateCommand.Execute(null);

        vm.ResetCommand.Execute(null);

        Assert.Equal("", vm.NMaxText);
        Assert.Equal("", vm.NMinText);
        Assert.Equal("", vm.HMaxText);
        Assert.Equal("", vm.HMinText);
        Assert.Equal("", vm.MiText);
        Assert.Equal("", vm.PpNumText);
        Assert.Equal("", vm.GeneratedCommandText);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter HelpViewModelTests`
Expected: FAIL (build error) — `HelpViewModel` does not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `dotnet/CspAnalyzer.Desktop/ViewModels/HelpViewModel.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S11d: backs HelpWindow's TopSpin peak-picking command generator, ported
/// from CSPv2/FormHelp.cs's button_generate/button1_Click/button2_Click.
/// Plain ObservableObject (no Avalonia dependency) so it's unit-testable
/// without a headless window - HelpWindow instantiates it directly since
/// it has no external dependencies to inject.
/// </summary>
public partial class HelpViewModel : ObservableObject
{
    [ObservableProperty]
    private string _nMaxText = "";

    [ObservableProperty]
    private string _nMinText = "";

    [ObservableProperty]
    private string _hMaxText = "";

    [ObservableProperty]
    private string _hMinText = "";

    [ObservableProperty]
    private string _miText = "";

    [ObservableProperty]
    private string _ppNumText = "";

    [ObservableProperty]
    private string _generatedCommandText = "";

    partial void OnNMaxTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnNMinTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnHMaxTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnHMinTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnMiTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnPpNumTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();

    private bool CanGenerate() =>
        double.TryParse(NMaxText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(NMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(HMaxText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(HMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(MiText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        int.TryParse(PpNumText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private void Generate()
    {
        GeneratedCommandText =
            $"1 F1P {NMaxText}; 2 F1P {HMaxText}; 1 F2P {NMinText}; 2 F2P {HMinText}; MI {MiText}; PPNUM {PpNumText}; pp2d nodia";
    }

    [RelayCommand]
    private void Reset()
    {
        NMaxText = "";
        NMinText = "";
        HMaxText = "";
        HMinText = "";
        MiText = "";
        PpNumText = "";
        GeneratedCommandText = "";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter HelpViewModelTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS (all tests, including the 5 new ones).

- [ ] **Step 6: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/HelpViewModel.cs dotnet/CspAnalyzer.Desktop.Tests/HelpViewModelTests.cs
git commit -m "S11d: HelpViewModel for TopSpin command generator"
```

---

### Task 2: `HelpWindow` view (static content + generator UI)

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Views/HelpWindow.axaml`
- Create: `dotnet/CspAnalyzer.Desktop/Views/HelpWindow.axaml.cs`
- Test: `dotnet/CspAnalyzer.Desktop.Tests/HelpWindowTests.cs`

**Interfaces:**
- Consumes: `HelpViewModel` (Task 1) — its six input properties,
  `GenerateCommand`, `ResetCommand`, `GeneratedCommandText`.
- Produces: `CspAnalyzer.Desktop.Views.HelpWindow` (a `Window`,
  parameterless constructor, sets its own `DataContext = new
  HelpViewModel()`). Task 3's `AvaloniaHelpWindowService` constructs
  `new HelpWindow(); window.Show(owner);` — same shape as
  `AvaloniaShortcutsWindowService`.

- [ ] **Step 1: Write the failing tests**

Create `dotnet/CspAnalyzer.Desktop.Tests/HelpWindowTests.cs`:

```csharp
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

        string? clipboardText = await TopLevel.GetTopLevel(window)!.Clipboard!.GetTextAsync();
        Assert.Equal(
            "1 F1P 135; 2 F1P 11; 1 F2P 105; 2 F2P 6; MI 0.0001; PPNUM 90; pp2d nodia",
            clipboardText);
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter HelpWindowTests`
Expected: FAIL (build error) — `HelpWindow` does not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `dotnet/CspAnalyzer.Desktop/Views/HelpWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="CspAnalyzer.Desktop.Views.HelpWindow"
        Title="Help"
        Width="700" Height="700"
        WindowStartupLocation="CenterOwner">
    <ScrollViewer>
        <StackPanel Margin="16" Spacing="10">
            <TextBlock Text="Help" FontSize="16" FontWeight="Bold" HorizontalAlignment="Center" Margin="0,0,0,8" />

            <TextBlock Text="Tips and Tricks" FontWeight="Bold" FontSize="14" />
            <TextBlock Text="Common issues" FontWeight="SemiBold" />

            <TextBlock Text="1. No actives found after analysis" FontWeight="SemiBold" Margin="0,4,0,0" />
            <TextBlock TextWrapping="Wrap" Text="This may be due to several causes, but most probably to incorrect or lousy peak picking - the classifier's scaler/PCA/SVM pipeline wasn't trained to separate feature vectors built from badly-picked peaks. We are aware that peak picking should be handled in a full-stack application; we are working on it!" />

            <TextBlock Text="2. Memory error exception(s)" FontWeight="SemiBold" Margin="0,8,0,0" />
            <TextBlock TextWrapping="Wrap" Text="This is due to the csp_modern conda environment the backend runs in. Try to free some memory and rerun the software. We chose to build our own Python environment for ease of deployability of this and future upgrades; the downside is that this requires more memory." />

            <TextBlock Text="3. Unable to display analysis results" FontWeight="SemiBold" Margin="0,8,0,0" />
            <TextBlock TextWrapping="Wrap" Text="Check that the CSP Analyzer has permission to access your user temporary folder, or try running the executable with administrative rights." />

            <TextBlock Text="Tricks" FontWeight="Bold" FontSize="14" Margin="0,12,0,0" />
            <TextBlock Text="Peak picking in TopSpin" FontWeight="SemiBold" />
            <TextBlock TextWrapping="Wrap" Text="Because the CSP Analyzer relies on TopSpin peak picking, this must be done appropriately. Of course, the peak picking is strictly dependent on the experiment and there is no easy way to standardize this operation. But we have a workaround. This is how you can have a relatively fast and efficient peak picking done in TopSpin." />
            <TextBlock FontFamily="monospace" Text="1 F1P 135; 2 F1P 11; 1 F2P 105; 2 F2P 6; MI 0.0001; PPNUM 90; pp2d nodia" />

            <StackPanel Orientation="Horizontal" Spacing="12" Margin="0,8,0,0">
                <StackPanel Spacing="2">
                    <TextBlock Text="15N-High" FontSize="11" />
                    <TextBox Name="NMaxTextBox" Width="60" Text="{Binding NMaxText}" />
                </StackPanel>
                <StackPanel Spacing="2">
                    <TextBlock Text="1H-High" FontSize="11" />
                    <TextBox Name="HMaxTextBox" Width="60" Text="{Binding HMaxText}" />
                </StackPanel>
                <StackPanel Spacing="2">
                    <TextBlock Text="15N-Low" FontSize="11" />
                    <TextBox Name="NMinTextBox" Width="60" Text="{Binding NMinText}" />
                </StackPanel>
                <StackPanel Spacing="2">
                    <TextBlock Text="1H-Low" FontSize="11" />
                    <TextBox Name="HMinTextBox" Width="60" Text="{Binding HMinText}" />
                </StackPanel>
                <StackPanel Spacing="2">
                    <TextBlock Text="Min. Intensity" FontSize="11" />
                    <TextBox Name="MiTextBox" Width="70" Text="{Binding MiText}" />
                </StackPanel>
                <StackPanel Spacing="2">
                    <TextBlock Text="Desired Peaks" FontSize="11" />
                    <TextBox Name="PpNumTextBox" Width="60" Text="{Binding PpNumText}" />
                </StackPanel>
            </StackPanel>

            <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
                <Button Content="Generate" Command="{Binding GenerateCommand}" />
                <Button Content="Reset" Command="{Binding ResetCommand}" />
            </StackPanel>

            <TextBox Name="GeneratedCommandTextBox" Text="{Binding GeneratedCommandText}" IsReadOnly="True" Margin="0,8,0,0" />
            <Button Content="Copy" Click="OnCopyClicked" HorizontalAlignment="Left" />

            <TextBlock Text="Peak lists extractor" FontWeight="SemiBold" Margin="0,12,0,0" />
            <TextBlock TextWrapping="Wrap" Text="This small app will retrieve just the peaklist.xml files, keeping the same folder-tree used by TopSpin, and copy them to a custom path, so you won't have to copy the whole experiments folders (which saves precious disk space). Note that if 1-D experiments were peak-picked, these will be included in the exported folder." />
        </StackPanel>
    </ScrollViewer>
</Window>
```

Create `dotnet/CspAnalyzer.Desktop/Views/HelpWindow.axaml.cs`:

```csharp
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

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HelpViewModel vm && !string.IsNullOrEmpty(vm.GeneratedCommandText))
        {
            TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(vm.GeneratedCommandText);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter HelpWindowTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/HelpWindow.axaml dotnet/CspAnalyzer.Desktop/Views/HelpWindow.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/HelpWindowTests.cs
git commit -m "S11d: HelpWindow view - tips/tricks content + TopSpin generator UI"
```

---

### Task 3: `IHelpWindowService` + `MainViewModel` wiring

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Services/IHelpWindowService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/AvaloniaHelpWindowService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/NullHelpWindowService.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.SecondaryWindows.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/App.axaml.cs`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`

**Interfaces:**
- Consumes: `HelpWindow` (Task 2).
- Produces: `MainViewModel.OpenHelpWindowCommand` (an `IRelayCommand`) and
  a 6th `MainViewModel` constructor parameter `IHelpWindowService
  helpWindowService` (appended after `shortcutsWindowService`). Task 4
  (`MainWindow` Help button + `H` keybinding) binds to
  `OpenHelpWindowCommand`.

- [ ] **Step 1: Write the failing test**

Add to `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs`
— add a nested recording fake and a test, alongside the existing
`RecordingAboutWindowService`/`RecordingShortcutsWindowService`:

```csharp
    private sealed class RecordingHelpWindowService : IHelpWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }
```

```csharp
    [Fact]
    public void OpenHelpWindowCommand_CallsHelpWindowServiceShow()
    {
        var helpService = new RecordingHelpWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), helpService);

        vm.OpenHelpWindowCommand.Execute(null);

        Assert.Equal(1, helpService.ShowCallCount);
    }
```

Also update the file's two *existing* `new MainViewModel(...)` calls
(they'll fail to compile once the constructor gains a 6th required
parameter) — append `, new NullHelpWindowService()` before the closing
paren in both `OpenAboutWindowCommand_CallsAboutWindowServiceShow` and
`OpenShortcutsWindowCommand_CallsShortcutsWindowServiceShow`:

```csharp
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            aboutService, new NullShortcutsWindowService(), new NullHelpWindowService());
```

```csharp
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), shortcutsService, new NullHelpWindowService());
```

Two more call sites elsewhere in the test project also need the same
6th argument (they use the explicit 5-arg constructor and will stop
compiling otherwise):

In `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs`,
find:
```csharp
        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService());
```
replace with:
```csharp
        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService());
```

In `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs`,
find:
```csharp
        var vm = new MainViewModel(new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService());
```
replace with:
```csharp
        var vm = new MainViewModel(new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService());
```

In `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`,
find (inside `CtrlK_OpensShortcutsWindow`):
```csharp
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), recording);
```
replace with:
```csharp
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), recording, new NullHelpWindowService());
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainViewModelSecondaryWindowsTests`
Expected: FAIL (build error) — `IHelpWindowService`/`NullHelpWindowService`
don't exist, and `MainViewModel`'s constructor doesn't accept a 6th
argument yet.

- [ ] **Step 3: Write minimal implementation**

Create `dotnet/CspAnalyzer.Desktop/Services/IHelpWindowService.cs`:

```csharp
namespace CspAnalyzer.Desktop.Services;

/// <summary>Opens the Help window (S11d) - mirrors IAboutWindowService/IShortcutsWindowService's reasoning: keeps MainViewModel usable with no live Window (design-time, tests).</summary>
public interface IHelpWindowService
{
    void Show();
}
```

Create `dotnet/CspAnalyzer.Desktop/Services/AvaloniaHelpWindowService.cs`:

```csharp
using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaHelpWindowService(Window owner) : IHelpWindowService
{
    public void Show()
    {
        var window = new HelpWindow();
        window.Show(owner);
    }
}
```

Create `dotnet/CspAnalyzer.Desktop/Services/NullHelpWindowService.cs`:

```csharp
namespace CspAnalyzer.Desktop.Services;

/// <summary>No-op for the Avalonia design-time DataContext, where no real window exists.</summary>
public sealed class NullHelpWindowService : IHelpWindowService
{
    public void Show()
    {
    }
}
```

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, find the
field block:
```csharp
    private readonly IFilePickerService _filePicker;
    private readonly IResultsWindowService _resultsWindowService;
    private readonly IConfirmDialogService _confirmDialogService;
    private readonly IAboutWindowService _aboutWindowService;
    private readonly IShortcutsWindowService _shortcutsWindowService;
```
replace with:
```csharp
    private readonly IFilePickerService _filePicker;
    private readonly IResultsWindowService _resultsWindowService;
    private readonly IConfirmDialogService _confirmDialogService;
    private readonly IAboutWindowService _aboutWindowService;
    private readonly IShortcutsWindowService _shortcutsWindowService;
    private readonly IHelpWindowService _helpWindowService;
```

Find the two constructors:
```csharp
    public MainViewModel() : this(
        new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
        new NullAboutWindowService(), new NullShortcutsWindowService())
    {
    }

    public MainViewModel(
        IFilePickerService filePicker,
        IResultsWindowService resultsWindowService,
        IConfirmDialogService confirmDialogService,
        IAboutWindowService aboutWindowService,
        IShortcutsWindowService shortcutsWindowService)
    {
        _filePicker = filePicker;
        _resultsWindowService = resultsWindowService;
        _confirmDialogService = confirmDialogService;
        _aboutWindowService = aboutWindowService;
        _shortcutsWindowService = shortcutsWindowService;
    }
```
replace with:
```csharp
    public MainViewModel() : this(
        new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
        new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService())
    {
    }

    public MainViewModel(
        IFilePickerService filePicker,
        IResultsWindowService resultsWindowService,
        IConfirmDialogService confirmDialogService,
        IAboutWindowService aboutWindowService,
        IShortcutsWindowService shortcutsWindowService,
        IHelpWindowService helpWindowService)
    {
        _filePicker = filePicker;
        _resultsWindowService = resultsWindowService;
        _confirmDialogService = confirmDialogService;
        _aboutWindowService = aboutWindowService;
        _shortcutsWindowService = shortcutsWindowService;
        _helpWindowService = helpWindowService;
    }
```

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.SecondaryWindows.cs`,
find:
```csharp
    [RelayCommand]
    private void OpenShortcutsWindow() => _shortcutsWindowService.Show();
}
```
replace with:
```csharp
    [RelayCommand]
    private void OpenShortcutsWindow() => _shortcutsWindowService.Show();

    [RelayCommand]
    private void OpenHelpWindow() => _helpWindowService.Show();
}
```

In `dotnet/CspAnalyzer.Desktop/App.axaml.cs`, find:
```csharp
            var viewModel = new MainViewModel(
                new AvaloniaFilePickerService(window),
                new AvaloniaResultsWindowService(window),
                new AvaloniaConfirmDialogService(window),
                new AvaloniaAboutWindowService(window),
                new AvaloniaShortcutsWindowService(window));
```
replace with:
```csharp
            var viewModel = new MainViewModel(
                new AvaloniaFilePickerService(window),
                new AvaloniaResultsWindowService(window),
                new AvaloniaConfirmDialogService(window),
                new AvaloniaAboutWindowService(window),
                new AvaloniaShortcutsWindowService(window),
                new AvaloniaHelpWindowService(window));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainViewModelSecondaryWindowsTests`
Expected: PASS (4 tests: 2 existing About/Shortcuts + the new Help one +
`ResetAllImportAndThresholdControlsCommand`'s existing test).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS — this step specifically confirms
`MainViewModelNavigationTests`, `MainViewModelManualOverrideTests`, and
`MainWindowKeyBindingsTests` still compile and pass after their
constructor-call-site edits.

- [ ] **Step 6: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Services/IHelpWindowService.cs dotnet/CspAnalyzer.Desktop/Services/AvaloniaHelpWindowService.cs dotnet/CspAnalyzer.Desktop/Services/NullHelpWindowService.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.SecondaryWindows.cs dotnet/CspAnalyzer.Desktop/App.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
git commit -m "S11d: IHelpWindowService + MainViewModel.OpenHelpWindowCommand wiring"
```

---

### Task 4: `MainWindow` Help button + guarded `H` keybinding

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml`
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`

**Interfaces:**
- Consumes: `MainViewModel.OpenHelpWindowCommand` (Task 3),
  `GuardedViewModelCommand` (existing private method in
  `MainWindow.axaml.cs`).
- Produces: nothing further downstream — this is the last functional
  wiring task.

- [ ] **Step 1: Write the failing tests**

In `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`, add
a `RecordingHelpWindowService` nested class (mirroring the existing
`RecordingShortcutsWindowService` at the bottom of the file) and two
tests mirroring the `G` guard pair. Find:

```csharp
    private sealed class RecordingShortcutsWindowService : IShortcutsWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }
}
```

replace with:

```csharp
    private sealed class RecordingShortcutsWindowService : IShortcutsWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    private sealed class RecordingHelpWindowService : IHelpWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    [AvaloniaFact]
    public void H_OpensHelpWindow()
    {
        var recording = new RecordingHelpWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), recording);
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.H, RawInputModifiers.None);

        Assert.Equal(1, recording.ShowCallCount);
    }

    [AvaloniaFact]
    public void H_GuardedWhileTextBoxFocused_DoesNotOpenHelpWindow()
    {
        var recording = new RecordingHelpWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), recording);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var nMinBox = window.FindControl<TextBox>("NMinTextBox")!;
        nMinBox.Focus();

        window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.H, RawInputModifiers.None);

        Assert.Equal(0, recording.ShowCallCount);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainWindowKeyBindingsTests`
Expected: FAIL — `H_OpensHelpWindow` gets `ShowCallCount == 0` (no `H`
binding exists yet); `H_GuardedWhileTextBoxFocused_DoesNotOpenHelpWindow`
passes vacuously (also a signal nothing is wired) but the first failure
is what confirms the test actually exercises new behavior.

- [ ] **Step 3: Write minimal implementation**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml`, find (line 173):
```xml
                        <Button Content="Help" />
```
replace with:
```xml
                        <Button Content="Help" Command="{Binding OpenHelpWindowCommand}" />
```

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs`, find:
```csharp
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.X, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.FitOverlayZoomToReferenceCommand) });
```
replace with:
```csharp
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.X, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.FitOverlayZoomToReferenceCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.H), Command = GuardedViewModelCommand(vm => vm.OpenHelpWindowCommand) });
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainWindowKeyBindingsTests`
Expected: PASS (all tests in the file, including the 2 new ones).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
git commit -m "S11d: wire Help button + guarded H keybinding to OpenHelpWindowCommand"
```

---

### Task 5: `ShortcutsWindow` documentation update

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs`

**Interfaces:**
- Consumes: nothing (pure documentation update, no code dependency —
  can technically run anytime after Task 4, placed last since it
  documents Task 4's new behavior).
- Produces: nothing consumed elsewhere.

- [ ] **Step 1: Write the failing test**

In `dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs`, find:
```csharp
        Assert.Contains(texts, t => t.Contains("Next Spectrum"));
        Assert.Contains(texts, t => t.Contains("Right"));
        Assert.Contains(texts, t => t.Contains("Show Auto Actives") && t.Contains("not yet implemented"));
        Assert.Contains(texts, t => t.Contains("Export To Excel"));
```
replace with:
```csharp
        Assert.Contains(texts, t => t.Contains("Next Spectrum"));
        Assert.Contains(texts, t => t.Contains("Right"));
        Assert.Contains(texts, t => t.Contains("Show Auto Actives") && t.Contains("not yet implemented"));
        Assert.Contains(texts, t => t.Contains("Export To Excel"));
        Assert.Contains(texts, t => t == "H");
        Assert.Contains(texts, t => t.Contains("Show Help Guide") && !t.Contains("not yet implemented"));
        Assert.Contains(texts, t => t.Contains("Show Information Window") && t.Contains("not yet implemented"));
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter ShortcutsWindowTests`
Expected: FAIL — the current row 18 text is `"Load Reference/Dataset,
Show Help Guide, Show Information Window (not yet implemented)"`, a
single `TextBlock` whose `Show Help Guide` substring is still bundled
with `"not yet implemented"`.

- [ ] **Step 3: Write minimal implementation**

In `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml`, find:
```xml
            <TextBlock Text="Loading Data/Processing" FontWeight="SemiBold" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="R" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Run CSP Analysis" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+K" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Show Keyboard Shortcut Window" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Enter, H, I" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Load Reference/Dataset, Show Help Guide, Show Information Window (not yet implemented)" TextWrapping="Wrap" />
            </Grid>
```
replace with:
```xml
            <TextBlock Text="Loading Data/Processing" FontWeight="SemiBold" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="R" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Run CSP Analysis" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+K" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Show Keyboard Shortcut Window" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="H" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Show Help Guide" />
                <TextBlock Grid.Row="3" Grid.Column="0" Text="Enter, I" />
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Load Reference/Dataset, Show Information Window (not yet implemented)" TextWrapping="Wrap" />
            </Grid>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter ShortcutsWindowTests`
Expected: PASS.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS — full suite green, S11d complete.

- [ ] **Step 6: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs
git commit -m "S11d: ShortcutsWindow - H now documented as wired, split from Enter/I row"
```

---

## Post-implementation

Update `docs/superpowers/SESSIONS.md`: check off **S11d**, add a summary
paragraph (content reworded vs. legacy, generator validation approach,
`H` keybinding, any review findings) following the style of the S11c
entry immediately above it.
