# S11c Keyboard Shortcuts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire real `Window.KeyBindings` for every legacy shortcut that maps to a command this app already has, add a Shortcuts window documenting all of them (wired and not-yet-implemented), and wire the sidebar's inert About button to a minimal About window.

**Architecture:** Two new modeless windows (`AboutWindow`, `ShortcutsWindow`) follow the existing `IResultsWindowService`-style interface+Avalonia-impl+Null-impl pattern. `MainWindow`/`ResultsWindow` get `<Window.KeyBindings>` blocks bound to existing/new `MainViewModel`/`ResultsViewModel` commands via `{Binding}`; two window-only actions (Ctrl+Q close, G focus-textbox) are wired imperatively in code-behind since they have no ViewModel state.

**Tech Stack:** .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm, Avalonia.Headless.XUnit (using `KeyPressQwerty`/`KeyReleaseQwerty` for the first time in this codebase to simulate real physical key input).

## Global Constraints

- Every `KeyBinding` added must map to a real, already-existing (or this-session-added) command — never invent a feature just to have something to bind. Legacy shortcuts with no real target are documented in `ShortcutsWindow` as "Not yet implemented," never silently dropped and never wired to a no-op.
- The Help window and its TopSpin command generator are explicitly out of scope (deferred session).
- New services follow the exact `IConfirmDialogService`/`IResultsWindowService` pattern: one interface with a single method, one `Avalonia*` implementation taking a `Window owner` constructor parameter, one `Null*` implementation that no-ops, used via constructor injection into `MainViewModel` with the `Null*` variant as the parameterless constructor's default.
- Bare single-letter `KeyBinding`s (R, T, N, D, S, A, G) must not fire while a `TextBox` has keyboard focus and the user is typing normal text — this must be verified with an explicit test, not assumed.
- `KeyGesture` strings use Avalonia's `"Ctrl+Alt+R"` format (no spaces) via `KeyGesture.Parse`/XAML's `Gesture="..."` attribute.

---

### Task 1: `MainViewModel` new commands + `IAboutWindowService`/`IShortcutsWindowService`

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Services/IAboutWindowService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/NullAboutWindowService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/IShortcutsWindowService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/NullShortcutsWindowService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.SecondaryWindows.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (constructor signature, lines 122-131)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs`

**Interfaces:**
- Produces: `IAboutWindowService { void Show(); }`, `IShortcutsWindowService { void Show(); }`, both with `Null*` no-op implementations. `MainViewModel` constructor becomes `MainViewModel(IFilePickerService filePicker, IResultsWindowService resultsWindowService, IConfirmDialogService confirmDialogService, IAboutWindowService aboutWindowService, IShortcutsWindowService shortcutsWindowService)` — the parameterless constructor passes `new NullAboutWindowService()`/`new NullShortcutsWindowService()` for the two new parameters. New commands: `OpenAboutWindowCommand`, `OpenShortcutsWindowCommand` (both plain `IRelayCommand`, no `CanExecute`), and `ResetAllImportAndThresholdControlsCommand` (plain `IRelayCommand`). Tasks 2-5 depend on these exact names.

- [ ] **Step 1: Write the failing tests**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelSecondaryWindowsTests
{
    private sealed class RecordingAboutWindowService : IAboutWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    private sealed class RecordingShortcutsWindowService : IShortcutsWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    [Fact]
    public void OpenAboutWindowCommand_CallsAboutWindowServiceShow()
    {
        var aboutService = new RecordingAboutWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            aboutService, new NullShortcutsWindowService());

        vm.OpenAboutWindowCommand.Execute(null);

        Assert.Equal(1, aboutService.ShowCallCount);
    }

    [Fact]
    public void OpenShortcutsWindowCommand_CallsShortcutsWindowServiceShow()
    {
        var shortcutsService = new RecordingShortcutsWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), shortcutsService);

        vm.OpenShortcutsWindowCommand.Execute(null);

        Assert.Equal(1, shortcutsService.ShowCallCount);
    }

    [Fact]
    public void ResetAllImportAndThresholdControlsCommand_ResetsAllSixFieldsToHardcodedDefaults()
    {
        var vm = new MainViewModel
        {
            NMin = 1,
            NMax = 2,
            HMin = 3,
            HMax = 4,
            ReferenceIntensityThreshold = 999,
            DatasetIntensityThreshold = 888,
        };

        vm.ResetAllImportAndThresholdControlsCommand.Execute(null);

        Assert.Equal(100, vm.NMin);
        Assert.Equal(140, vm.NMax);
        Assert.Equal(5, vm.HMin);
        Assert.Equal(12, vm.HMax);
        Assert.Equal(5000, vm.ReferenceIntensityThreshold);
        Assert.Equal(2000, vm.DatasetIntensityThreshold);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainViewModelSecondaryWindowsTests`
