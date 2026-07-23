# S12 Remaining Shortcuts + Zoom-Binding Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the last 9 legacy keyboard shortcuts (Enter/I, Ctrl+A, Ctrl+I, Ctrl+Y, Ctrl+Alt+Space, Ctrl+Alt+F, Ctrl+Alt+Y, Ctrl+R) into `CspAnalyzer.Desktop`, and fix a real S11c bug where Ctrl+C and Ctrl+Y's zoom-reset behaviors got swapped.

**Architecture:** All new behavior lives on `MainViewModel` (new `[RelayCommand]`s, two new `ObservableCollection<string>` diagnostic lists, one new constructor-injected `IInfoDialogService`, one new constructor-injected `SettingsService`), wired into `MainWindow`'s existing gesture-dispatch pattern (`Window.KeyBindings` XAML for unguarded Ctrl+Alt+* combos, code-behind `GuardedViewModelCommand` for anything that could collide with normal `TextBox` typing/editing/clipboard shortcuts). `ShortcutsWindow.axaml` is the single source of truth for what's documented as wired vs. not — every task that wires a gesture also removes its "(not yet implemented)" suffix in the same commit.

**Tech Stack:** .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm, xunit + Avalonia.Headless.XUnit.

## Global Constraints

- Windows is unverified this session (no Windows box available) — every change gets a Linux run + screenshot, not a Windows-verified claim.
- Every bare-letter, plain-Ctrl+letter, or Enter gesture bound to a `MainViewModel` command MUST go through `MainWindow.axaml.cs`'s `GuardedViewModelCommand` (not a plain XAML `<KeyBinding>`), so it doesn't fire while a sidebar `TextBox` is focused — this is the established pattern for every such gesture since S11c, and applies equally to new ones here (`Enter`, `I`, `Ctrl+A`, `Ctrl+I`, `Ctrl+C` (rebind), `Ctrl+Y`, `Ctrl+R`).
- Ctrl+Alt+* combos are the one exception: they go directly in `MainWindow.axaml`'s `<Window.KeyBindings>` block, unguarded — matches the existing Ctrl+Alt+R/E/I/T/O precedent (no observed textbox-collision class for this combo family).
- `MainViewModel`'s constructor is additive-only: new dependencies are appended as new trailing parameters, never inserted/reordered — every existing call site keeps working positionally once updated, matching the pattern from S11c (3→5 params) and S11d (5→6 params).
- No placeholders, no TODOs — every step below has the exact code to write.

---

## Task 1: Extend `MainViewModel`'s constructor with `IInfoDialogService` + `SettingsService`

This is pure plumbing two later tasks need (Task 4's corrupted/out-of-range dialogs, Task 6's Reset Application). Doing it once, first, means every other task just consumes `_infoDialogService`/`_settingsService` instead of touching the constructor and its ~9 call sites twice.

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Services/IInfoDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/AvaloniaInfoDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/NullInfoDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Views/InfoDialog.axaml`
- Create: `dotnet/CspAnalyzer.Desktop/Views/InfoDialog.axaml.cs`
- Create: `dotnet/CspAnalyzer.Desktop.Tests/InfoDialogTests.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs:33-145` (constructor)
- Modify: `dotnet/CspAnalyzer.Desktop/App.axaml.cs:18-48`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs:57`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs:13`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs:31-33,44-46,57-59`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs:30-32,500-502,516-518`

**Interfaces:**
- Produces: `IInfoDialogService.ShowAsync(string title, string message) : Task`; `NullInfoDialogService`, `AvaloniaInfoDialogService(Window owner)`; `MainViewModel`'s constructor becomes `MainViewModel(IFilePickerService, IResultsWindowService, IConfirmDialogService, IAboutWindowService, IShortcutsWindowService, IHelpWindowService, IInfoDialogService, SettingsService)` (8 params, was 6); private fields `_infoDialogService` (`IInfoDialogService`), `_settingsService` (`SettingsService`, `CspAnalyzer.Desktop.Services.SettingsService`).

- [ ] **Step 1: Write the failing view test for `InfoDialog`**

Create `dotnet/CspAnalyzer.Desktop.Tests/InfoDialogTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter InfoDialogTests`
Expected: FAIL — `InfoDialog`/`NullInfoDialogService` don't exist yet (compile error).

- [ ] **Step 3: Create `IInfoDialogService`**

`dotnet/CspAnalyzer.Desktop/Services/IInfoDialogService.cs`:

```csharp
using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Read-only info display (S12) - mirrors IConfirmDialogService's reasoning
/// but OK-button-only, no return value. Used for listing corrupted/out-of-
/// range experiment names (CSPv2/Form1.cs's MessageBox.Show(..., OK) calls
/// behind Ctrl+Alt+F/Ctrl+Alt+O).
/// </summary>
public interface IInfoDialogService
{
    Task ShowAsync(string title, string message);
}
```

- [ ] **Step 4: Create `NullInfoDialogService`**

`dotnet/CspAnalyzer.Desktop/Services/NullInfoDialogService.cs`:

```csharp
using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>No-op for the Avalonia design-time DataContext and tests, where there's no real dialog to show.</summary>
public sealed class NullInfoDialogService : IInfoDialogService
{
    public Task ShowAsync(string title, string message) => Task.CompletedTask;
}
```

- [ ] **Step 5: Create the `InfoDialog` view**

`dotnet/CspAnalyzer.Desktop/Views/InfoDialog.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="CspAnalyzer.Desktop.Views.InfoDialog"
        Title="Info"
        Width="420" Height="360"
        WindowStartupLocation="CenterOwner">
    <DockPanel Margin="16">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="OK" Click="OnOkClicked" IsDefault="True" />
        </StackPanel>
        <ScrollViewer>
            <TextBlock x:Name="MessageText" TextWrapping="Wrap" />
        </ScrollViewer>
    </DockPanel>
</Window>
```

`dotnet/CspAnalyzer.Desktop/Views/InfoDialog.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CspAnalyzer.Desktop.Views;

public partial class InfoDialog : Window
{
    public InfoDialog()
    {
        InitializeComponent();
    }