Expected: FAIL / build error — `IAboutWindowService`, `IShortcutsWindowService`, the new constructor overload, and the new commands don't exist yet.

- [ ] **Step 3: Create the two new service interfaces + Null implementations**

```csharp
// dotnet/CspAnalyzer.Desktop/Services/IAboutWindowService.cs
namespace CspAnalyzer.Desktop.Services;

/// <summary>Opens the About window (S11c) - mirrors IResultsWindowService's reasoning: keeps MainViewModel usable with no live Window (design-time, tests).</summary>
public interface IAboutWindowService
{
    void Show();
}
```

```csharp
// dotnet/CspAnalyzer.Desktop/Services/NullAboutWindowService.cs
namespace CspAnalyzer.Desktop.Services;

/// <summary>No-op for the Avalonia design-time DataContext, where no real window exists.</summary>
public sealed class NullAboutWindowService : IAboutWindowService
{
    public void Show()
    {
    }
}
```

```csharp
// dotnet/CspAnalyzer.Desktop/Services/IShortcutsWindowService.cs
namespace CspAnalyzer.Desktop.Services;

/// <summary>Opens the Shortcuts window (S11c) - mirrors IResultsWindowService's reasoning: keeps MainViewModel usable with no live Window (design-time, tests).</summary>
public interface IShortcutsWindowService
{
    void Show();
}
```

```csharp
// dotnet/CspAnalyzer.Desktop/Services/NullShortcutsWindowService.cs
namespace CspAnalyzer.Desktop.Services;

/// <summary>No-op for the Avalonia design-time DataContext, where no real window exists.</summary>
public sealed class NullShortcutsWindowService : IShortcutsWindowService
{
    public void Show()
    {
    }
}
```

- [ ] **Step 4: Update `MainViewModel`'s constructors**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, replace:

```csharp
    public MainViewModel() : this(new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService())
    {
    }

    public MainViewModel(IFilePickerService filePicker, IResultsWindowService resultsWindowService, IConfirmDialogService confirmDialogService)
    {
        _filePicker = filePicker;
        _resultsWindowService = resultsWindowService;
        _confirmDialogService = confirmDialogService;
    }
```

with:

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

Add the two new readonly fields next to the existing three (around line 37):

```csharp
    private readonly IAboutWindowService _aboutWindowService;
    private readonly IShortcutsWindowService _shortcutsWindowService;
```

- [ ] **Step 5: Create the new partial file with the two Open*WindowCommands**

```csharp
// dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.SecondaryWindows.cs
using CommunityToolkit.Mvvm.Input;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>S11c: opens the About/Shortcuts windows, mirroring OpenResultsWindow's service-call pattern in MainViewModel.cs.</summary>
public partial class MainViewModel
{
    [RelayCommand]
    private void OpenAboutWindow() => _aboutWindowService.Show();

    [RelayCommand]
    private void OpenShortcutsWindow() => _shortcutsWindowService.Show();
}
```

- [ ] **Step 6: Add `ResetAllImportAndThresholdControlsCommand` next to the two commands it composes**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, immediately after the existing `ResetPeakFiltering` method (after line 351, i.e. right after its closing brace), add:

```csharp

    [RelayCommand]
    private void ResetAllImportAndThresholdControls()
    {
        ResetImportControls();
        ResetPeakFiltering();
    }
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainViewModelSecondaryWindowsTests`
Expected: PASS (3/3)

- [ ] **Step 8: Update `App.axaml.cs`'s constructor call so the solution still builds**

`CspAnalyzer.Desktop.Tests` references `CspAnalyzer.Desktop`, so `App.axaml.cs` must compile against `MainViewModel`'s new 5-argument constructor even though the real `AvaloniaAboutWindowService`/`AvaloniaShortcutsWindowService` don't exist until Tasks 2/3 — use the `Null*` implementations as temporary placeholders here; Tasks 2 and 3 each swap one placeholder for its real service as it's built.

In `dotnet/CspAnalyzer.Desktop/App.axaml.cs`, change:

```csharp
            var viewModel = new MainViewModel(
                new AvaloniaFilePickerService(window),
                new AvaloniaResultsWindowService(window),
                new AvaloniaConfirmDialogService(window));
```

to:

```csharp
            var viewModel = new MainViewModel(
                new AvaloniaFilePickerService(window),
                new AvaloniaResultsWindowService(window),
                new AvaloniaConfirmDialogService(window),
                new NullAboutWindowService(),
                new NullShortcutsWindowService());
```

- [ ] **Step 9: Run the full test suite to check for regressions**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS, no regressions.

- [ ] **Step 10: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Services/IAboutWindowService.cs dotnet/CspAnalyzer.Desktop/Services/NullAboutWindowService.cs dotnet/CspAnalyzer.Desktop/Services/IShortcutsWindowService.cs dotnet/CspAnalyzer.Desktop/Services/NullShortcutsWindowService.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.SecondaryWindows.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop/App.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs
git commit -m "S11c: MainViewModel Open*WindowCommands + IAboutWindowService/IShortcutsWindowService"
```

---

### Task 2: `AboutWindow` + `AvaloniaAboutWindowService`

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Views/AboutWindow.axaml`
- Create: `dotnet/CspAnalyzer.Desktop/Views/AboutWindow.axaml.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/AvaloniaAboutWindowService.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml:125` (the inert "About" button)
- Modify: `dotnet/CspAnalyzer.Desktop/App.axaml.cs` (constructor arguments)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/AboutWindowTests.cs`

**Interfaces:**
- Consumes: `IAboutWindowService` (Task 1), `MainViewModel.OpenAboutWindowCommand` (Task 1).
- Produces: `AboutWindow` (Avalonia `Window`, no custom constructor logic needed beyond `InitializeComponent()`), `AvaloniaAboutWindowService(Window owner) : IAboutWindowService`.

- [ ] **Step 1: Write the failing test**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/AboutWindowTests.cs
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter AboutWindowTests`
Expected: FAIL / build error — `AboutWindow` doesn't exist yet.

- [ ] **Step 3: Create `AboutWindow.axaml`**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="CspAnalyzer.Desktop.Views.AboutWindow"
        Title="About CSP Analyzer"
        Width="360" SizeToContent="Height"
        CanResize="False"
        WindowStartupLocation="CenterOwner">
    <StackPanel Margin="20" Spacing="8">
        <TextBlock Text="CSP Analyzer" FontSize="18" FontWeight="Bold" HorizontalAlignment="Center" />
        <TextBlock x:Name="VersionText" HorizontalAlignment="Center" FontSize="11" />
        <TextBlock Text="Developed by R. Byrne and R. Fino" HorizontalAlignment="Center" FontStyle="Italic" TextWrapping="Wrap" TextAlignment="Center" />
    </StackPanel>
</Window>
```

- [ ] **Step 4: Create `AboutWindow.axaml.cs`**

```csharp
// dotnet/CspAnalyzer.Desktop/Views/AboutWindow.axaml.cs
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
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter AboutWindowTests`
Expected: PASS (1/1)

- [ ] **Step 6: Create `AvaloniaAboutWindowService`**

```csharp
// dotnet/CspAnalyzer.Desktop/Services/AvaloniaAboutWindowService.cs
using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaAboutWindowService(Window owner) : IAboutWindowService
{
    public void Show()
    {
        var window = new AboutWindow();
        window.Show(owner);
    }
}
```

- [ ] **Step 7: Wire the sidebar's "About" button**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml`, change line 125 from:

```xml
                        <Button Content="About" />
```

to:

```xml
                        <Button Content="About" Command="{Binding OpenAboutWindowCommand}" />
```

- [ ] **Step 8: Swap the `NullAboutWindowService` placeholder for the real one in `App.axaml.cs`**

Task 1 left `App.axaml.cs` passing `new NullAboutWindowService()` as a
build-fixing placeholder. In `dotnet/CspAnalyzer.Desktop/App.axaml.cs`,
change:

```csharp
                new NullAboutWindowService(),
                new NullShortcutsWindowService());
```

to:

```csharp
                new AvaloniaAboutWindowService(window),
                new NullShortcutsWindowService());
```

(The Shortcuts placeholder stays until Task 3, which swaps it the same way.)

- [ ] **Step 9: Run the full test suite to check for regressions**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS, no regressions.

- [ ] **Step 10: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/AboutWindow.axaml dotnet/CspAnalyzer.Desktop/Views/AboutWindow.axaml.cs dotnet/CspAnalyzer.Desktop/Services/AvaloniaAboutWindowService.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/App.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/AboutWindowTests.cs
git commit -m "S11c: AboutWindow + AvaloniaAboutWindowService, wire sidebar About button"
```

---

### Task 3: `ShortcutsWindow` + `AvaloniaShortcutsWindowService`

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml`
- Create: `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/AvaloniaShortcutsWindowService.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml:126` (the inert "Shortcuts" button)
- Modify: `dotnet/CspAnalyzer.Desktop/App.axaml.cs` (constructor arguments — completes Task 2's deferred step)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs`

**Interfaces:**
- Consumes: `IShortcutsWindowService` (Task 1), `MainViewModel.OpenShortcutsWindowCommand` (Task 1), `AvaloniaAboutWindowService` (Task 2, for the combined `App.axaml.cs` edit).
- Produces: `ShortcutsWindow`, `AvaloniaShortcutsWindowService(Window owner) : IShortcutsWindowService`.

This is the full reference table from the spec's mapping - every row from both the MainWindow and ResultsWindow tables, grouped by category, with **Wired** rows shown plainly and the unmapped ones suffixed "(not yet implemented)".

- [ ] **Step 1: Write the failing test**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs
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
        Assert.Contains(texts, t => t.Contains("Show Auto Actives") && t.Contains("not yet implemented"));
        Assert.Contains(texts, t => t.Contains("Export To Excel"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter ShortcutsWindowTests`
Expected: FAIL / build error — `ShortcutsWindow` doesn't exist yet.

- [ ] **Step 3: Create `ShortcutsWindow.axaml`**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="CspAnalyzer.Desktop.Views.ShortcutsWindow"
        Title="Keyboard Shortcuts"
        Width="520" Height="640"
        WindowStartupLocation="CenterOwner">
    <ScrollViewer>
        <StackPanel Margin="16" Spacing="10">
            <TextBlock Text="Keyboard Shortcuts" FontSize="16" FontWeight="Bold" HorizontalAlignment="Center" Margin="0,0,0,8" />

            <TextBlock Text="Loading Data/Processing" FontWeight="SemiBold" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="R" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Run CSP Analysis" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+K" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Show Keyboard Shortcut Window" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Enter, H, I" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Load Reference/Dataset, Show Help Guide, Show Information Window (not yet implemented)" TextWrapping="Wrap" />
            </Grid>

            <TextBlock Text="Player" FontWeight="SemiBold" Margin="0,8,0,0" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="Right" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Next Spectrum" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Left" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Previous Spectrum" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Down" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Last Spectrum" />
                <TextBlock Grid.Row="3" Grid.Column="0" Text="Up" />
                <TextBlock Grid.Row="3" Grid.Column="1" Text="First Spectrum" />
            </Grid>

            <TextBlock Text="Reference/Dataset info" FontWeight="SemiBold" Margin="0,8,0,0" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="Ctrl+Alt+R" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Show Reference PP info" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+Alt+E" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Show Current Exp. PP info" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Ctrl+Alt+O" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Show Out-of-Import Range Exp. (not yet implemented)" TextWrapping="Wrap" />
                <TextBlock Grid.Row="3" Grid.Column="0" Text="Ctrl+Alt+F" />
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Show Corrupted Peaklist Exp. (not yet implemented)" TextWrapping="Wrap" />
            </Grid>

            <TextBlock Text="Spectra Overlay" FontWeight="SemiBold" Margin="0,8,0,0" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="Ctrl+I" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Show Auto Inactives (not yet implemented)" TextWrapping="Wrap" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+A" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Show Auto Actives (not yet implemented)" TextWrapping="Wrap" />
            </Grid>

            <TextBlock Text="Zoom/Import Control" FontWeight="SemiBold" Margin="0,8,0,0" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="Ctrl+C" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Reset Zoom Bar charts" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+X" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Fit Zoom to Reference" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Ctrl+Y" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Reset Zoom to Import limits (not yet implemented)" TextWrapping="Wrap" />
                <TextBlock Grid.Row="3" Grid.Column="0" Text="Ctrl+Alt+Space" />
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Reset Zoom for all Graphs (not yet implemented)" TextWrapping="Wrap" />
            </Grid>

            <TextBlock Text="Abort/Reset" FontWeight="SemiBold" Margin="0,8,0,0" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="T" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Abort CSP Analysis" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+R" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Reset Application (not yet implemented)" TextWrapping="Wrap" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Ctrl+Q" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Close Selected Window/Quit" />
            </Grid>

            <TextBlock Text="Manual Flag Control" FontWeight="SemiBold" Margin="0,8,0,0" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="N" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Reset All Manual Flags" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="D" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Reset Manual Flag" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="S" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Mark as Inactive" />
                <TextBlock Grid.Row="3" Grid.Column="0" Text="A" />
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Mark as Active" />
            </Grid>

            <TextBlock Text="Export Data Window (in the Export/Results window)" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="Ctrl+E" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Export To Excel" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+P" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Print (exports PDF)" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="R" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Refresh" />
            </Grid>

            <TextBlock Text="Import/Threshold controls" FontWeight="SemiBold" Margin="0,8,0,0" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto,Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="Ctrl+Alt+I" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Reset Import Limits" />
                <TextBlock Grid.Row="1" Grid.Column="0" Text="Ctrl+Alt+T" />
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Reset Intensity Thresholds" />
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Ctrl+Alt+O" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Reset All Imp.Controls to Default" />
            </Grid>

            <TextBlock Text="Other" FontWeight="SemiBold" Margin="0,8,0,0" />
            <Grid ColumnDefinitions="90,*" RowDefinitions="Auto">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="G" />
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Select Go To Experiment Textbox" />
            </Grid>
        </StackPanel>
    </ScrollViewer>
</Window>
```

- [ ] **Step 4: Create `ShortcutsWindow.axaml.cs`**

```csharp
// dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml.cs
using Avalonia.Controls;

namespace CspAnalyzer.Desktop.Views;

public partial class ShortcutsWindow : Window
{
    public ShortcutsWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter ShortcutsWindowTests`
Expected: PASS (1/1)

- [ ] **Step 6: Create `AvaloniaShortcutsWindowService`**

```csharp
// dotnet/CspAnalyzer.Desktop/Services/AvaloniaShortcutsWindowService.cs
using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaShortcutsWindowService(Window owner) : IShortcutsWindowService
{
    public void Show()
    {
        var window = new ShortcutsWindow();
        window.Show(owner);
    }
}
```

- [ ] **Step 7: Wire the sidebar's "Shortcuts" button**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml`, change line 126 from:

```xml
                        <Button Content="Shortcuts" />
```

to:

```xml
                        <Button Content="Shortcuts" Command="{Binding OpenShortcutsWindowCommand}" />
```

- [ ] **Step 8: Swap the remaining `NullShortcutsWindowService` placeholder in `App.axaml.cs`**

Task 2 already swapped the About placeholder, leaving Shortcuts as the
only remaining one. In `dotnet/CspAnalyzer.Desktop/App.axaml.cs`, change:

```csharp
                new AvaloniaAboutWindowService(window),
                new NullShortcutsWindowService());
```

to:

```csharp
                new AvaloniaAboutWindowService(window),
                new AvaloniaShortcutsWindowService(window));
```

- [ ] **Step 9: Run the full test suite to check for regressions**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS, no regressions.

- [ ] **Step 10: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml.cs dotnet/CspAnalyzer.Desktop/Services/AvaloniaShortcutsWindowService.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/App.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs
git commit -m "S11c: ShortcutsWindow + AvaloniaShortcutsWindowService, wire sidebar Shortcuts button + App.axaml.cs"
```

---

### Task 4: `MainWindow` real `KeyBindings`

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml` (add `x:Name` to the Go-To-Experiment `TextBox` at line 296, add a `<Window.KeyBindings>` block)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs` (two new `RelayCommand` properties)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`

**Interfaces:**
- Consumes: `MainViewModel.NextCommand`/`PreviousCommand`/`FirstCommand`/`LastCommand`/`RunCommand`/`CancelRunCommand`/`ShowReferencePpDetailsCommand`/`ShowExperimentPpDetailsCommand`/`ResetOverlayZoomCommand`/`FitOverlayZoomToReferenceCommand`/`ResetAllManualFlagsCommand`/`ResetManualStatusCommand`/`MarkInactiveCommand`/`MarkActiveCommand`/`ResetImportControlsCommand`/`ResetPeakFilteringCommand`/`ResetAllImportAndThresholdControlsCommand`/`OpenShortcutsWindowCommand` (all pre-existing or from Task 1).
- Produces: `MainWindow.CloseCommand`, `MainWindow.FocusGoToExperimentCommand` (public `ICommand` properties, `RelativeSource={RelativeSource Self}`-bindable), used only within this file.

- [ ] **Step 1: Write the failing tests**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
using System.Threading.Tasks;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainWindowKeyBindingsTests
{
    private static (MainWindow Window, MainViewModel ViewModel) NewWindow()
    {
        var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (window, vm);
    }

    [AvaloniaFact]
    public void CtrlK_OpensShortcutsWindow()
    {
        var recording = new RecordingShortcutsWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), recording);
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.Control);

        Assert.Equal(1, recording.ShowCallCount);
    }

    // CancelRun's only real effect (_runCts?.Cancel()) isn't observable
    // through public state without a live subprocess (RunAsync returns
    // before creating _runCts when no python env is found, which is always
    // true in this test environment). Verified structurally instead: the
    // KeyBinding for "T" resolves to the exact same command instance the
    // ViewModel exposes, which is what makes the real key press wire to
    // the real command in production.
    [AvaloniaFact]
    public void T_IsBoundToCancelRunCommand()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();

        KeyBinding binding = Assert.Single(window.KeyBindings, kb => kb.Gesture.Key == Key.T && kb.Gesture.KeyModifiers == KeyModifiers.None);

        Assert.Same(vm.CancelRunCommand, binding.Command);
    }

    [AvaloniaFact]
    public async Task N_ResetsAllManualFlags()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ds",
            Peaklist = new(),
            UserSelection = "ACTIVE (MAN)",
        });

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.N, RawInputModifiers.None);
        await ((IAsyncRelayCommand)vm.ResetAllManualFlagsCommand).ExecutionTask!;

        Assert.Equal("Not set", vm.DatasetSpectra[0].UserSelection);
    }

    [AvaloniaFact]
    public void CtrlAltI_ResetsImportControls()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.NMin = 1;
        vm.NMax = 2;

        window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.Control | RawInputModifiers.Alt);
        window.KeyReleaseQwerty(PhysicalKey.I, RawInputModifiers.Control | RawInputModifiers.Alt);

        Assert.Equal(100, vm.NMin);
        Assert.Equal(140, vm.NMax);
    }

    [AvaloniaFact]
    public void CtrlAltO_ResetsBothImportAndThresholdControls()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.NMin = 1;
        vm.ReferenceIntensityThreshold = 1;

        window.KeyPressQwerty(PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Alt);
        window.KeyReleaseQwerty(PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Alt);

        Assert.Equal(100, vm.NMin);
        Assert.Equal(5000, vm.ReferenceIntensityThreshold);
    }

    // The input-focus caveat from the design spec: bare-letter KeyBindings
    // (N here) must not fire while a TextBox has focus and the user is
    // typing normal text. GoToExperimentText is the one sidebar TextBox
    // bound to a plain string (the numeric ones reject non-digit input
    // before this even becomes observable), so it's the fixture that can
    // prove a real character got inserted. The dataset fixture's
    // UserSelection staying "ACTIVE (MAN)" (not flipped to "Not set")
    // proves ResetAllManualFlagsCommand did NOT fire while the TextBox had
    // focus - if this assertion fails, that's the blocking finding the
    // design spec calls out, not a shippable known-limitation.
    [AvaloniaFact]
    public void TypingLetterN_InGoToExperimentTextBox_InsertsCharacterAndDoesNotResetManualFlags()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ds",
            Peaklist = new(),
            UserSelection = "ACTIVE (MAN)",
        });
        var goToBox = window.FindControl<Avalonia.Controls.TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.N, RawInputModifiers.None);

        Assert.Equal("n", vm.GoToExperimentText);
        Assert.Equal("ACTIVE (MAN)", vm.DatasetSpectra[0].UserSelection);
    }

    [AvaloniaFact]
    public void G_FocusesGoToExperimentTextBox()
    {
        (MainWindow window, _) = NewWindow();
        var textBox = window.FindControl<Avalonia.Controls.TextBox>("GoToExperimentTextBox")!;

        window.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.G, RawInputModifiers.None);

        Assert.True(textBox.IsFocused);
    }

    [AvaloniaFact]
    public void CtrlQ_ClosesWindow()
    {
        (MainWindow window, _) = NewWindow();
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        window.KeyPressQwerty(PhysicalKey.Q, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Q, RawInputModifiers.Control);

        Assert.True(closed);
    }

    private sealed class RecordingShortcutsWindowService : IShortcutsWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainWindowKeyBindingsTests`
Expected: FAIL — no `KeyBindings` exist on `MainWindow` yet, and `NMinTextBox`/`GoToExperimentTextBox` have no `x:Name` yet.

- [ ] **Step 3: Name the two `TextBox`es the tests/focus-command need**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml`, change line 82 from:

```xml
                        <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding NMin}" />
```

to:

```xml
                        <TextBox x:Name="NMinTextBox" Grid.Row="0" Grid.Column="1" Text="{Binding NMin}" />
```

and change line 296 from:

```xml
                            <TextBox Width="60" Text="{Binding GoToExperimentText}" />
```

to:

```xml
                            <TextBox x:Name="GoToExperimentTextBox" Width="60" Text="{Binding GoToExperimentText}" />
```

- [ ] **Step 4: Add the `<Window.KeyBindings>` block**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml`, immediately after the closing `</Window.Styles>` tag (after line 48), add:

```xml
    <Window.KeyBindings>
        <KeyBinding Gesture="R" Command="{Binding RunCommand}" />
        <KeyBinding Gesture="Ctrl+K" Command="{Binding OpenShortcutsWindowCommand}" />
        <KeyBinding Gesture="Right" Command="{Binding NextCommand}" />
        <KeyBinding Gesture="Left" Command="{Binding PreviousCommand}" />
        <KeyBinding Gesture="Down" Command="{Binding LastCommand}" />
        <KeyBinding Gesture="Up" Command="{Binding FirstCommand}" />
        <KeyBinding Gesture="Ctrl+Alt+R" Command="{Binding ShowReferencePpDetailsCommand}" />
        <KeyBinding Gesture="Ctrl+Alt+E" Command="{Binding ShowExperimentPpDetailsCommand}" />
        <KeyBinding Gesture="Ctrl+C" Command="{Binding ResetOverlayZoomCommand}" />
        <KeyBinding Gesture="Ctrl+X" Command="{Binding FitOverlayZoomToReferenceCommand}" />
        <KeyBinding Gesture="T" Command="{Binding CancelRunCommand}" />
        <KeyBinding Gesture="N" Command="{Binding ResetAllManualFlagsCommand}" />
        <KeyBinding Gesture="D" Command="{Binding ResetManualStatusCommand}" />
        <KeyBinding Gesture="S" Command="{Binding MarkInactiveCommand}" />
        <KeyBinding Gesture="A" Command="{Binding MarkActiveCommand}" />
        <KeyBinding Gesture="Ctrl+Alt+I" Command="{Binding ResetImportControlsCommand}" />
        <KeyBinding Gesture="Ctrl+Alt+T" Command="{Binding ResetPeakFilteringCommand}" />
        <KeyBinding Gesture="Ctrl+Alt+O" Command="{Binding ResetAllImportAndThresholdControlsCommand}" />
        <KeyBinding Gesture="Ctrl+Q" Command="{Binding CloseCommand, RelativeSource={RelativeSource Self}}" />
        <KeyBinding Gesture="G" Command="{Binding FocusGoToExperimentCommand, RelativeSource={RelativeSource Self}}" />
    </Window.KeyBindings>