    public InfoDialog(string title, string message) : this()
    {
        Title = title;
        MessageText.Text = message;
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 6: Create `AvaloniaInfoDialogService`**

`dotnet/CspAnalyzer.Desktop/Services/AvaloniaInfoDialogService.cs`:

```csharp
using System.Threading.Tasks;
using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaInfoDialogService(Window owner) : IInfoDialogService
{
    public async Task ShowAsync(string title, string message)
    {
        var dialog = new InfoDialog(title, message);
        await dialog.ShowDialog(owner);
    }
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter InfoDialogTests`
Expected: PASS (2 tests)

- [ ] **Step 8: Extend `MainViewModel`'s constructor**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, replace lines 35-40 (the six private readonly service fields) with:

```csharp
    private readonly IFilePickerService _filePicker;
    private readonly IResultsWindowService _resultsWindowService;
    private readonly IConfirmDialogService _confirmDialogService;
    private readonly IAboutWindowService _aboutWindowService;
    private readonly IShortcutsWindowService _shortcutsWindowService;
    private readonly IHelpWindowService _helpWindowService;
    private readonly IInfoDialogService _infoDialogService;
    private readonly SettingsService _settingsService;
```

`MainViewModel.cs` already has `using CspAnalyzer.Desktop.Services;` at the top, so `SettingsService` resolves unqualified — same style as every other service field.

Replace lines 125-145 (the two constructors) with:

```csharp
    public MainViewModel() : this(
        new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
        new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(),
        new NullInfoDialogService(), new SettingsService())
    {
    }

    public MainViewModel(
        IFilePickerService filePicker,
        IResultsWindowService resultsWindowService,
        IConfirmDialogService confirmDialogService,
        IAboutWindowService aboutWindowService,
        IShortcutsWindowService shortcutsWindowService,
        IHelpWindowService helpWindowService,
        IInfoDialogService infoDialogService,
        SettingsService settingsService)
    {
        _filePicker = filePicker;
        _resultsWindowService = resultsWindowService;
        _confirmDialogService = confirmDialogService;
        _aboutWindowService = aboutWindowService;
        _shortcutsWindowService = shortcutsWindowService;
        _helpWindowService = helpWindowService;
        _infoDialogService = infoDialogService;
        _settingsService = settingsService;
    }
```

- [ ] **Step 9: Update `App.axaml.cs`**

Replace `dotnet/CspAnalyzer.Desktop/App.axaml.cs:22-35` with:

```csharp
            var window = new MainWindow();
            var settingsService = new SettingsService();
            var viewModel = new MainViewModel(
                new AvaloniaFilePickerService(window),
                new AvaloniaResultsWindowService(window),
                new AvaloniaConfirmDialogService(window),
                new AvaloniaAboutWindowService(window),
                new AvaloniaShortcutsWindowService(window),
                new AvaloniaHelpWindowService(window),
                new AvaloniaInfoDialogService(window),
                settingsService);
            window.DataContext = viewModel;

            AppSettings settings = settingsService.Load();
            window.ApplyAppearanceSettings(settings);
            viewModel.ApplySettings(settings);
```

- [ ] **Step 10: Update the 9 explicit test call sites**

In `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs:57`, change:

```csharp
        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService());
```

to:

```csharp
        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(), new NullInfoDialogService(), new SettingsService());
```

In `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs:13`, change:

```csharp
        var vm = new MainViewModel(new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService());
```

to:

```csharp
        var vm = new MainViewModel(new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(), new NullInfoDialogService(), new SettingsService());
```

In `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs`, all three `new MainViewModel(...)` calls (lines 31-33, 44-46, 57-59) gain `new NullInfoDialogService(), new SettingsService())` as the final two arguments, e.g. lines 31-33 become:

```csharp
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            aboutService, new NullShortcutsWindowService(), new NullHelpWindowService(),
            new NullInfoDialogService(), new SettingsService());
```

(apply the same trailing-two-args addition to the shortcutsService call at lines 44-46 and the helpService call at lines 57-59).

In `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`, the three explicit `new MainViewModel(...)` calls (lines 30-32, 500-502, 516-518) each gain `new NullInfoDialogService(), new SettingsService())` as the final two arguments, e.g. lines 30-32 become:

```csharp
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), recording, new NullHelpWindowService(),
            new NullInfoDialogService(), new SettingsService());
```

(apply the same trailing-two-args addition at lines 500-502 and 516-518).

Add `using CspAnalyzer.Desktop.Services;` to `MainViewModelNavigationTests.cs` and `MainViewModelManualOverrideTests.cs` if not already present (both already have it per current imports — verify, don't duplicate).

- [ ] **Step 11: Run the full test suite**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS, same test count as before Step 1 plus the 2 new `InfoDialogTests`, zero compile errors across every touched call site.

- [ ] **Step 12: Build the desktop app**

Run: `dotnet build dotnet/CspAnalyzer.sln`
Expected: Build succeeds with no errors.

- [ ] **Step 13: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Services/IInfoDialogService.cs dotnet/CspAnalyzer.Desktop/Services/AvaloniaInfoDialogService.cs dotnet/CspAnalyzer.Desktop/Services/NullInfoDialogService.cs dotnet/CspAnalyzer.Desktop/Views/InfoDialog.axaml dotnet/CspAnalyzer.Desktop/Views/InfoDialog.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/InfoDialogTests.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop/App.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSecondaryWindowsTests.cs dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
git commit -m "S12 Task 1: add IInfoDialogService + SettingsService to MainViewModel's constructor"
```

---

## Task 2: Fix the Ctrl+C/Ctrl+Y zoom-binding bug + Ctrl+Alt+Space

Legacy `Ctrl+C` resets the two bar charts; legacy `Ctrl+Y` resets the overlay chart. S11c bound `ResetOverlayZoomCommand` (the `Ctrl+Y` behavior) to `Ctrl+C` and never bound `Ctrl+Y` at all.

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs:307-319` (add commands)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs:47,86-93` (rebind + new binding + comment)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml:50-57` (add Ctrl+Alt+Space)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:61-64`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs` (rename 2 existing tests, add 3 new)

**Interfaces:**
- Produces: `MainViewModel.ResetBarChartZoomCommand`, `MainViewModel.ResetAllZoomCommand` (both parameterless `[RelayCommand]`, no `CanExecute`).
- Consumes: existing `PeakDiffXAxes`/`ProbabilityXAxes` (`Axis[]`), `DatasetSpectra.Count`, existing `ResetOverlayZoom()` private method.

- [ ] **Step 1: Write the failing tests**

In `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`, replace the two existing tests `CtrlC_GuardedWhileTextBoxFocused_DoesNotResetOverlayZoom` and `CtrlC_NotFocused_ResetsOverlayZoom` (currently lines 143-169) with:

```csharp
    [AvaloniaFact]
    public void CtrlC_GuardedWhileTextBoxFocused_DoesNotResetBarChartZoom()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ref", Peaklist = new(), TotReadPeaks = 10 };
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new(), TotReadPeaks = 12 });
        vm.BuildPeakDiffChart();
        vm.PeakDiffXAxes[0].MinLimit = 999;
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.C, RawInputModifiers.Control);

        Assert.Equal(999, vm.PeakDiffXAxes[0].MinLimit);
    }

    [AvaloniaFact]
    public void CtrlC_NotFocused_ResetsBarChartZoom()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ref", Peaklist = new(), TotReadPeaks = 10 };
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new(), TotReadPeaks = 12 });
        vm.BuildPeakDiffChart();
        vm.BuildProbabilityChart();
        vm.PeakDiffXAxes[0].MinLimit = 999;
        vm.ProbabilityXAxes[0].MinLimit = 999;

        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.C, RawInputModifiers.Control);

        Assert.Equal(0, vm.PeakDiffXAxes[0].MinLimit);
        Assert.Equal(1, vm.PeakDiffXAxes[0].MaxLimit);
        Assert.Equal(0, vm.ProbabilityXAxes[0].MinLimit);
        Assert.Equal(1, vm.ProbabilityXAxes[0].MaxLimit);
    }

    [AvaloniaFact]
    public void CtrlY_GuardedWhileTextBoxFocused_DoesNotResetOverlayZoom()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.BuildOverlayAxes();
        vm.OverlayXAxes[0].MinLimit = 999;
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Y, RawInputModifiers.Control);

        Assert.Equal(999, vm.OverlayXAxes[0].MinLimit);
    }

    [AvaloniaFact]
    public void CtrlY_NotFocused_ResetsOverlayZoom()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.BuildOverlayAxes();
        vm.OverlayXAxes[0].MinLimit = 999;

        window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Y, RawInputModifiers.Control);

        Assert.Equal(-vm.HMax, vm.OverlayXAxes[0].MinLimit);
    }

    [AvaloniaFact]
    public void CtrlAltSpace_ResetsOverlayAndBarChartZoom()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 1, DsName = "ref",
            Peaklist = new() { new CspAnalyzer.BackendInterop.Peak { F1 = 110, F2 = 8 } },
            TotReadPeaks = 10,
        };
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new(), TotReadPeaks = 12 });
        vm.BuildOverlayAxes();
        vm.BuildPeakDiffChart();
        vm.OverlayXAxes[0].MinLimit = 999;
        vm.PeakDiffXAxes[0].MinLimit = 999;

        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control | RawInputModifiers.Alt);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control | RawInputModifiers.Alt);

        Assert.Equal(-vm.HMax, vm.OverlayXAxes[0].MinLimit);
        Assert.Equal(0, vm.PeakDiffXAxes[0].MinLimit);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter "MainWindowKeyBindingsTests&CtrlC_NotFocused_ResetsBarChartZoom|MainWindowKeyBindingsTests&CtrlY_NotFocused_ResetsOverlayZoom|MainWindowKeyBindingsTests&CtrlAltSpace_ResetsOverlayAndBarChartZoom"`
Expected: FAIL — `ResetBarChartZoomCommand`/`ResetAllZoomCommand` don't exist (compile error), and `CtrlC_NotFocused_ResetsBarChartZoom` would fail on assertion even once it compiles (Ctrl+C still resets overlay, not bar charts).

- [ ] **Step 3: Add the two new chart commands**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs`, after the existing `ResetOverlayZoom`/`FitOverlayZoomToReference` commands (after line 333, before the closing `}` of the class at line 334), add:

```csharp

    [RelayCommand]
    private void ResetBarChartZoom()
    {
        if (PeakDiffXAxes.Length > 0)
        {
            PeakDiffXAxes[0].MinLimit = 0;
            PeakDiffXAxes[0].MaxLimit = DatasetSpectra.Count;
        }

        if (ProbabilityXAxes.Length > 0)
        {
            ProbabilityXAxes[0].MinLimit = 0;
            ProbabilityXAxes[0].MaxLimit = DatasetSpectra.Count;
        }
    }

    [RelayCommand]
    private void ResetAllZoom()
    {
        ResetOverlayZoom();
        ResetBarChartZoom();
    }
```

- [ ] **Step 4: Rebind Ctrl+C, add Ctrl+Y, update the doc comment**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs:47`, change:

```csharp
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.C, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ResetOverlayZoomCommand) });
```

to:

```csharp
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.C, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ResetBarChartZoomCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Y, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ResetOverlayZoomCommand) });
```

Update the doc comment at lines 86-93 (the "Ctrl+C (ResetOverlayZoomCommand) and Ctrl+X ..." paragraph) to:

```csharp
    - Ctrl+C (ResetBarChartZoomCommand), Ctrl+Y (ResetOverlayZoomCommand), and
      Ctrl+X (FitOverlayZoomToReferenceCommand): same defect class as the bare
      keys/arrows above, just via a Ctrl+letter combo. Window.KeyBindings
      intercept before a routed KeyDown reaches a focused control, so a
      plain {Binding ...} KeyBinding on these would always fire the
      chart-reset commands instead of letting a focused TextBox perform its
      standard clipboard copy (Ctrl+C) / cut (Ctrl+X) / redo (Ctrl+Y), even
      with text selected. Guarded the same way, via GuardedViewModelCommand.
      (S11c originally bound Ctrl+C to ResetOverlayZoomCommand instead of
      ResetBarChartZoomCommand and left Ctrl+Y unbound - legacy CSPv2/
      Form1.cs:2986-3002 has it the other way around; fixed in S12.)
    -->
```

(replace through the existing closing `-->` on the line after the old paragraph).

- [ ] **Step 5: Add the Ctrl+Alt+Space XAML binding**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml:56`, after the `Ctrl+Alt+O` line, add:

```xml
        <KeyBinding Gesture="Ctrl+Alt+Space" Command="{Binding ResetAllZoomCommand}" />
```

- [ ] **Step 6: Update `ShortcutsWindow.axaml`**

In `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:62,64`, change:

```xml
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Reset Zoom to Import limits (not yet implemented)" TextWrapping="Wrap" />
```

to:

```xml
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Reset Zoom to Import limits" />
```

and:

```xml
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Reset Zoom for all Graphs (not yet implemented)" TextWrapping="Wrap" />
```

to:

```xml
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Reset Zoom for all Graphs" />
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter MainWindowKeyBindingsTests`
Expected: PASS, all tests in the file including the 5 new/renamed ones.

- [ ] **Step 8: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
git commit -m "S12 Task 2: fix Ctrl+C/Ctrl+Y zoom-binding swap, add Ctrl+Alt+Space (reset all zoom)"
```

---

## Task 3: Auto Actives/Inactives toggle (Ctrl+A / Ctrl+I)

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Navigation.cs:45-55` (add toggle commands)
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs:307-311` (notify new commands after a run)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs:49,70-93` (bindings + comment)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:50,52`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs` (5 new tests)

**Interfaces:**
- Produces: `MainViewModel.ToggleAutoActivesFilterCommand`, `MainViewModel.ToggleAutoInactivesFilterCommand` (`[RelayCommand(CanExecute = nameof(CanToggleAutoFilter))]`).
- Consumes: existing `IsActivesFilterChecked`/`IsInactivesFilterChecked` (settable `bool`), `RunResults` (`ObservableCollection<SpectrumResult>`).

- [ ] **Step 1: Write the failing tests**

In `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`, add (anywhere after the `NewWindow` helper, e.g. right after the `CtrlAltSpace_ResetsOverlayAndBarChartZoom` test added in Task 2):

```csharp
    [AvaloniaFact]
    public void CtrlA_NotFocused_TogglesActivesFilter()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 1, IsActive = true, ActivePseudoprobability = 0.9 });

        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);

        Assert.True(vm.IsActivesFilterChecked);
    }

    [AvaloniaFact]
    public void CtrlA_GuardedWhileTextBoxFocused_DoesNotToggleActivesFilter()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 1, IsActive = true, ActivePseudoprobability = 0.9 });
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);

        Assert.False(vm.IsActivesFilterChecked);
    }

    [AvaloniaFact]
    public void CtrlA_NoRunResultsYet_DoesNotToggleActivesFilter()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();

        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);

        Assert.False(vm.IsActivesFilterChecked);
    }

    [AvaloniaFact]
    public void CtrlI_NotFocused_TogglesInactivesFilter()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 1, IsActive = false, ActivePseudoprobability = 0.1 });

        window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.I, RawInputModifiers.Control);

        Assert.True(vm.IsInactivesFilterChecked);
    }

    [AvaloniaFact]
    public void CtrlI_GuardedWhileTextBoxFocused_DoesNotToggleInactivesFilter()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 1, IsActive = false, ActivePseudoprobability = 0.1 });
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.I, RawInputModifiers.Control);

        Assert.False(vm.IsInactivesFilterChecked);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter "CtrlA_NotFocused_TogglesActivesFilter|CtrlI_NotFocused_TogglesInactivesFilter"`
Expected: FAIL — `ToggleAutoActivesFilterCommand`/`ToggleAutoInactivesFilterCommand` don't exist (compile error).

- [ ] **Step 3: Add the toggle commands**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Navigation.cs`, after the `IsInactivesFilterChecked` property (after line 55, before `private Dictionary<int, SpectrumResult> ResultsByExpNumber` at line 57), add:

```csharp

    private bool CanToggleAutoFilter() => RunResults.Count > 0;

    [RelayCommand(CanExecute = nameof(CanToggleAutoFilter))]
    private void ToggleAutoActivesFilter() => IsActivesFilterChecked = !IsActivesFilterChecked;

    [RelayCommand(CanExecute = nameof(CanToggleAutoFilter))]
    private void ToggleAutoInactivesFilter() => IsInactivesFilterChecked = !IsInactivesFilterChecked;
```

- [ ] **Step 4: Notify the new commands after a run populates `RunResults`**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, after `OpenResultsWindowCommand.NotifyCanExecuteChanged();` (line 311), add:

```csharp
                ToggleAutoActivesFilterCommand.NotifyCanExecuteChanged();
                ToggleAutoInactivesFilterCommand.NotifyCanExecuteChanged();
```

- [ ] **Step 5: Add the guarded key bindings**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs:49`, after the `Key.H` binding, add:

```csharp
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.A, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ToggleAutoActivesFilterCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.I, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ToggleAutoInactivesFilterCommand) });
```

Append to the doc comment block above `<Window.KeyBindings>` in `MainWindow.axaml` (the same block Task 2 extended) one more paragraph, after the Ctrl+C/Ctrl+Y/Ctrl+X paragraph:

```csharp
    - Ctrl+A (ToggleAutoActivesFilterCommand) and Ctrl+I
      (ToggleAutoInactivesFilterCommand): same defect class again - Ctrl+A is
      "select all" and Ctrl+I is commonly "italic" in standard text editing;
      both must not fire while a TextBox is focused. Guarded via
      GuardedViewModelCommand.
    -->