```

- [ ] **Step 5: Add `CloseCommand`/`FocusGoToExperimentCommand` to `MainWindow.axaml.cs`**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs`, add `using CommunityToolkit.Mvvm.Input;` and `using System.Windows.Input;` to the usings, and inside the class add:

```csharp
    public ICommand CloseCommand { get; }

    public ICommand FocusGoToExperimentCommand { get; }
```

and in the constructor, after `InitializeComponent();`, add:

```csharp
        CloseCommand = new RelayCommand(Close);
        FocusGoToExperimentCommand = new RelayCommand(() => this.FindControl<TextBox>("GoToExperimentTextBox")!.Focus());
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainWindowKeyBindingsTests`
Expected: PASS (8/8)

- [ ] **Step 7: Run the full test suite to check for regressions**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS, no regressions.

- [ ] **Step 8: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
git commit -m "S11c: MainWindow.KeyBindings for every mapped legacy shortcut"
```

---

### Task 5: `ResultsWindow` real `KeyBindings`

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml` (add a `<Window.KeyBindings>` block)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml.cs` (one new `RelayCommand` property)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/ResultsWindowKeyBindingsTests.cs`

**Interfaces:**
- Consumes: `ResultsViewModel.ExportXlsxCommand`/`ExportPdfCommand`/`RefreshCommand` (all pre-existing).
- Produces: `ResultsWindow.CloseCommand` (public `ICommand`, `RelativeSource={RelativeSource Self}`-bindable), used only within this file.

- [ ] **Step 1: Write the failing tests**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/ResultsWindowKeyBindingsTests.cs
using System.Collections.Generic;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class ResultsWindowKeyBindingsTests
{
    private static (ResultsWindow Window, ResultsViewModel ViewModel) NewWindow()
    {
        var reference = new PeaklistSpectrum { ExpNumber = 1, DsName = "ref", Peaklist = new() };
        var vm = new ResultsViewModel(new NullFilePickerService(), reference, new List<PeaklistSpectrum>(), new List<SpectrumResult>());
        var window = new ResultsWindow { DataContext = vm };
        window.Show();
        return (window, vm);
    }

    [AvaloniaFact]
    public void R_RefreshesResults()
    {
        (ResultsWindow window, ResultsViewModel vm) = NewWindow();
        int countBefore = vm.TotalExperiments;

        window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.R, RawInputModifiers.None);

        Assert.Equal(countBefore, vm.TotalExperiments);
    }

    [AvaloniaFact]
    public void CtrlQ_ClosesWindow()
    {
        (ResultsWindow window, _) = NewWindow();
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        window.KeyPressQwerty(PhysicalKey.Q, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Q, RawInputModifiers.Control);

        Assert.True(closed);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter ResultsWindowKeyBindingsTests`
Expected: FAIL — no `KeyBindings` exist on `ResultsWindow` yet.

- [ ] **Step 3: Add the `<Window.KeyBindings>` block**

In `dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml`, immediately after the closing `</Window.Resources>` tag (after line 23), add:

```xml
    <Window.KeyBindings>
        <KeyBinding Gesture="Ctrl+E" Command="{Binding ExportXlsxCommand}" />
        <KeyBinding Gesture="Ctrl+P" Command="{Binding ExportPdfCommand}" />
        <KeyBinding Gesture="R" Command="{Binding RefreshCommand}" />
        <KeyBinding Gesture="Ctrl+Q" Command="{Binding CloseCommand, RelativeSource={RelativeSource Self}}" />
    </Window.KeyBindings>
```

- [ ] **Step 4: Add `CloseCommand` to `ResultsWindow.axaml.cs`**

Replace the full contents of `dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml.cs`:

```csharp
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace CspAnalyzer.Desktop.Views;

public partial class ResultsWindow : Window
{
    public ICommand CloseCommand { get; }

    public ResultsWindow()
    {
        InitializeComponent();
        CloseCommand = new RelayCommand(Close);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter ResultsWindowKeyBindingsTests`
Expected: PASS (2/2)

- [ ] **Step 6: Run the full test suite to check for regressions**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS, no regressions.

- [ ] **Step 7: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/ResultsWindowKeyBindingsTests.cs
git commit -m "S11c: ResultsWindow.KeyBindings for Export/Refresh/Close"
```