```

(insert this paragraph before the final `-->` that closes the whole comment block, replacing that single closing `-->` with the paragraph above ending in its own `-->`).

- [ ] **Step 6: Update `ShortcutsWindow.axaml`**

In `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:50,52`, change:

```xml
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Show Auto Inactives (not yet implemented)" TextWrapping="Wrap" />
```

to:

```xml
                <TextBlock Grid.Row="0" Grid.Column="1" Text="Show Auto Inactives" />
```

and:

```xml
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Show Auto Actives (not yet implemented)" TextWrapping="Wrap" />
```

to:

```xml
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Show Auto Actives" />
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter MainWindowKeyBindingsTests`
Expected: PASS, all tests including the 5 new ones.

- [ ] **Step 8: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Navigation.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
git commit -m "S12 Task 3: wire Ctrl+A/Ctrl+I auto actives/inactives toggle"
```

---

## Task 4: Corrupted / out-of-import-range experiment lists + dialogs (Ctrl+Alt+F / Ctrl+Alt+Y)

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (new collections, `LoadDatasetAsync` plumbing, two new commands)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml:56` (two new unguarded bindings)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:41-44`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs` (5 new tests)

**Interfaces:**
- Produces: `MainViewModel.CorruptedPeaklistExperiments`, `MainViewModel.OutOfImportRangeExperiments` (`ObservableCollection<string>`); `MainViewModel.ShowCorruptedPeaklistExpCommand`, `MainViewModel.ShowOutOfImportRangeExpCommand` (`IAsyncRelayCommand`, `CanExecute` = own list non-empty).
- Consumes: `_infoDialogService` (Task 1), existing `LoadDatasetAsync` loop (`MainViewModel.cs:212-240`).

- [ ] **Step 1: Write the failing tests**

In `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs`, add after the existing `LoadDatasetAsync_sorts_experiments_by_ExpNumber_not_directory_listing_order` test (after line 62):

```csharp

    [Fact]
    public async Task LoadDatasetAsync_tracks_folders_missing_peaklist_xml_as_corrupted()
    {
        string root = Directory.CreateTempSubdirectory("csp_nav_test_").FullName;
        string refXml = WritePeaklistXml("1", Path.Combine(root, "ref_ds"));

        string dsRoot = Path.Combine(root, "ds");
        WritePeaklistXml("1", dsRoot);
        Directory.CreateDirectory(Path.Combine(dsRoot, "2"));

        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(), new NullInfoDialogService(), new SettingsService());
        await vm.LoadReferenceCommand.ExecuteAsync(null);
        await vm.LoadDatasetCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "2" }, vm.CorruptedPeaklistExperiments);
    }

    [Fact]
    public async Task LoadDatasetAsync_tracks_malformed_peaklist_xml_as_corrupted()
    {
        string root = Directory.CreateTempSubdirectory("csp_nav_test_").FullName;
        string refXml = WritePeaklistXml("1", Path.Combine(root, "ref_ds"));

        string dsRoot = Path.Combine(root, "ds");
        string badDir = Path.Combine(dsRoot, "3", "pdata", "1");
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "peaklist.xml"), "not xml at all <<<");

        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(), new NullInfoDialogService(), new SettingsService());
        await vm.LoadReferenceCommand.ExecuteAsync(null);
        await vm.LoadDatasetCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "3" }, vm.CorruptedPeaklistExperiments);
    }

    [Fact]
    public async Task LoadDatasetAsync_tracks_experiments_emptied_by_import_range_as_out_of_range()
    {
        string root = Directory.CreateTempSubdirectory("csp_nav_test_").FullName;
        string refXml = WritePeaklistXml("1", Path.Combine(root, "ref_ds"));

        string dsRoot = Path.Combine(root, "ds");
        string subfolder = Path.Combine(dsRoot, "4", "pdata", "1");
        Directory.CreateDirectory(subfolder);
        // F1=1.0 is well outside the default NMin=100/NMax=140 import range,
        // so PeaklistImporter filters this experiment's only peak out entirely.
        File.WriteAllText(Path.Combine(subfolder, "peaklist.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <peaklist>
              <PeakList2D>
                <Peak2D F1="1.0" F2="8.0" intensity="9000" Number="1"/>
              </PeakList2D>
            </peaklist>
            """);

        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(), new NullInfoDialogService(), new SettingsService());
        await vm.LoadReferenceCommand.ExecuteAsync(null);
        await vm.LoadDatasetCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "4" }, vm.OutOfImportRangeExperiments);
    }

    [Fact]
    public void ShowCorruptedPeaklistExpCommand_disabled_when_empty_enabled_when_populated()
    {
        var vm = new MainViewModel();

        Assert.False(vm.ShowCorruptedPeaklistExpCommand.CanExecute(null));

        vm.CorruptedPeaklistExperiments.Add("3");

        Assert.True(vm.ShowCorruptedPeaklistExpCommand.CanExecute(null));
    }

    private sealed class RecordingInfoDialogService : IInfoDialogService
    {
        public string? LastTitle;
        public string? LastMessage;

        public Task ShowAsync(string title, string message)
        {
            LastTitle = title;
            LastMessage = message;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ShowOutOfImportRangeExpCommand_shows_the_out_of_range_experiment_list()
    {
        var infoDialog = new RecordingInfoDialogService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(),
            infoDialog, new SettingsService());
        vm.OutOfImportRangeExperiments.Add("4");
        vm.OutOfImportRangeExperiments.Add("5");

        await vm.ShowOutOfImportRangeExpCommand.ExecuteAsync(null);

        Assert.Equal($"4{System.Environment.NewLine}5", infoDialog.LastMessage);
    }
```

Add `using CspAnalyzer.Desktop.Services;` to the top of `MainViewModelNavigationTests.cs` if not already present (it already is, per the existing `IFilePickerService` usage).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter MainViewModelNavigationTests`
Expected: FAIL — `CorruptedPeaklistExperiments`/`OutOfImportRangeExperiments`/`ShowCorruptedPeaklistExpCommand`/`ShowOutOfImportRangeExpCommand` don't exist (compile error).

- [ ] **Step 3: Add the two collections and wire `LoadDatasetAsync`**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, after `public ObservableCollection<PeaklistSpectrum> DatasetSpectra { get; } = new();` (line 108), add:

```csharp
    public ObservableCollection<string> CorruptedPeaklistExperiments { get; } = new();

    public ObservableCollection<string> OutOfImportRangeExperiments { get; } = new();
```

In `LoadDatasetAsync`, change line 198 from:

```csharp
        DatasetSpectra.Clear();
```

to:

```csharp
        DatasetSpectra.Clear();
        CorruptedPeaklistExperiments.Clear();
        OutOfImportRangeExperiments.Clear();
```

Change the loop body (lines 212-240) from:

```csharp
        foreach (string dir in subfolders)
        {
            string peaklistPath = Path.Combine(dir, "pdata", "1", "peaklist.xml");
            if (!File.Exists(peaklistPath))
            {
                continue;
            }

            found++;
            PeaklistSpectrum spectrum;
            try
            {
                spectrum = PeaklistImporter.Import(peaklistPath, DatasetFilter, jsonData: "Experiment");
            }
            catch (System.Xml.XmlException)
            {
                corruptedXml++;
                continue;
            }

            validXml++;
            if (spectrum.Peaklist.Count == 0)
            {
                outOfRange++;
                continue;
            }

            DatasetSpectra.Add(spectrum);
        }
```

to:

```csharp
        foreach (string dir in subfolders)
        {
            string peaklistPath = Path.Combine(dir, "pdata", "1", "peaklist.xml");
            if (!File.Exists(peaklistPath))
            {
                CorruptedPeaklistExperiments.Add(Path.GetFileName(dir));
                continue;
            }

            found++;
            PeaklistSpectrum spectrum;
            try
            {
                spectrum = PeaklistImporter.Import(peaklistPath, DatasetFilter, jsonData: "Experiment");
            }
            catch (System.Xml.XmlException)
            {
                corruptedXml++;
                CorruptedPeaklistExperiments.Add(Path.GetFileName(dir));
                continue;
            }

            validXml++;
            if (spectrum.Peaklist.Count == 0)
            {
                outOfRange++;
                OutOfImportRangeExperiments.Add(spectrum.ExpNumber.ToString());
                continue;
            }

            DatasetSpectra.Add(spectrum);
        }
```

After `ValidExperimentsCount = DatasetSpectra.Count;` (line 246), add:

```csharp
        ShowCorruptedPeaklistExpCommand.NotifyCanExecuteChanged();
        ShowOutOfImportRangeExpCommand.NotifyCanExecuteChanged();
```

- [ ] **Step 4: Add the two commands**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, after `OpenResultsWindow` (end of file, after line 381), add:

```csharp

    private bool CanShowCorruptedPeaklistExp() => CorruptedPeaklistExperiments.Count > 0;

    [RelayCommand(CanExecute = nameof(CanShowCorruptedPeaklistExp))]
    private Task ShowCorruptedPeaklistExpAsync() =>
        _infoDialogService.ShowAsync("Corrupted Peaklist Experiments", string.Join(Environment.NewLine, CorruptedPeaklistExperiments));

    private bool CanShowOutOfImportRangeExp() => OutOfImportRangeExperiments.Count > 0;

    [RelayCommand(CanExecute = nameof(CanShowOutOfImportRangeExp))]
    private Task ShowOutOfImportRangeExpAsync() =>
        _infoDialogService.ShowAsync("Out-of-Import-Range Experiments", string.Join(Environment.NewLine, OutOfImportRangeExperiments));
```

(`Environment.NewLine` requires `using System;`, already present at the top of `MainViewModel.cs`.)

Note: unlike legacy's `reference_loaded && ds_loaded && <list>.Any()` guard, `CanExecute` here checks only the list itself — both lists are populated exclusively inside `LoadDatasetAsync`, which already requires `IsReferenceLoaded` before it runs, so the extra legacy conditions are redundant in this port and dropping them avoids a real edge case the literal guard would introduce (a dataset where *every* experiment is corrupted/out-of-range would leave `DatasetSpectra.Count == 0`, wrongly disabling the button under a literal `DatasetSpectra.Count > 0` guard even though the list has content to show).

- [ ] **Step 5: Add the unguarded XAML bindings**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml:56`, after the `Ctrl+Alt+Space` line added in Task 2, add:

```xml
        <KeyBinding Gesture="Ctrl+Alt+F" Command="{Binding ShowCorruptedPeaklistExpCommand}" />
        <KeyBinding Gesture="Ctrl+Alt+Y" Command="{Binding ShowOutOfImportRangeExpCommand}" />
```

- [ ] **Step 6: Update `ShortcutsWindow.axaml`**

In `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:41-44`, change:

```xml
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Ctrl+Alt+O" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Show Out-of-Import Range Exp. (not yet implemented)" TextWrapping="Wrap" />
                <TextBlock Grid.Row="3" Grid.Column="0" Text="Ctrl+Alt+F" />
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Show Corrupted Peaklist Exp. (not yet implemented)" TextWrapping="Wrap" />
```

to:

```xml
                <TextBlock Grid.Row="2" Grid.Column="0" Text="Ctrl+Alt+Y" />
                <TextBlock Grid.Row="2" Grid.Column="1" Text="Show Out-of-Import Range Exp." />
                <TextBlock Grid.Row="3" Grid.Column="0" Text="Ctrl+Alt+F" />
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Show Corrupted Peaklist Exp." />
```

(the gesture label changes from `Ctrl+Alt+O` to `Ctrl+Alt+Y` because this port's `Ctrl+Alt+O` is already `ResetAllImportAndThresholdControlsCommand` — legacy's gesture doesn't apply here.)

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter MainViewModelNavigationTests`
Expected: PASS, all tests including the 5 new ones.

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS (confirms `LoadDatasetAsync`'s existing count-based tests, if any exist elsewhere, still pass unchanged).

- [ ] **Step 9: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs
git commit -m "S12 Task 4: track corrupted/out-of-range experiments, wire Ctrl+Alt+F/Ctrl+Alt+Y info dialogs"
```

---

## Task 5: Enter (load reference/dataset) + bare I (About)

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (new `LoadReferenceOrDataset` command)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs` (two new guarded bindings + comment)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:19-20`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs` (5 new tests + 1 new fake)

**Interfaces:**
- Produces: `MainViewModel.LoadReferenceOrDatasetCommand` (`IAsyncRelayCommand`, no `CanExecute` beyond the guard).
- Consumes: existing `IsReferenceLoaded`, private `LoadReferenceAsync()`/`LoadDatasetAsync()` methods, existing `OpenAboutWindowCommand` (S11c).

- [ ] **Step 1: Write the failing tests**

In `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`, add a new private fake near the existing `RecordingAboutWindowService`-style fakes at the bottom of the class (before the closing `}`):

```csharp
    private sealed class RecordingFilePickerService : IFilePickerService
    {
        public int XmlPickCount;
        public int FolderPickCount;
        public Task<string?> PickXmlFileAsync(string title) { XmlPickCount++; return Task.FromResult<string?>(null); }
        public Task<string?> PickFolderAsync(string title) { FolderPickCount++; return Task.FromResult<string?>(null); }
        public Task<string?> PickSaveFileAsync(string suggestedFileName, string extension) => Task.FromResult<string?>(null);
    }

    private sealed class RecordingAboutWindowService : IAboutWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    [AvaloniaFact]
    public async Task Enter_NotFocused_WhenNoReferenceLoaded_LoadsReference()
    {
        var picker = new RecordingFilePickerService();
        var vm = new MainViewModel(
            picker, new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(),
            new NullInfoDialogService(), new SettingsService());
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await ((IAsyncRelayCommand)vm.LoadReferenceOrDatasetCommand).ExecutionTask!;

        Assert.Equal(1, picker.XmlPickCount);
        Assert.Equal(0, picker.FolderPickCount);
    }

    [AvaloniaFact]
    public async Task Enter_NotFocused_WhenReferenceLoaded_LoadsDataset()
    {
        var picker = new RecordingFilePickerService();
        var vm = new MainViewModel(
            picker, new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(),
            new NullInfoDialogService(), new SettingsService());
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ref", Peaklist = new() };
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await ((IAsyncRelayCommand)vm.LoadReferenceOrDatasetCommand).ExecutionTask!;

        Assert.Equal(0, picker.XmlPickCount);
        Assert.Equal(1, picker.FolderPickCount);
    }

    [AvaloniaFact]
    public void Enter_GuardedWhileTextBoxFocused_DoesNotPickAnything()
    {
        var picker = new RecordingFilePickerService();
        var vm = new MainViewModel(
            picker, new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(),
            new NullInfoDialogService(), new SettingsService());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.Equal(0, picker.XmlPickCount);
        Assert.Equal(0, picker.FolderPickCount);
    }

    [AvaloniaFact]
    public void I_OpensAboutWindow()
    {
        var recording = new RecordingAboutWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            recording, new NullShortcutsWindowService(), new NullHelpWindowService(),
            new NullInfoDialogService(), new SettingsService());
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.I, RawInputModifiers.None);

        Assert.Equal(1, recording.ShowCallCount);
    }

    [AvaloniaFact]
    public void I_GuardedWhileTextBoxFocused_DoesNotOpenAboutWindow()
    {
        var recording = new RecordingAboutWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            recording, new NullShortcutsWindowService(), new NullHelpWindowService(),
            new NullInfoDialogService(), new SettingsService());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var nMinBox = window.FindControl<TextBox>("NMinTextBox")!;
        nMinBox.Focus();

        window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.I, RawInputModifiers.None);

        Assert.Equal(0, recording.ShowCallCount);
    }
```

Note: `Ctrl+I` (Task 3, toggle inactives filter) and bare `I` (this task, About) are distinct `KeyGesture`s (different `KeyModifiers`) and don't collide.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter "Enter_NotFocused_WhenNoReferenceLoaded_LoadsReference|I_OpensAboutWindow"`
Expected: FAIL — `LoadReferenceOrDatasetCommand` doesn't exist (compile error); `I_OpensAboutWindow` fails at runtime once it compiles (no `I` binding exists yet).

- [ ] **Step 3: Add the `LoadReferenceOrDataset` command**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, after `LoadDatasetAsync` (after line 266, before `private bool CanRun()` at line 268), add:

```csharp

    [RelayCommand]
    private async Task LoadReferenceOrDataset()
    {
        if (!IsReferenceLoaded)
        {
            await LoadReferenceAsync();
        }
        else
        {
            await LoadDatasetAsync();
        }
    }
```

- [ ] **Step 4: Add the guarded key bindings**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs`, after the two Task 3 bindings (Ctrl+A/Ctrl+I), add:

```csharp
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Enter), Command = GuardedViewModelCommand(vm => vm.LoadReferenceOrDatasetCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.I), Command = GuardedViewModelCommand(vm => vm.OpenAboutWindowCommand) });
```

Extend the bare-key doc comment (the "R, N, D, S, A, T, and the arrow keys ..." paragraph) to mention the two new bare gestures — change its opening line from:

```csharp
    - R, N, D, S, A, T, and the arrow keys (Right/Left/Down/Up): these
```

to:

```csharp
    - R, N, D, S, A, T, I, Enter, and the arrow keys (Right/Left/Down/Up): these
```

- [ ] **Step 5: Update `ShortcutsWindow.axaml`**

In `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:20`, change:

```xml
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Load Reference/Dataset, Show Information Window (not yet implemented)" TextWrapping="Wrap" />
```

to:

```xml
                <TextBlock Grid.Row="3" Grid.Column="1" Text="Load Reference/Dataset, Show Information Window" />
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter MainWindowKeyBindingsTests`
Expected: PASS, all tests including the 5 new ones.

- [ ] **Step 7: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
git commit -m "S12 Task 5: wire Enter (load reference/dataset) and bare I (About)"
```

---

## Task 6: Reset Application (Ctrl+R)

Confirm → clear all loaded/derived state → re-apply persisted settings via the existing `ApplySettings`/`SettingsService.Load()` (S11b) — the in-memory equivalent of what a real process restart would now actually do, per the approved design (no cross-platform process-relaunch risk).

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.ResetApplication.cs`
- Create: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelResetApplicationTests.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs` (guarded binding + comment)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:72`
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs` (2 new tests)

**Interfaces:**
- Produces: `MainViewModel.ResetApplicationCommand` (`IAsyncRelayCommand`, no `CanExecute`).
- Consumes: `_confirmDialogService`, `_settingsService` (Task 1), existing `ApplySettings(AppSettings)` (`MainViewModel.Settings.cs`), existing `BuildOverlayAxes()`/`RaiseNavigationChanged()`.

- [ ] **Step 1: Write the failing tests**

Create `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelResetApplicationTests.cs`:

```csharp
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelResetApplicationTests
{
    private sealed class DecliningConfirmDialogService : IConfirmDialogService
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
    }

    private static MainViewModel MakeFullyLoadedViewModel(IConfirmDialogService confirmDialog, SettingsService settingsService)
    {
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), confirmDialog,
            new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(),
            new NullInfoDialogService(), settingsService);

        vm.ReferenceSpectrum = new PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ref",
            Peaklist = { new Peak { Number = 1, F1 = 120, F2 = 8, Intensity = 9000 } },
        };
        vm.DatasetSpectra.Add(new PeaklistSpectrum
        {
            ExpNumber = 2,
            DsName = "ds",
            Peaklist = { new Peak { Number = 1, F1 = 121, F2 = 8, Intensity = 9000 } },
            UserSelection = "ACTIVE (MAN)",
        });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 2, IsActive = true, ActivePseudoprobability = 0.9 });
        vm.CorruptedPeaklistExperiments.Add("3");
        vm.OutOfImportRangeExperiments.Add("4");
        vm.NMin = 1;
        vm.NMax = 2;
        vm.RaiseNavigationChanged();
        return vm;
    }

    [Fact]
    public async Task ResetApplicationCommand_declined_leaves_everything_loaded()
    {
        var vm = MakeFullyLoadedViewModel(new DecliningConfirmDialogService(), new SettingsService(Path.GetTempFileName()));

        await ((IAsyncRelayCommand)vm.ResetApplicationCommand).ExecuteAsync(null);

        Assert.True(vm.IsReferenceLoaded);
        Assert.Single(vm.DatasetSpectra);
        Assert.Single(vm.RunResults);
    }

    [Fact]
    public async Task ResetApplicationCommand_confirmed_clears_loaded_data_and_reapplies_persisted_settings()
    {
        string settingsPath = Path.Combine(Directory.CreateTempSubdirectory("csp_reset_test_").FullName, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        settingsService.Save(new AppSettings { NMin = 77, NMax = 88, HMin = 3, HMax = 9, ReferenceIntensityThreshold = 1111, DatasetIntensityThreshold = 2222 });

        var vm = MakeFullyLoadedViewModel(new NullConfirmDialogService(), settingsService);

        await ((IAsyncRelayCommand)vm.ResetApplicationCommand).ExecuteAsync(null);

        Assert.False(vm.IsReferenceLoaded);
        Assert.Empty(vm.DatasetSpectra);
        Assert.Empty(vm.RunResults);
        Assert.Empty(vm.CorruptedPeaklistExperiments);
        Assert.Empty(vm.OutOfImportRangeExperiments);
        Assert.Equal("No Reference Loaded", vm.ReferenceStatusText);
        Assert.Equal("No Dataset Loaded", vm.DatasetStatusText);
        Assert.Equal(77, vm.NMin);
        Assert.Equal(88, vm.NMax);
        Assert.Equal(3, vm.HMin);
        Assert.Equal(9, vm.HMax);
        Assert.Equal(1111, vm.ReferenceIntensityThreshold);
        Assert.Equal(2222, vm.DatasetIntensityThreshold);
        Assert.False(vm.RunCommand.CanExecute(null));
        Assert.False(vm.OpenResultsWindowCommand.CanExecute(null));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter MainViewModelResetApplicationTests`
Expected: FAIL — `ResetApplicationCommand` doesn't exist (compile error).

- [ ] **Step 3: Implement `ResetApplicationAsync`**

Create `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.ResetApplication.cs`:

```csharp
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.Desktop.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.VisualElements;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S12: Ctrl+R "Reset Application" (CSPv2/Form1.cs:2744-2758's
/// buttonReset_Click, which called WinForms' Application.Restart()). .NET 8
/// has no equivalent that behaves identically across a self-contained
/// publish on every platform, so this clears all loaded/derived state back
/// to construction defaults and re-applies persisted settings via
/// ApplySettings - the same two things a real process restart would
/// actually do (App.axaml.cs re-runs SettingsService.Load()/ApplySettings
/// on every launch), without the cross-platform relaunch risk.
/// </summary>
public partial class MainViewModel
{
    [RelayCommand]
    private async Task ResetApplicationAsync()
    {
        bool confirmed = await _confirmDialogService.ConfirmAsync(
            "Reset Application",
            "This will clear the loaded reference, dataset, and all results, and reload your saved settings." +
            Environment.NewLine + Environment.NewLine + "Continue?");

        if (!confirmed)
        {
            return;
        }

        ReferenceSpectrum = null;
        ReferenceStatusText = "No Reference Loaded";
        ReferencePeakCount = 0;
        ReferenceMinIntensity = 0;
        ReferenceMaxIntensity = 0;
        OnPropertyChanged(nameof(IsReferenceLoaded));

        DatasetSpectra.Clear();
        DatasetStatusText = "No Dataset Loaded";
        TotalSubfoldersFound = 0;
        PeaklistFilesFoundCount = 0;
        ValidXmlPeaklistCount = 0;
        CorruptedXmlPeaklistCount = 0;
        OutOfPeakImportRangeCount = 0;
        ValidExperimentsCount = 0;
        CorruptedPeaklistExperiments.Clear();
        OutOfImportRangeExperiments.Clear();
        DatasetAveragePeakCount = 0;
        DatasetAverageMinIntensity = 0;
        DatasetAverageMaxIntensity = 0;

        RunResults.Clear();
        IsRunning = false;
        RunCompletedSuccessfully = false;
        RunStatusText = "";

        CurrentFilter = null;
        CurrentIndex = 0;
        GoToExperimentText = "";
        GoToStatusText = "";

        // Bypass the ManualProbabilityThreshold setter - same reason
        // ApplySettings/RunAsync do below: OnManualProbabilityThresholdChanged
        // rebuilds charts/gauges that are about to be cleared anyway.
        _manualProbabilityThreshold = 0.5;
        OnPropertyChanged(nameof(ManualProbabilityThreshold));

        AppSettings settings = _settingsService.Load();
        ApplySettings(settings);

        PeakDiffSeries = Array.Empty<ISeries>();
        PeakDiffXAxes = Array.Empty<Axis>();
        PeakDiffYAxes = Array.Empty<Axis>();
        PeakDiffSections = Array.Empty<RectangularSection>();
        PeakDiffAnnotations = Array.Empty<LabelVisual>();

        ProbabilitySeries = Array.Empty<ISeries>();
        ProbabilityXAxes = Array.Empty<Axis>();
        ProbabilityYAxes = Array.Empty<Axis>();
        ProbabilitySections = Array.Empty<RectangularSection>();
        ProbabilityAnnotations = Array.Empty<LabelVisual>();

        ActivesGaugeSeries = Array.Empty<ISeries>();
        InactivesGaugeSeries = Array.Empty<ISeries>();
        ActivesAutoCount = 0;
        InactivesAutoCount = 0;

        BuildOverlayAxes();
        RaiseNavigationChanged();

        RunCommand.NotifyCanExecuteChanged();
        OpenResultsWindowCommand.NotifyCanExecuteChanged();
        ToggleAutoActivesFilterCommand.NotifyCanExecuteChanged();
        ToggleAutoInactivesFilterCommand.NotifyCanExecuteChanged();
        ShowCorruptedPeaklistExpCommand.NotifyCanExecuteChanged();
        ShowOutOfImportRangeExpCommand.NotifyCanExecuteChanged();
    }
}
```

- [ ] **Step 4: Run the new tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter MainViewModelResetApplicationTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Write the failing key-binding tests**

In `dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs`, add:

```csharp
    [AvaloniaFact]
    public async Task CtrlR_NotFocused_ResetsApplication()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ref", Peaklist = new() };

        window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.R, RawInputModifiers.Control);
        await ((IAsyncRelayCommand)vm.ResetApplicationCommand).ExecutionTask!;

        Assert.False(vm.IsReferenceLoaded);
    }

    [AvaloniaFact]
    public void CtrlR_GuardedWhileTextBoxFocused_DoesNotResetApplication()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ref", Peaklist = new() };
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.R, RawInputModifiers.Control);

        Assert.True(vm.IsReferenceLoaded);
    }
```

(`NewWindow()` uses the parameterless `new MainViewModel()`, whose default `NullConfirmDialogService` always confirms `true`, so `CtrlR_NotFocused_ResetsApplication` exercises the full reset.)

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter "CtrlR_NotFocused_ResetsApplication"`
Expected: FAIL — no `Ctrl+R` binding exists yet (assertion fails: `IsReferenceLoaded` stays `true`).

- [ ] **Step 7: Add the guarded key binding**

In `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs`, after the `Enter`/`I` bindings added in Task 5, add:

```csharp
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.R, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ResetApplicationCommand) });
```

Extend the Ctrl+C/Ctrl+Y/Ctrl+X/Ctrl+A/Ctrl+I doc-comment paragraph (from Tasks 2 and 3) one more time — change its opening line from:

```csharp
    - Ctrl+C (ResetBarChartZoomCommand), Ctrl+Y (ResetOverlayZoomCommand), and
      Ctrl+X (FitOverlayZoomToReferenceCommand): same defect class as the bare
```

to:

```csharp
    - Ctrl+C (ResetBarChartZoomCommand), Ctrl+Y (ResetOverlayZoomCommand),
      Ctrl+X (FitOverlayZoomToReferenceCommand), and Ctrl+R
      (ResetApplicationCommand): same defect class as the bare
```

- [ ] **Step 8: Update `ShortcutsWindow.axaml`**

In `dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml:72`, change:

```xml
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Reset Application (not yet implemented)" TextWrapping="Wrap" />
```

to:

```xml
                <TextBlock Grid.Row="1" Grid.Column="1" Text="Reset Application" />
```

- [ ] **Step 9: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter MainWindowKeyBindingsTests`
Expected: PASS, all tests including the 2 new ones.

- [ ] **Step 10: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.ResetApplication.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelResetApplicationTests.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml dotnet/CspAnalyzer.Desktop.Tests/MainWindowKeyBindingsTests.cs
git commit -m "S12 Task 6: wire Ctrl+R Reset Application (in-memory reset + reapply persisted settings)"
```

---

## Task 7: Final doc-test flip, full suite run, Linux smoke test, Windows-risk audit

`ShortcutsWindowTests.cs` has two assertions from S11d that still expect "(not yet implemented)" for rows Task 3 and Task 5 just wired — these must flip or the suite lies about what's shipped.

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs`
- Modify: `docs/superpowers/SESSIONS.md` (check off S12)

**Interfaces:**
- Consumes: everything from Tasks 1-6.

- [ ] **Step 1: Update the stale assertions**

In `dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs`, replace lines 23 and 27:

```csharp
        Assert.Contains(texts, t => t.Contains("Show Auto Actives") && t.Contains("not yet implemented"));
```

```csharp
        Assert.Contains(texts, t => t.Contains("Show Information Window") && t.Contains("not yet implemented"));
```

with:

```csharp
        Assert.Contains(texts, t => t.Contains("Show Auto Actives") && !t.Contains("not yet implemented"));
```

```csharp
        Assert.Contains(texts, t => t.Contains("Show Information Window") && !t.Contains("not yet implemented"));
```

Also add one assertion proving no row anywhere still says "not yet implemented" (the strongest possible check that Task 1-6 covered every row this plan set out to close), appended at the end of the same test method body, before its closing `}`:

```csharp
        Assert.DoesNotContain(texts, t => t.Contains("not yet implemented"));
```

- [ ] **Step 2: Run the test to verify it fails first (sanity on the added assertion), then passes**

Run: `dotnet test dotnet/CspAnalyzer.Desktop.Tests --filter ShortcutsWindowTests`
Expected: PASS. (If this fails, it means some row's "(not yet implemented)" suffix was missed in an earlier task — go back and find it via `grep -n "not yet implemented" dotnet/CspAnalyzer.Desktop/Views/ShortcutsWindow.axaml` before proceeding.)

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS, full green, no regressions from Task 1-7's cumulative changes.

- [ ] **Step 4: Build and run the real app for a Linux smoke test**

Run: `dotnet build dotnet/CspAnalyzer.sln` then launch `dotnet run --project dotnet/CspAnalyzer.Desktop` against the local `CSPv2/Demo-dataset` (git-ignored, kept locally per S5). Manually exercise, and screenshot:
- `Enter` with nothing loaded → reference file picker opens; after loading reference, `Enter` again → dataset folder picker opens.
- Bare `I` → About window opens.
- After a run: `Ctrl+A`/`Ctrl+I` toggle the Actives/Inactives checkboxes; `Ctrl+C` resets bar-chart zoom after zooming in; `Ctrl+Y` resets overlay zoom after panning; `Ctrl+Alt+Space` resets both after zooming both.
- If the demo dataset has (or a throwaway git-ignored fixture folder is temporarily pointed at) at least one corrupted or out-of-range experiment: `Ctrl+Alt+F`/`Ctrl+Alt+Y` open the new info dialogs with the right content.
- `Ctrl+R` → confirm dialog → after confirming, the UI returns to its just-launched state.

Expected: every gesture above behaves as described; no exceptions in the terminal output.

- [ ] **Step 5: Windows-specific risk audit (static, not run-verified)**

Review every file touched across Tasks 1-6 for Windows-specific risk:
- `InfoDialog`/`AvaloniaInfoDialogService`: same `Window.ShowDialog` pattern as the pre-existing `ConfirmDialog`/`AvaloniaConfirmDialogService` (already shipped and presumably fine on Windows) — no new platform-specific API surface.
- `SettingsService` (now also constructor-injected into `MainViewModel`): unchanged from S11b, already uses `Environment.SpecialFolder.ApplicationData` (cross-platform-safe).
- `ResetApplicationAsync`: pure in-memory state mutation, no process/OS calls — this was the entire point of choosing the in-memory design over a real process relaunch.
- `Path.GetFileName(dir)` (Task 4): works identically on Windows/Linux path separators via .NET's `Path` APIs.
- No new file-system paths, process launches, or OS-specific APIs were introduced anywhere in Tasks 1-6.

Expected conclusion: no Windows-specific risk identified in this session's changes; still flagged as unverified (no Windows box available), not Windows-tested.

- [ ] **Step 6: Update `SESSIONS.md`**

In `docs/superpowers/SESSIONS.md`, change:

```markdown
- [ ] **S12** — Polish, cross-platform smoke test (Linux + Windows), fix platform gaps.
```

to (check the box, summarize what shipped, matching the style of every prior session entry):

```markdown
- [x] **S12** — Ported the last 9 legacy shortcuts (Enter/I, Ctrl+A, Ctrl+I,
  Ctrl+Y, Ctrl+Alt+Space, Ctrl+Alt+F, Ctrl+Alt+Y, Ctrl+R) and fixed a real
  S11c bug: Ctrl+C and Ctrl+Y's zoom-reset bindings were swapped (legacy
  Ctrl+C resets the bar charts, Ctrl+Y resets the overlay chart; S11c had
  bound the overlay-reset behavior to Ctrl+C and left Ctrl+Y unbound).
  `LoadDatasetAsync` now tracks corrupted/out-of-range experiment names (not
  just counts) behind two new `IInfoDialogService`-backed dialogs. Reset
  Application (Ctrl+R) is an in-memory full-state reset that re-applies
  persisted settings via the existing S11b `ApplySettings`/`SettingsService`
  rather than a true process relaunch (no cross-platform `Application.
  Restart()` equivalent in .NET 8). `MainViewModel`'s constructor grew to 8
  params (added `IInfoDialogService`, `SettingsService`). Windows verified
  only via static code audit (no risk found) - no Windows box on this dev
  machine; smoke-tested on Linux via a real run against `CSPv2/Demo-dataset`
  with screenshots, same as every prior UI session. See
  `docs/superpowers/specs/2026-07-23-sub-project-3-s12-remaining-shortcuts-polish-design.md`
  and `docs/superpowers/plans/2026-07-23-s12-remaining-shortcuts-polish.md`.
```

- [ ] **Step 7: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop.Tests/ShortcutsWindowTests.cs docs/superpowers/SESSIONS.md
git commit -m "S12 Task 7: flip stale ShortcutsWindowTests assertions, mark S12 complete in SESSIONS.md"
```
