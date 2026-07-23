# S10b: Form1 Charts + Manual-Override Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port `CSPv2/Form1.cs`'s embedded charts (peak-difference bar chart, probability bar chart, spectra-overlay scatter, actives/inactives gauges, manual-results bar chart) and its manual-override workflow (player navigation, mark active/inactive, actives/inactives filtering) into `MainViewModel`/`MainWindow.axaml`, replacing the last placeholders left over from S7.

**Architecture:** All new state lives in `MainViewModel`, split across three new partial-class files by responsibility (`MainViewModel.Navigation.cs`, `MainViewModel.ManualOverride.cs`, `MainViewModel.Charts.cs`) plus edits to the existing `MainViewModel.cs` to call into them at the right lifecycle points (after dataset load, after a run completes). A new `CspAnalyzer.Desktop.Tests` xunit project covers all of this view-model logic directly (no Avalonia window needed — `PeaklistSpectrum.UserSelection` and the LiveChartsCore types used here are plain .NET, same reasoning as `BackendInterop.Tests`).

**Tech Stack:** .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm 8.4.2, LiveChartsCore.SkiaSharpView.Avalonia 2.0.5, xunit.

## Global Constraints

- Chart fidelity: full fidelity — threshold-zone shading, floating text callouts, bar coloring by zone, X-axis zoom-sync between the two bar charts, click-a-bar-to-navigate. (User choice, brainstorming session.)
- Actives/Inactives counts render as LiveChartsCore solid gauges via `GaugeGenerator.BuildSolidGauge` + `PieChart` (verified API below — **not** a `RadialGaugeSeries`/`GaugeBuilder` control, which does not exist in this package version).
- `ResetAllManualFlagsCommand`'s confirmation is a minimal hand-rolled Avalonia dialog behind `IConfirmDialogService` — no new NuGet dependency.
- Spectra-overlay chart gets both a reset-zoom-to-import-bounds button and a fit-zoom-to-reference button (user's last answer added the second one).
- `DatasetSpectra` must be sorted by `ExpNumber` after load — every bar-chart index / player `CurrentIndex` / experiment-number label depends on a stable index↔experiment mapping.
- All legacy color values (`ActiveAutoFill`, `InactiveAutoFill`, `BrokenSpectrum`, `FineSpectrum`, `CheckSpectrum`, `DangerBrush`, manual-status colors) are ported at their exact original ARGB values (`CSPv2/Form1.cs:100-119`), converted to SkiaSharp's `(r,g,b,a)` constructor order — same convention `ResultsViewModel.cs:36-40` already documents and uses.
- Spec: `docs/superpowers/specs/2026-07-23-sub-project-3-s10b-form-charts-manual-override-design.md`.

## Verified LiveChartsCore 2.0.5 API (read this before writing chart code)

S7's session hit a costly gotcha from guessing an API that didn't exist (Avalonia 12 template defaults). To avoid repeating that here, every LiveChartsCore type/member used in this plan was confirmed by loading the actual installed package and reflecting on it (not from memory/docs) before this plan was written:

- `LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart`: `Series`, `XAxes`, `YAxes` (plural, not legacy's singular `AxisX`/`AxisY`), `Sections` (for threshold-zone shading), `VisualElements` (for floating text), `ZoomMode`, event `ChartPointPointerDown(IChartView chart, ChartPoint point)` (`LiveChartsCore.Kernel.Events.ChartPointHandler` delegate) — this is the click-to-navigate hook; `point.Index` gives the bar/point's index directly.
- `LiveChartsCore.SkiaSharpView.Axis`: `MinLimit`/`MaxLimit` (`double?`, replaces legacy `MinValue`/`MaxValue`), `Labels` (`IList<string>`), `LabelsRotation`, `Name` (replaces legacy `Title`), `SharedWith` (`IEnumerable<ICartesianAxis>`) — **this is the modern zoom-sync mechanism**, replacing legacy's manual `Axis_RangeChanged` handler: set two axes' `SharedWith` to point at each other and the library keeps their pan/zoom in sync natively.
- `LiveChartsCore.SkiaSharpView.RectangularSection`: `Yi`/`Yj`/`Xi`/`Xj` (`double?`, null = full chart width/height), `Fill` — this is the threshold-zone-shading primitive (legacy's `AxisSection`).
- `LiveChartsCore.SkiaSharpView.VisualElements.LabelVisual`: `X`/`Y` (`double`, chart-value units), `Text`, `TextSize`, `Paint` — the floating-text-callout primitive (legacy's `VisualElement`/`TextBlock`).
- `LiveChartsCore.SkiaSharpView.ColumnSeries<T>` / `ScatterSeries<T>`: `Values` (public get/set, accepts any array), `Fill`, `Name`. Raw `int[]`/`double[]` work directly as `Values` for `ColumnSeries<int>`/`ColumnSeries<double>` (same pattern `ResultsViewModel.cs` already uses for `PieSeries<int>`).
- `LiveChartsCore.Defaults.WeightedPoint(double? x, double? y, double? weight)` — the scatter-point type with an intensity-driven size, replacing legacy's WPF-only `ScatterPoint`.
- `LiveChartsCore.SkiaSharpView.Extensions.GaugeGenerator.BuildSolidGauge(params GaugeItem[])` returns `PieSeries<ObservableValue>[]`, meant to be bound into a `PieChart.Series`; `GaugeItem(double value, Action<PieSeries<ObservableValue>> builder)`. The Avalonia `PieChart` control itself carries `MinValue`/`MaxValue` (bind `MaxValue` to the total count) — this is the actual gauge mechanism in this package, not a dedicated radial-gauge control.
- `LiveChartsCore.SkiaSharpView.Avalonia.PieChart`: `Series`, `MinValue`, `MaxValue`, `LegendPosition` (already used in `ResultsWindow.axaml`).

## File Structure

- Create: `dotnet/CspAnalyzer.Desktop.Tests/CspAnalyzer.Desktop.Tests.csproj`
- Create: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs`
- Create: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs`
- Create: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs`
- Modify: `dotnet/CspAnalyzer.sln` (register new test project)
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (ctor param, small edits to `LoadDatasetAsync`/`RunAsync`)
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Navigation.cs`
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.ManualOverride.cs`
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/IConfirmDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/NullConfirmDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/AvaloniaConfirmDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Views/ConfirmDialog.axaml` + `.axaml.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml` (replace remaining placeholders)
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs` (chart click-to-navigate wiring)
- Modify: `dotnet/CspAnalyzer.Desktop/App.axaml.cs` (wire `IConfirmDialogService`)
- Modify: `docs/superpowers/SESSIONS.md` (check off S10b)

---

### Task 1: Scaffold `CspAnalyzer.Desktop.Tests`

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop.Tests/CspAnalyzer.Desktop.Tests.csproj`
- Create: `dotnet/CspAnalyzer.Desktop.Tests/SanityTests.cs`
- Modify: `dotnet/CspAnalyzer.sln`

**Interfaces:**
- Produces: a buildable, runnable xunit project referencing `CspAnalyzer.Desktop.csproj` (transitively `BackendInterop`), proving `MainViewModel` (and everything it depends on) can be constructed and exercised with no live Avalonia `Window`.

- [ ] **Step 1: Create the test project**, same shape as `dotnet/BackendInterop.Tests/BackendInterop.Tests.csproj` but referencing `CspAnalyzer.Desktop`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CspAnalyzer.Desktop\CspAnalyzer.Desktop.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write a sanity test** that constructs `MainViewModel` via its parameterless constructor (the one the `Design.DataContext` uses) and checks it doesn't throw:

```csharp
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class SanityTests
{
    [Fact]
    public void MainViewModel_constructs_with_default_null_services()
    {
        var vm = new MainViewModel();

        Assert.False(vm.IsReferenceLoaded);
        Assert.Empty(vm.DatasetSpectra);
    }
}
```

- [ ] **Step 3: Register the project in the solution.** Add a new `Project(...)` block to `dotnet/CspAnalyzer.sln` (copy the GUID pattern from the existing `BackendInterop.Tests` entry — generate a fresh GUID, e.g. with `uuidgen` or any new GUID, don't reuse an existing one) and matching `ProjectConfigurationPlatforms` lines for `Debug|Any CPU` / `Release|Any CPU`, same as the other three projects.

- [ ] **Step 4: Run it.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~CspAnalyzer.Desktop.Tests
```

Expected: 1 passed (`SanityTests.MainViewModel_constructs_with_default_null_services`).

- [ ] **Step 5: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop.Tests dotnet/CspAnalyzer.sln
git commit -m "S10b: scaffold CspAnalyzer.Desktop.Tests project"
```

---

### Task 2: Sort `DatasetSpectra` by `ExpNumber` after load

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs:139-183` (`LoadDatasetAsync`)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs` (new file, first test in it)

**Interfaces:**
- Produces: `LoadDatasetAsync` populates `DatasetSpectra` in ascending `ExpNumber` order regardless of filesystem enumeration order.

- [ ] **Step 1: Write the failing test.**

```csharp
using System.IO;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelNavigationTests
{
    // A fake picker that returns a real temp folder laid out like a
    // Demo-dataset: subfolders whose names sort differently than their
    // EXP_NUMBER content, to actually exercise the sort (not just happen
    // to already be in order).
    private sealed class FixedFolderFilePickerService(string referenceXmlPath, string datasetFolder) : IFilePickerService
    {
        public Task<string?> PickXmlFileAsync(string title) => Task.FromResult<string?>(referenceXmlPath);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(datasetFolder);
        public Task<string?> PickSaveFileAsync(string suggestedFileName, string extension) => Task.FromResult<string?>(null);
    }

    private static string WritePeaklistXml(string dir, int expNumber)
    {
        string subfolder = Path.Combine(dir, "pdata", "1");
        Directory.CreateDirectory(subfolder);
        string path = Path.Combine(subfolder, "peaklist.xml");
        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <peaklist>
              <PeakList2D>
                <Peak2D F1="120.0" F2="8.0" intensity="9000" Number="1"/>
              </PeakList2D>
            </peaklist>
            """);
        return path;
    }

    [Fact]
    public async Task LoadDatasetAsync_sorts_experiments_by_ExpNumber_not_filesystem_order()
    {
        string root = Directory.CreateTempSubdirectory("csp_nav_test_").FullName;
        string refDir = Path.Combine(root, "ref");
        Directory.CreateDirectory(refDir);
        string refXml = WritePeaklistXml(refDir, 1);

        string dsRoot = Path.Combine(root, "ds");
        Directory.CreateDirectory(dsRoot);
        // Create subfolders in an order that would NOT already sort correctly by name.
        WritePeaklistXml(Path.Combine(dsRoot, "zz_exp_9"), 9);
        WritePeaklistXml(Path.Combine(dsRoot, "aa_exp_1"), 1);

        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService(), new NullConfirmDialogService());
        await vm.LoadReferenceCommand.ExecuteAsync(null);
        await vm.LoadDatasetCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.DatasetSpectra.Count);
        Assert.True(vm.DatasetSpectra[0].ExpNumber <= vm.DatasetSpectra[1].ExpNumber);
    }
}
```

Note: `PeaklistXmlParser`'s exact expected XML shape must match `dotnet/BackendInterop/PeaklistXmlParser.cs` — check that file if this test's fixture XML doesn't parse; the important part being tested here is the *sort*, not the parse.

- [ ] **Step 2: Run it to verify it fails** (or passes by luck of ordering — if so, this fixture isn't exercising the bug; two experiments named so `zz_exp_9` sorts after `aa_exp_1` alphabetically but has the *smaller* index requirement inverted should force a real failure pre-fix. Adjust folder names if needed so alphabetical order and `ExpNumber` order disagree).

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~LoadDatasetAsync_sorts_experiments
```

Expected: FAIL (`DatasetSpectra[0].ExpNumber` is 9, not <= `DatasetSpectra[1].ExpNumber`).

- [ ] **Step 3: Fix `LoadDatasetAsync`.** In `MainViewModel.cs`, after the `foreach (string dir in subfolders)` loop that populates `DatasetSpectra` (around line 166-176), sort before setting the status text:

```csharp
        PeaklistFilesFoundCount = found;

        List<PeaklistSpectrum> sorted = DatasetSpectra.OrderBy(s => s.ExpNumber).ToList();
        DatasetSpectra.Clear();
        foreach (PeaklistSpectrum spectrum in sorted)
        {
            DatasetSpectra.Add(spectrum);
        }

        DatasetStatusText = found > 0
            ? $"Dataset Loaded ({found} experiments)"
            : "No experiments were found in this folder.";
        RunCommand.NotifyCanExecuteChanged();
```

- [ ] **Step 4: Run it to verify it passes.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~LoadDatasetAsync_sorts_experiments
```

Expected: PASS.

- [ ] **Step 5: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs
git commit -m "S10b: sort DatasetSpectra by ExpNumber after load"
```

---

### Task 3: Navigation state — `ExperimentFilter`, `CurrentView`, `CurrentIndex`, derived display properties

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Navigation.cs`
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs` (append)

**Interfaces:**
- Consumes: `MainViewModel.DatasetSpectra` (`ObservableCollection<PeaklistSpectrum>`), `MainViewModel.RunResults` (`ObservableCollection<SpectrumResult>`), `MainViewModel.ReferenceSpectrum` (`PeaklistSpectrum?`) — all already exist in `MainViewModel.cs`.
- Produces: `ExperimentFilter` enum, `CurrentFilter` (`ExperimentFilter?`), `CurrentIndex` (`int`), `CurrentView` (`IReadOnlyList<PeaklistSpectrum>`), `CurrentSpectrum` (`PeaklistSpectrum?`), `CurrentExperimentNumber`/`CurrentCounterText`/`CurrentPeakDifference`/`CurrentManualStatusText`/`CurrentAutomaticStatusText` (display strings), `IsActivesFilterChecked`/`IsInactivesFilterChecked` (bool, two-way, back `CurrentFilter`), `RaiseNavigationChanged()` (public — later tasks/files call this after mutating `DatasetSpectra`/`RunResults`/spectrum state).

- [ ] **Step 1: Write the failing tests** (append to `MainViewModelNavigationTests.cs`):

```csharp
    private static PeaklistSpectrum MakeSpectrum(int expNumber) => new()
    {
        ExpNumber = expNumber,
        DsName = "ds",
        TotReadPeaks = 10 + expNumber,
        Peaklist = { new Peak { Number = 1, F1 = 120, F2 = 8, Intensity = 9000 } },
    };

    private static MainViewModel MakeViewModelWithDataset(int refTotReadPeaks, params int[] expNumbers)
    {
        var vm = new MainViewModel();
        vm.ReferenceSpectrum = new PeaklistSpectrum { ExpNumber = 1, DsName = "ref", TotReadPeaks = refTotReadPeaks };
        foreach (int exp in expNumbers)
        {
            vm.DatasetSpectra.Add(MakeSpectrum(exp));
        }
        vm.RaiseNavigationChanged();
        return vm;
    }

    [Fact]
    public void CurrentView_defaults_to_the_full_dataset_when_no_filter_is_set()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102, 103);

        Assert.Equal(3, vm.CurrentView.Count);
        Assert.Equal(101, vm.CurrentSpectrum!.ExpNumber);
        Assert.Equal("1 / 3", vm.CurrentCounterText);
    }

    [Fact]
    public void CurrentView_filters_to_actives_only_using_RunResults_IsActive()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102, 103);
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 101, IsActive = true });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 102, IsActive = false });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 103, IsActive = true });
        vm.RaiseNavigationChanged();

        vm.IsActivesFilterChecked = true;

        Assert.Equal(2, vm.CurrentView.Count);
        Assert.All(vm.CurrentView, s => Assert.True(s.ExpNumber is 101 or 103));
    }

    [Fact]
    public void Checking_Inactives_filter_unchecks_Actives_filter()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101);

        vm.IsActivesFilterChecked = true;
        vm.IsInactivesFilterChecked = true;

        Assert.False(vm.IsActivesFilterChecked);
        Assert.True(vm.IsInactivesFilterChecked);
    }

    [Fact]
    public void CurrentPeakDifference_is_TotReadPeaks_minus_reference()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101);

        Assert.Equal(vm.DatasetSpectra[0].TotReadPeaks - 80, vm.CurrentPeakDifference);
    }
```

Add `using CspAnalyzer.BackendInterop;` at the top of the test file if not already present.

- [ ] **Step 2: Run to verify failure** (compile error is expected here since `CurrentView` etc. don't exist yet):

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelNavigationTests
```

Expected: FAIL to build (`'MainViewModel' does not contain a definition for 'CurrentView'` etc.)

- [ ] **Step 3: Implement `MainViewModel.Navigation.cs`:**

```csharp
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10b: which subset of DatasetSpectra the player/overlay/gauges are
/// currently showing - replaces CSPv2/Form1.cs's two independently-mutable
/// ShowActives/ShowInactives bools (each checkbox handler manually
/// unchecked the other, leaving several "both true" branches in
/// update_graphs/update_player that could never actually be reached
/// through the UI) with a single nullable enum.
/// </summary>
public enum ExperimentFilter
{
    Actives,
    Inactives,
}

/// <summary>
/// S10b: player navigation (CurrentIndex/CurrentView/First-Previous-Next-
/// Last/GoToExperiment) and the actives/inactives filter. Port of
/// CSPv2/Form1.cs's update_player/go_to_experiment/CheckBoxActives_
/// CheckedChanged region. Bounds are enforced via RelayCommand CanExecute
/// instead of legacy's pattern of relying on update_player() to disable
/// buttons after the fact.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    private ExperimentFilter? _currentFilter;

    [ObservableProperty]
    private int _currentIndex;

    [ObservableProperty]
    private string _goToExperimentText = "";

    [ObservableProperty]
    private string _goToStatusText = "";

    public bool IsActivesFilterChecked
    {
        get => CurrentFilter == ExperimentFilter.Actives;
        set => CurrentFilter = value ? ExperimentFilter.Actives : null;
    }

    public bool IsInactivesFilterChecked
    {
        get => CurrentFilter == ExperimentFilter.Inactives;
        set => CurrentFilter = value ? ExperimentFilter.Inactives : null;
    }

    private Dictionary<int, SpectrumResult> ResultsByExpNumber => RunResults.ToDictionary(r => r.ExpNumber);

    public IReadOnlyList<PeaklistSpectrum> CurrentView => CurrentFilter switch
    {
        ExperimentFilter.Actives => DatasetSpectra.Where(IsAutoActive).ToList(),
        ExperimentFilter.Inactives => DatasetSpectra.Where(s => !IsAutoActive(s)).ToList(),
        _ => DatasetSpectra.ToList(),
    };

    private bool IsAutoActive(PeaklistSpectrum spectrum) =>
        ResultsByExpNumber.TryGetValue(spectrum.ExpNumber, out SpectrumResult? result) && result.IsActive;

    public PeaklistSpectrum? CurrentSpectrum =>
        CurrentIndex >= 0 && CurrentIndex < CurrentView.Count ? CurrentView[CurrentIndex] : null;

    public string CurrentExperimentNumber => CurrentSpectrum is null ? "-" : CurrentSpectrum.ExpNumber.ToString();

    public string CurrentCounterText => CurrentView.Count == 0 ? "- / -" : $"{CurrentIndex + 1} / {CurrentView.Count}";

    public int? CurrentPeakDifference =>
        CurrentSpectrum is null || ReferenceSpectrum is null ? null : CurrentSpectrum.TotReadPeaks - ReferenceSpectrum.TotReadPeaks;

    public string CurrentManualStatusText => CurrentSpectrum?.UserSelection ?? "-";

    public string CurrentAutomaticStatusText =>
        CurrentSpectrum is null ? "-" :
        ResultsByExpNumber.TryGetValue(CurrentSpectrum.ExpNumber, out SpectrumResult? result)
            ? (result.IsActive ? "ACTIVE" : "INACTIVE")
            : "Run analysis";

    partial void OnCurrentFilterChanged(ExperimentFilter? value)
    {
        OnPropertyChanged(nameof(IsActivesFilterChecked));
        OnPropertyChanged(nameof(IsInactivesFilterChecked));
        CurrentIndex = 0;
        RaiseNavigationChanged();
    }

    private bool CanGoPrevious() => CurrentIndex > 0;
    private bool CanGoNext() => CurrentIndex < CurrentView.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void First()
    {
        CurrentIndex = 0;
        RaiseNavigationChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        CurrentIndex--;
        RaiseNavigationChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        CurrentIndex++;
        RaiseNavigationChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Last()
    {
        CurrentIndex = CurrentView.Count - 1;
        RaiseNavigationChanged();
    }

    [RelayCommand]
    private void GoToExperiment()
    {
        if (!int.TryParse(GoToExperimentText, out int expNumber))
        {
            GoToStatusText = "Enter a valid experiment number.";
            return;
        }

        int index = CurrentView.ToList().FindIndex(s => s.ExpNumber == expNumber);
        if (index < 0)
        {
            GoToStatusText = $"Experiment {expNumber} not found.";
            return;
        }

        CurrentIndex = index;
        GoToStatusText = "";
        RaiseNavigationChanged();
    }

    /// <summary>
    /// Called by chart click-to-navigate (MainWindow.axaml.cs) and by
    /// LoadDatasetAsync/RunAsync/manual-override mutations - anything that
    /// changes DatasetSpectra, RunResults, or a spectrum's UserSelection
    /// must call this afterward so the computed display properties and
    /// nav-command CanExecute states refresh (they don't auto-cascade from
    /// ObservableProperty since CurrentView/CurrentSpectrum are plain
    /// computed properties, not backed by their own [ObservableProperty]).
    /// </summary>
    public void RaiseNavigationChanged()
    {
        OnPropertyChanged(nameof(CurrentView));
        OnPropertyChanged(nameof(CurrentSpectrum));
        OnPropertyChanged(nameof(CurrentExperimentNumber));
        OnPropertyChanged(nameof(CurrentCounterText));
        OnPropertyChanged(nameof(CurrentPeakDifference));
        OnPropertyChanged(nameof(CurrentManualStatusText));
        OnPropertyChanged(nameof(CurrentAutomaticStatusText));
        FirstCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        LastCommand.NotifyCanExecuteChanged();
        RebuildOverlayPoints();
    }

    public void NavigateToChartIndex(int index)
    {
        if (index < 0 || index >= CurrentView.Count)
        {
            return;
        }

        CurrentIndex = index;
        RaiseNavigationChanged();
    }
}
```

Note: `RebuildOverlayPoints()` is defined in Task 11's `MainViewModel.Charts.cs` — this file won't compile until that method exists. **Temporarily comment out that one call** (`// RebuildOverlayPoints();`) for this task, and uncomment it in Task 11 once the method exists. Leave a `// TODO(S10b Task 11): uncomment once RebuildOverlayPoints exists` marker so it isn't missed.

- [ ] **Step 4: Run to verify tests pass.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelNavigationTests
```

Expected: all PASS.

- [ ] **Step 5: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Navigation.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs
git commit -m "S10b: navigation state - ExperimentFilter, CurrentView, CurrentIndex"
```

---

### Task 4: Player navigation bounds tests (First/Previous/Next/Last/GoTo)

**Files:**
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs` (append)

**Interfaces:**
- Consumes: everything from Task 3.

- [ ] **Step 1: Write the tests.**

```csharp
    [Fact]
    public void First_and_Previous_are_disabled_at_index_zero()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102);

        Assert.False(vm.FirstCommand.CanExecute(null));
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.True(vm.NextCommand.CanExecute(null));
        Assert.True(vm.LastCommand.CanExecute(null));
    }

    [Fact]
    public void Next_and_Last_are_disabled_at_the_final_index()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102);

        vm.LastCommand.Execute(null);

        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.False(vm.LastCommand.CanExecute(null));
        Assert.True(vm.PreviousCommand.CanExecute(null));
        Assert.Equal(102, vm.CurrentSpectrum!.ExpNumber);
    }

    [Fact]
    public void All_nav_commands_are_disabled_with_a_single_experiment()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101);

        Assert.False(vm.FirstCommand.CanExecute(null));
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.False(vm.LastCommand.CanExecute(null));
    }

    [Fact]
    public void GoToExperiment_jumps_to_the_matching_experiment()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102, 103);

        vm.GoToExperimentText = "103";
        vm.GoToExperimentCommand.Execute(null);

        Assert.Equal(2, vm.CurrentIndex);
        Assert.Equal("", vm.GoToStatusText);
    }

    [Fact]
    public void GoToExperiment_reports_not_found_and_does_not_move()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102);

        vm.GoToExperimentText = "999";
        vm.GoToExperimentCommand.Execute(null);

        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal("Experiment 999 not found.", vm.GoToStatusText);
    }
```

- [ ] **Step 2: Run.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelNavigationTests
```

Expected: all PASS (this task validates Task 3's implementation more thoroughly; no production code changes expected — if any test fails, fix `MainViewModel.Navigation.cs`, not the test).

- [ ] **Step 3: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop.Tests/MainViewModelNavigationTests.cs
git commit -m "S10b: test player navigation bounds and go-to-experiment"
```

---

### Task 5: `IConfirmDialogService` infrastructure

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Services/IConfirmDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/NullConfirmDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/AvaloniaConfirmDialogService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Views/ConfirmDialog.axaml`
- Create: `dotnet/CspAnalyzer.Desktop/Views/ConfirmDialog.axaml.cs`

**Interfaces:**
- Produces: `IConfirmDialogService.ConfirmAsync(string title, string message) : Task<bool>`, used by Task 6's `ResetAllManualFlagsCommand`.

- [ ] **Step 1: `IConfirmDialogService.cs`:**

```csharp
using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Replaces WinForms' MessageBox.Show(..., YesNo) (CSPv2/Form1.cs's
/// buttonResetAllManualFlags_Click) - Avalonia has no built-in MessageBox.
/// Same reasoning as IFilePickerService/IResultsWindowService: keeps
/// MainViewModel usable with no live Window (design-time, tests).
/// </summary>
public interface IConfirmDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
}
```

- [ ] **Step 2: `NullConfirmDialogService.cs`:**

```csharp
using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>Always confirms - used by the design-time DataContext and by tests, where there's no real dialog to show.</summary>
public sealed class NullConfirmDialogService : IConfirmDialogService
{
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
}
```

- [ ] **Step 3: `ConfirmDialog.axaml`:**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="CspAnalyzer.Desktop.Views.ConfirmDialog"
        Title="Confirm"
        Width="420" SizeToContent="Height"
        CanResize="False"
        WindowStartupLocation="CenterOwner">
    <StackPanel Margin="16" Spacing="12">
        <TextBlock x:Name="MessageText" TextWrapping="Wrap" />
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8">
            <Button Content="Yes" Click="OnYesClicked" />
            <Button Content="No" Click="OnNoClicked" IsDefault="True" />
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 4: `ConfirmDialog.axaml.cs`:**

```csharp
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
```

- [ ] **Step 5: `AvaloniaConfirmDialogService.cs`:**

```csharp
using System.Threading.Tasks;
using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaConfirmDialogService(Window owner) : IConfirmDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message);
        return await dialog.ShowDialog<bool>(owner);
    }
}
```

- [ ] **Step 6: Build to confirm it all compiles** (no behavior to unit-test here - this is UI-shell infrastructure, exercised in Task 6's tests via `NullConfirmDialogService` and manually in Task 12):

```bash
cd dotnet && dotnet build CspAnalyzer.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop/Services/IConfirmDialogService.cs dotnet/CspAnalyzer.Desktop/Services/NullConfirmDialogService.cs dotnet/CspAnalyzer.Desktop/Services/AvaloniaConfirmDialogService.cs dotnet/CspAnalyzer.Desktop/Views/ConfirmDialog.axaml dotnet/CspAnalyzer.Desktop/Views/ConfirmDialog.axaml.cs
git commit -m "S10b: add IConfirmDialogService and a minimal Avalonia confirm dialog"
```

---

### Task 6: Manual-override commands + `MainViewModel` constructor wiring

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (ctor)
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.ManualOverride.cs`
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs` (new file)

**Interfaces:**
- Consumes: `IConfirmDialogService` (Task 5), `CurrentSpectrum`/`RaiseNavigationChanged()` (Task 3), `PeaklistSpectrum.UserSelection` (existing).
- Produces: `MarkActiveCommand`/`MarkInactiveCommand`/`ResetManualStatusCommand`/`ResetAllManualFlagsCommand`, `ActivesManualCount`/`InactivesManualCount`/`NotSetManualCount` (int), `ManualResultsSeries` (`ISeries[]`, consumed by Task 12's XAML).

- [ ] **Step 1: Write the failing tests** (new file):

```csharp
using System.Threading.Tasks;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelManualOverrideTests
{
    private static MainViewModel MakeViewModel(params int[] expNumbers)
    {
        var vm = new MainViewModel(new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService());
        vm.ReferenceSpectrum = new PeaklistSpectrum { ExpNumber = 1, DsName = "ref", TotReadPeaks = 80 };
        foreach (int exp in expNumbers)
        {
            vm.DatasetSpectra.Add(new PeaklistSpectrum { ExpNumber = exp, DsName = "ds", TotReadPeaks = 80 + exp });
        }
        vm.RaiseNavigationChanged();
        return vm;
    }

    [Fact]
    public void MarkActive_sets_the_current_spectrums_UserSelection_and_updates_counts()
    {
        MainViewModel vm = MakeViewModel(101, 102);

        vm.MarkActiveCommand.Execute(null);

        Assert.Equal("ACTIVE (MAN)", vm.CurrentSpectrum!.UserSelection);
        Assert.Equal(1, vm.ActivesManualCount);
        Assert.Equal(0, vm.InactivesManualCount);
        Assert.Equal(1, vm.NotSetManualCount);
    }

    [Fact]
    public void MarkInactive_then_ResetManualStatus_returns_to_Not_set()
    {
        MainViewModel vm = MakeViewModel(101);

        vm.MarkInactiveCommand.Execute(null);
        Assert.Equal("INACTIVE (MAN)", vm.CurrentSpectrum!.UserSelection);

        vm.ResetManualStatusCommand.Execute(null);

        Assert.Equal("Not set", vm.CurrentSpectrum!.UserSelection);
        Assert.Equal(0, vm.ActivesManualCount);
        Assert.Equal(0, vm.InactivesManualCount);
        Assert.Equal(1, vm.NotSetManualCount);
    }

    [Fact]
    public async Task ResetAllManualFlags_resets_every_spectrum_when_confirmed()
    {
        MainViewModel vm = MakeViewModel(101, 102, 103);
        vm.MarkActiveCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.MarkInactiveCommand.Execute(null);

        await vm.ResetAllManualFlagsCommand.ExecuteAsync(null);

        Assert.All(vm.DatasetSpectra, s => Assert.Equal("Not set", s.UserSelection));
        Assert.Equal(0, vm.ActivesManualCount);
        Assert.Equal(0, vm.InactivesManualCount);
        Assert.Equal(3, vm.NotSetManualCount);
    }
}
```

- [ ] **Step 2: Run to verify failure** (compile error - members don't exist yet):

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelManualOverrideTests
```

Expected: FAIL to build.

- [ ] **Step 3: Add `_confirmDialogService` to the main constructor.** In `MainViewModel.cs`, update:

```csharp
    private readonly IFilePickerService _filePicker;
    private readonly IResultsWindowService _resultsWindowService;
    private readonly IConfirmDialogService _confirmDialogService;
```

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

(Remove the old two-parameter constructor entirely - it's replaced, not overloaded, so every call site must pass all three.)

- [ ] **Step 4: Implement `MainViewModel.ManualOverride.cs`:**

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10b: port of CSPv2/Form1.cs's "Buttons Manual UserSelection" region.
/// PeaklistSpectrum.UserSelection already exists and already flows into
/// ResultsBuilder -> ResultsWindow's Manual Flag column/pie (S10) - this
/// file is what actually mutates it, which nothing did before S10b.
/// </summary>
public partial class MainViewModel
{
    // CSPv2/Form1.cs:100-119's exact ARGB values, reordered to SkiaSharp's
    // (r,g,b,a) constructor - same values ResultsViewModel.cs already uses
    // for the ResultsWindow pie charts, kept consistent here.
    private static readonly SKColor ActiveManualColor = new(123, 217, 157, 200);
    private static readonly SKColor InactiveManualColor = new(199, 137, 137, 180);
    private static readonly SKColor NotSetManualColor = new(178, 178, 178, 180);

    [ObservableProperty]
    private int _activesManualCount;

    [ObservableProperty]
    private int _inactivesManualCount;

    [ObservableProperty]
    private int _notSetManualCount;

    [ObservableProperty]
    private ISeries[] _manualResultsSeries = Array.Empty<ISeries>();

    [RelayCommand]
    private void MarkActive() => SetCurrentUserSelection("ACTIVE (MAN)");

    [RelayCommand]
    private void MarkInactive() => SetCurrentUserSelection("INACTIVE (MAN)");

    [RelayCommand]
    private void ResetManualStatus() => SetCurrentUserSelection("Not set");

    private void SetCurrentUserSelection(string value)
    {
        if (CurrentSpectrum is null)
        {
            return;
        }

        CurrentSpectrum.UserSelection = value;
        RebuildManualResults();
        RaiseNavigationChanged();
    }

    [RelayCommand]
    private async Task ResetAllManualFlags()
    {
        bool confirmed = await _confirmDialogService.ConfirmAsync(
            "Manual Flag Reset",
            "Are you sure you want to reset your manual selection?" + Environment.NewLine + Environment.NewLine +
            "WARNING: All the Spectra Manual Flags will be reset to \"Not set\".");

        if (!confirmed)
        {
            return;
        }

        foreach (PeaklistSpectrum spectrum in DatasetSpectra)
        {
            spectrum.UserSelection = "Not set";
        }

        RebuildManualResults();
        RaiseNavigationChanged();
    }

    public void RebuildManualResults()
    {
        ActivesManualCount = DatasetSpectra.Count(s => s.UserSelection == "ACTIVE (MAN)");
        InactivesManualCount = DatasetSpectra.Count(s => s.UserSelection == "INACTIVE (MAN)");
        NotSetManualCount = DatasetSpectra.Count(s => s.UserSelection == "Not set");

        ManualResultsSeries = new ISeries[]
        {
            new ColumnSeries<int> { Name = "Act. (man)", Values = new[] { ActivesManualCount }, Fill = new SolidColorPaint(ActiveManualColor) },
            new ColumnSeries<int> { Name = "Inact. (man)", Values = new[] { InactivesManualCount }, Fill = new SolidColorPaint(InactiveManualColor) },
            new ColumnSeries<int> { Name = "Not set (man)", Values = new[] { NotSetManualCount }, Fill = new SolidColorPaint(NotSetManualColor) },
        };
    }
}
```

- [ ] **Step 5: Update every other `MainViewModel(...)` call site** to pass a third argument:
  - `dotnet/CspAnalyzer.Desktop/App.axaml.cs`: `new MainViewModel(new AvaloniaFilePickerService(window), new AvaloniaResultsWindowService(window), new AvaloniaConfirmDialogService(window))`.
  - Any earlier test in `MainViewModelNavigationTests.cs` that calls the two-arg constructor - there shouldn't be any (Task 3/4's `MakeViewModelWithDataset` uses the parameterless ctor), but grep to be sure: `grep -rn "new MainViewModel(" dotnet/`.

- [ ] **Step 6: Run to verify tests pass.**

```bash
cd dotnet && dotnet build CspAnalyzer.sln && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelManualOverrideTests
```

Expected: build succeeds, all tests PASS.

- [ ] **Step 7: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.ManualOverride.cs dotnet/CspAnalyzer.Desktop/App.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelManualOverrideTests.cs
git commit -m "S10b: manual-override commands (mark active/inactive/reset/reset-all)"
```

---

### Task 7: Peak-difference bar chart

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (`LoadDatasetAsync`, call the builder)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs` (new file)

**Interfaces:**
- Consumes: `DatasetSpectra`, `ReferenceSpectrum`, `CurrentIndex` (Task 3).
- Produces: `PeakDiffSeries`/`PeakDiffXAxes`/`PeakDiffYAxes`/`PeakDiffSections`/`PeakDiffAnnotations`, `BuildPeakDiffChart()` (public — called from `LoadDatasetAsync` and from tests directly).

- [ ] **Step 1: Write the failing test** (new file):

```csharp
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelChartTests
{
    private static MainViewModel MakeViewModel(int refTotReadPeaks, params int[] datasetTotReadPeaks)
    {
        var vm = new MainViewModel();
        vm.ReferenceSpectrum = new PeaklistSpectrum { ExpNumber = 1, DsName = "ref", TotReadPeaks = refTotReadPeaks };
        for (int i = 0; i < datasetTotReadPeaks.Length; i++)
        {
            vm.DatasetSpectra.Add(new PeaklistSpectrum { ExpNumber = 100 + i, DsName = "ds", TotReadPeaks = datasetTotReadPeaks[i] });
        }
        return vm;
    }

    [Fact]
    public void BuildPeakDiffChart_produces_one_bar_per_experiment_valued_at_TotReadPeaks_minus_reference()
    {
        MainViewModel vm = MakeViewModel(80, 85, 40, 80);

        vm.BuildPeakDiffChart();

        var series = Assert.Single(vm.PeakDiffSeries);
        var column = Assert.IsType<LiveChartsCore.SkiaSharpView.ColumnSeries<int>>(series);
        Assert.Equal(new[] { 5, -40, 0 }, column.Values);
    }

    [Fact]
    public void BuildPeakDiffChart_sets_five_threshold_zone_sections()
    {
        MainViewModel vm = MakeViewModel(80, 85);

        vm.BuildPeakDiffChart();

        Assert.Equal(5, vm.PeakDiffSections.Length);
    }
}
```

- [ ] **Step 2: Run to verify failure.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelChartTests
```

Expected: FAIL to build.

- [ ] **Step 3: Implement `MainViewModel.Charts.cs`** (this file grows across Tasks 7-11; this step adds only the peak-diff piece):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10b: the three charts embedded in Form1 itself (peak-diff bar,
/// probability bar, spectra-overlay scatter) plus the actives/inactives
/// gauges - separate from S10's ResultsWindow charts, which are a
/// different window built from the same run's data. Colors are
/// CSPv2/Form1.cs:100-119's exact ARGB values reordered to SkiaSharp's
/// (r,g,b,a) constructor.
/// </summary>
public partial class MainViewModel
{
    private static readonly SKColor BrokenSpectrumColor = new(254, 132, 132, 5);
    private static readonly SKColor FineSpectrumColor = new(45, 161, 63, 5);
    private static readonly SKColor CheckSpectrumColor = new(204, 204, 204, 25);
    private static readonly SKColor AllSpectraFillColor = new(250, 163, 0, 180);
    private static readonly SKColor CurrentMarkerTextColor = new(255, 255, 255, 200);
    private static readonly SKColor ActiveAutoColor = new(45, 161, 63, 200);
    private static readonly SKColor InactiveAutoColor = new(225, 9, 20, 180);

    [ObservableProperty]
    private ISeries[] _peakDiffSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _peakDiffXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _peakDiffYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private RectangularSection[] _peakDiffSections = Array.Empty<RectangularSection>();

    [ObservableProperty]
    private LabelVisual[] _peakDiffAnnotations = Array.Empty<LabelVisual>();

    public void BuildPeakDiffChart()
    {
        if (ReferenceSpectrum is null)
        {
            return;
        }

        int[] diffs = DatasetSpectra.Select(s => s.TotReadPeaks - ReferenceSpectrum.TotReadPeaks).ToArray();

        PeakDiffXAxes = new[]
        {
            new Axis
            {
                Name = "Experiment No.",
                LabelsRotation = 30,
                MinLimit = 0,
                MaxLimit = DatasetSpectra.Count,
                Labels = DatasetSpectra.Select(s => s.ExpNumber.ToString()).ToList(),
            },
        };
        PeakDiffYAxes = new[]
        {
            new Axis { Name = "ΔPeaks", MinLimit = -80, MaxLimit = 80 },
        };
        PeakDiffSeries = new ISeries[]
        {
            new ColumnSeries<int> { Name = "ΔPeaks", Values = diffs, Fill = new SolidColorPaint(AllSpectraFillColor) },
        };
        PeakDiffSections = BuildThresholdZoneSections();
        RebuildPeakDiffAnnotations(diffs);
    }

    // Port of CSPv2/Form1.cs:298-334's AxisSection zones: Broken [-80,-40]
    // and [40,80], Check [-45,-25] and [25,45], Safe/Fine [-30,30].
    private static RectangularSection[] BuildThresholdZoneSections() => new[]
    {
        new RectangularSection { Yi = -80, Yj = -40, Fill = new SolidColorPaint(BrokenSpectrumColor) },
        new RectangularSection { Yi = 25, Yj = 45, Fill = new SolidColorPaint(CheckSpectrumColor) },
        new RectangularSection { Yi = -45, Yj = -25, Fill = new SolidColorPaint(CheckSpectrumColor) },
        new RectangularSection { Yi = 40, Yj = 80, Fill = new SolidColorPaint(BrokenSpectrumColor) },
        new RectangularSection { Yi = -30, Yj = 30, Fill = new SolidColorPaint(FineSpectrumColor) },
    };

    private void RebuildPeakDiffAnnotations(int[] diffs)
    {
        double center = diffs.Length / 2.0;
        var annotations = new List<LabelVisual>
        {
            Label(center, 15, "Safe range"),
            Label(center, -15, "Safe range"),
            Label(center, 35, "Check PP"),
            Label(center, -35, "Check PP"),
            Label(center, 65, "Broken Spectrum"),
            Label(center, -65, "Broken Spectrum"),
        };

        if (CurrentIndex >= 0 && CurrentIndex < diffs.Length)
        {
            annotations.Add(Label(CurrentIndex, diffs[CurrentIndex], "Current Spectrum"));
        }

        PeakDiffAnnotations = annotations.ToArray();
    }

    private static LabelVisual Label(double x, double y, string text) => new()
    {
        X = x,
        Y = y,
        Text = text,
        TextSize = 10,
        Paint = new SolidColorPaint(CurrentMarkerTextColor),
    };
}
```

- [ ] **Step 4: Wire it into `LoadDatasetAsync`.** In `MainViewModel.cs`, at the end of the `if (found > 0)` block (right after the sort added in Task 2, before/after the status-text assignment - order doesn't matter, but keep it near the other post-load setup):

```csharp
        BuildPeakDiffChart();
        RaiseNavigationChanged();
```

- [ ] **Step 5: Run to verify tests pass.**

```bash
cd dotnet && dotnet build CspAnalyzer.sln && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelChartTests
```

Expected: build succeeds, both tests PASS.

- [ ] **Step 6: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs
git commit -m "S10b: peak-difference bar chart"
```

---

### Task 8: Probability bar chart + zoom-sync with the peak-diff chart

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (`RunAsync`)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs` (append)

**Interfaces:**
- Consumes: `RunResults` (existing), `ResultsByExpNumber` (Task 3), `PeakDiffXAxes` (Task 7).
- Produces: `ProbabilitySeries`/`ProbabilityXAxes`/`ProbabilityYAxes`/`ProbabilitySections`/`ProbabilityAnnotations`, `BuildProbabilityChart()` (public).

- [ ] **Step 1: Write the failing tests** (append to `MainViewModelChartTests.cs`):

```csharp
    [Fact]
    public void BuildProbabilityChart_produces_one_bar_per_experiment_from_RunResults()
    {
        MainViewModel vm = MakeViewModel(80, 85, 40);
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 100, IsActive = true, ActivePseudoprobability = 0.91 });
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 101, IsActive = false, ActivePseudoprobability = 0.1 });

        vm.BuildProbabilityChart();

        var series = Assert.Single(vm.ProbabilitySeries);
        var column = Assert.IsType<LiveChartsCore.SkiaSharpView.ColumnSeries<double>>(series);
        Assert.Equal(new[] { 0.91, 0.1 }, column.Values);
    }

    [Fact]
    public void BuildProbabilityChart_shares_its_X_axis_with_the_peak_diff_chart_for_zoom_sync()
    {
        MainViewModel vm = MakeViewModel(80, 85);
        vm.BuildPeakDiffChart();

        vm.BuildProbabilityChart();

        Assert.Contains(vm.PeakDiffXAxes[0], vm.ProbabilityXAxes[0].SharedWith);
        Assert.Contains(vm.ProbabilityXAxes[0], vm.PeakDiffXAxes[0].SharedWith);
    }
```

- [ ] **Step 2: Run to verify failure.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelChartTests
```

Expected: FAIL to build (`BuildProbabilityChart`/`ProbabilitySeries` etc. don't exist).

- [ ] **Step 3: Add to `MainViewModel.Charts.cs`:**

```csharp
    [ObservableProperty]
    private ISeries[] _probabilitySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _probabilityXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _probabilityYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private RectangularSection[] _probabilitySections = Array.Empty<RectangularSection>();

    [ObservableProperty]
    private LabelVisual[] _probabilityAnnotations = Array.Empty<LabelVisual>();

    public void BuildProbabilityChart()
    {
        double[] probs = DatasetSpectra
            .Select(s => ResultsByExpNumber.TryGetValue(s.ExpNumber, out var r) ? r.ActivePseudoprobability : 0.0)
            .ToArray();

        var xAxis = new Axis
        {
            Name = "Experiment No.",
            LabelsRotation = 30,
            MinLimit = 0,
            MaxLimit = DatasetSpectra.Count,
            Labels = DatasetSpectra.Select(s => s.ExpNumber.ToString()).ToList(),
        };

        // Port of CSPv2/Form1.cs's Axis_RangeChanged zoom-sync hack - the
        // modern LiveChartsCore way is just sharing axes with each other.
        if (PeakDiffXAxes.Length > 0)
        {
            xAxis.SharedWith = new[] { PeakDiffXAxes[0] };
            PeakDiffXAxes[0].SharedWith = new[] { xAxis };
        }

        ProbabilityXAxes = new[] { xAxis };
        ProbabilityYAxes = new[] { new Axis { Name = "Probability", MinLimit = 0, MaxLimit = 1 } };
        ProbabilitySeries = new ISeries[]
        {
            new ColumnSeries<double> { Name = "Probability", Values = probs, Fill = new SolidColorPaint(InactiveAutoColor) },
        };
        ProbabilitySections = new[]
        {
            new RectangularSection { Yi = 0, Yj = 0.35, Fill = new SolidColorPaint(BrokenSpectrumColor) },
            new RectangularSection { Yi = 0.35, Yj = 0.75, Fill = new SolidColorPaint(CheckSpectrumColor) },
            new RectangularSection { Yi = 0.75, Yj = 1, Fill = new SolidColorPaint(FineSpectrumColor) },
        };

        if (CurrentIndex >= 0 && CurrentIndex < probs.Length)
        {
            ProbabilityAnnotations = new[] { Label(CurrentIndex, probs[CurrentIndex], "Current Spectrum") };
        }
    }
```

- [ ] **Step 4: Wire it into `RunAsync`.** In `MainViewModel.cs`'s `RunAsync`, right after the `foreach (SpectrumResult r in parsed) { RunResults.Add(r); }` loop and before `OpenResultsWindowCommand.NotifyCanExecuteChanged();`:

```csharp
                CurrentIndex = 0;
                BuildProbabilityChart();
                RaiseNavigationChanged();
```

- [ ] **Step 5: Run to verify tests pass.**

```bash
cd dotnet && dotnet build CspAnalyzer.sln && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelChartTests
```

Expected: build succeeds, all PASS.

- [ ] **Step 6: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs
git commit -m "S10b: probability bar chart, zoom-synced with the peak-diff chart"
```

---

### Task 9: Actives/Inactives gauges

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (`RunAsync`)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs` (append)

**Interfaces:**
- Produces: `ActivesGaugeSeries`/`InactivesGaugeSeries` (`ISeries[]`), `BuildGauges()` (public).

- [ ] **Step 1: Write the failing test.**

```csharp
    [Fact]
    public void BuildGauges_produces_a_solid_gauge_series_for_actives_and_inactives()
    {
        MainViewModel vm = MakeViewModel(80, 85, 40, 90);
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 100, IsActive = true });
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 101, IsActive = false });
        vm.RunResults.Add(new CspAnalyzer.BackendInterop.SpectrumResult { ExpNumber = 102, IsActive = true });

        vm.BuildGauges();

        Assert.NotEmpty(vm.ActivesGaugeSeries);
        Assert.NotEmpty(vm.InactivesGaugeSeries);
    }
```

- [ ] **Step 2: Run to verify failure.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelChartTests
```

Expected: FAIL to build.

- [ ] **Step 3: Add to `MainViewModel.Charts.cs`** (add `using LiveChartsCore.SkiaSharpView.Extensions;` and `using LiveChartsCore.Defaults;` to the top of the file):

```csharp
    [ObservableProperty]
    private ISeries[] _activesGaugeSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _inactivesGaugeSeries = Array.Empty<ISeries>();

    public void BuildGauges()
    {
        int actives = RunResults.Count(r => r.IsActive);
        int inactives = RunResults.Count - actives;

        ActivesGaugeSeries = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(actives, series =>
            {
                series.Name = "Actives";
                series.Fill = new SolidColorPaint(ActiveAutoColor);
            }));

        InactivesGaugeSeries = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(inactives, series =>
            {
                series.Name = "Inactives";
                series.Fill = new SolidColorPaint(InactiveAutoColor);
            }));
    }
```

- [ ] **Step 4: Wire it into `RunAsync`**, right next to the `BuildProbabilityChart();` call added in Task 8:

```csharp
                BuildGauges();
```

- [ ] **Step 5: Run to verify tests pass.**

```bash
cd dotnet && dotnet build CspAnalyzer.sln && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelChartTests
```

Expected: build succeeds, all PASS.

- [ ] **Step 6: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs
git commit -m "S10b: actives/inactives solid gauges"
```

---

### Task 10: Spectra-overlay scatter chart + zoom controls

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Navigation.cs` (uncomment `RebuildOverlayPoints()` call, see Task 3's note)
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (`LoadReferenceAsync`, `LoadDatasetAsync`)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs` (append)

**Interfaces:**
- Consumes: `NMin`/`NMax`/`HMin`/`HMax` (existing), `CurrentSpectrum`/`CurrentFilter` (Task 3).
- Produces: `OverlaySeries`/`OverlayXAxes`/`OverlayYAxes`, `BuildOverlayAxes()`, `RebuildOverlayPoints()` (both public), `ResetOverlayZoomCommand`/`FitOverlayZoomToReferenceCommand`.

- [ ] **Step 1: Write the failing tests.**

```csharp
    [Fact]
    public void BuildOverlayAxes_ranges_match_the_inverted_import_bounds()
    {
        var vm = new MainViewModel();
        vm.NMin = 100; vm.NMax = 140; vm.HMin = 5; vm.HMax = 12;

        vm.BuildOverlayAxes();

        Assert.Equal(-12, vm.OverlayXAxes[0].MinLimit);
        Assert.Equal(-5, vm.OverlayXAxes[0].MaxLimit);
        Assert.Equal(-140, vm.OverlayYAxes[0].MinLimit);
        Assert.Equal(-100, vm.OverlayYAxes[0].MaxLimit);
    }

    [Fact]
    public void RebuildOverlayPoints_plots_the_current_spectrums_peaks_inverted()
    {
        var vm = new MainViewModel();
        vm.BuildOverlayAxes();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ref", TotReadPeaks = 1 };
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 101,
            DsName = "ds",
            Peaklist = { new CspAnalyzer.BackendInterop.Peak { Number = 1, F1 = 120.0, F2 = 8.0, Intensity = 9000 } },
        });
        vm.RaiseNavigationChanged();

        var current = (LiveChartsCore.SkiaSharpView.ScatterSeries<LiveChartsCore.Defaults.WeightedPoint>)vm.OverlaySeries[1];
        var point = Assert.Single(current.Values);
        Assert.Equal(-8.0, point.X);
        Assert.Equal(-120.0, point.Y);
        Assert.Equal(9000.0, point.Weight);
    }
```

- [ ] **Step 2: Run to verify failure.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelChartTests
```

Expected: FAIL to build.

- [ ] **Step 3: Add to `MainViewModel.Charts.cs`** (add `using CommunityToolkit.Mvvm.Input;` and `using CspAnalyzer.BackendInterop;` to the top if not already present):

```csharp
    [ObservableProperty]
    private ISeries[] _overlaySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _overlayXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _overlayYAxes = Array.Empty<Axis>();

    private readonly ScatterSeries<WeightedPoint> _referenceOverlaySeries = new()
    {
        Name = "Reference",
        Fill = new SolidColorPaint(new SKColor(64, 79, 86, 220)),
    };

    private readonly ScatterSeries<WeightedPoint> _currentOverlaySeries = new()
    {
        Name = "Current Experiment",
        Fill = new SolidColorPaint(AllSpectraFillColor),
    };

    private readonly ScatterSeries<WeightedPoint> _activesOverlaySeries = new()
    {
        Name = "Actives",
        Fill = new SolidColorPaint(ActiveAutoColor),
    };

    private readonly ScatterSeries<WeightedPoint> _inactivesOverlaySeries = new()
    {
        Name = "Inactives",
        Fill = new SolidColorPaint(InactiveAutoColor),
    };

    public void BuildOverlayAxes()
    {
        OverlayXAxes = new[] { new Axis { Name = "1H ppm", MinLimit = -HMax, MaxLimit = -HMin } };
        OverlayYAxes = new[] { new Axis { Name = "15N ppm", MinLimit = -NMax, MaxLimit = -NMin } };
        OverlaySeries = new ISeries[] { _referenceOverlaySeries, _currentOverlaySeries, _activesOverlaySeries, _inactivesOverlaySeries };
    }

    public void RebuildOverlayPoints()
    {
        _referenceOverlaySeries.Values = ToOverlayPoints(ReferenceSpectrum);
        _currentOverlaySeries.Values = ToOverlayPoints(CurrentSpectrum);
        _activesOverlaySeries.Values = CurrentFilter == ExperimentFilter.Actives ? ToOverlayPoints(CurrentSpectrum) : Array.Empty<WeightedPoint>();
        _inactivesOverlaySeries.Values = CurrentFilter == ExperimentFilter.Inactives ? ToOverlayPoints(CurrentSpectrum) : Array.Empty<WeightedPoint>();
    }

    private static WeightedPoint[] ToOverlayPoints(PeaklistSpectrum? spectrum) =>
        spectrum is null
            ? Array.Empty<WeightedPoint>()
            : spectrum.Peaklist.Select(p => new WeightedPoint(-p.F2, -p.F1, p.Intensity)).ToArray();

    [RelayCommand]
    private void ResetOverlayZoom()
    {
        if (OverlayXAxes.Length == 0)
        {
            return;
        }

        OverlayXAxes[0].MinLimit = -HMax;
        OverlayXAxes[0].MaxLimit = -HMin;
        OverlayYAxes[0].MinLimit = -NMax;
        OverlayYAxes[0].MaxLimit = -NMin;
    }

    [RelayCommand]
    private void FitOverlayZoomToReference()
    {
        if (ReferenceSpectrum is null || ReferenceSpectrum.Peaklist.Count == 0 || OverlayXAxes.Length == 0)
        {
            return;
        }

        OverlayXAxes[0].MinLimit = -(ReferenceSpectrum.Peaklist.Max(p => p.F2) + 0.5);
        OverlayXAxes[0].MaxLimit = -(ReferenceSpectrum.Peaklist.Min(p => p.F2) - 0.5);
        OverlayYAxes[0].MinLimit = -(ReferenceSpectrum.Peaklist.Max(p => p.F1) + 3);
        OverlayYAxes[0].MaxLimit = -(ReferenceSpectrum.Peaklist.Min(p => p.F1) - 3);
    }
```

- [ ] **Step 4: Uncomment the `RebuildOverlayPoints();` call** in `MainViewModel.Navigation.cs`'s `RaiseNavigationChanged()` (added as a placeholder comment in Task 3).

- [ ] **Step 5: Call `BuildOverlayAxes()` once the reference loads and once the dataset loads** (its bounds depend on `NMin`/`NMax`/`HMin`/`HMax`, already available at reference-load time, but rebuilding again after dataset load is harmless and keeps it simple). In `MainViewModel.cs`'s `LoadReferenceAsync`, right before `RunCommand.NotifyCanExecuteChanged();`:

```csharp
        BuildOverlayAxes();
```

- [ ] **Step 6: Run to verify tests pass.**

```bash
cd dotnet && dotnet build CspAnalyzer.sln && dotnet test CspAnalyzer.sln --filter FullyQualifiedName~MainViewModelChartTests
```

Expected: build succeeds, all PASS.

- [ ] **Step 7: Run the full test suite** to make sure nothing earlier regressed now that `RaiseNavigationChanged()` does more work:

```bash
cd dotnet && dotnet test CspAnalyzer.sln
```

Expected: all projects' tests PASS.

- [ ] **Step 8: Commit.**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Charts.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Navigation.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelChartTests.cs
git commit -m "S10b: spectra-overlay scatter chart and zoom controls"
```

---

### Task 11: Chart click-to-navigate (code-behind wiring)

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `MainViewModel.NavigateToChartIndex(int)` (Task 3).
- Produces: click handler wired to two named `CartesianChart` controls that Task 12 adds to the XAML (`x:Name="PeakDiffChart"` / `x:Name="ProbabilityChart"`).

This task's code references XAML element names that don't exist until Task 12 - do Task 12's XAML changes first, or write both together. Listed separately here because they're conceptually different changes (view-model-facing event wiring vs. layout).

- [ ] **Step 1: Update `MainWindow.axaml.cs`:**

```csharp
using Avalonia.Controls;
using CspAnalyzer.Desktop.ViewModels;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView.Avalonia;

namespace CspAnalyzer.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        this.FindControl<CartesianChart>("PeakDiffChart")!.ChartPointPointerDown += OnChartPointClicked;
        this.FindControl<CartesianChart>("ProbabilityChart")!.ChartPointPointerDown += OnChartPointClicked;
    }

    private void OnChartPointClicked(IChartView chart, ChartPoint point)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.NavigateToChartIndex(point.Index);
        }
    }
}
```

- [ ] **Step 2: Build after Task 12 adds the named charts to XAML** (this file alone won't compile standalone since `FindControl<CartesianChart>("PeakDiffChart")` needs that name to exist - verify together with Task 12's Step covering the build):

```bash
cd dotnet && dotnet build CspAnalyzer.sln
```

Expected: Build succeeded (once Task 12 is also done).

- [ ] **Step 3: Commit together with Task 12** (see Task 12's commit step - these two are one logical unit of "wire the charts into the window").

---

### Task 12: Wire everything into `MainWindow.axaml`

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml`

**Interfaces:**
- Consumes: every public property/command added in Tasks 3, 6, 7, 8, 9, 10.

- [ ] **Step 1: Add the LiveChartsCore Avalonia namespace** to the `<Window>` root tag (it already has `xmlns:vm`, add alongside):

```xml
        xmlns:lvc="using:LiveChartsCore.SkiaSharpView.Avalonia"
```

- [ ] **Step 2: Replace the chart-zone placeholders** (currently `MainWindow.axaml:106-129`):

```xml
            <Grid Grid.Row="0" ColumnDefinitions="*,*">
                <Grid Grid.Column="0" RowDefinitions="*,*">
                    <Border Grid.Row="0" Classes="section">
                        <DockPanel>
                            <TextBlock DockPanel.Dock="Top" Classes="sectionHeader" Text="Peaks Difference Distribution" />
                            <lvc:CartesianChart x:Name="PeakDiffChart"
                                                 Series="{Binding PeakDiffSeries}"
                                                 XAxes="{Binding PeakDiffXAxes}"
                                                 YAxes="{Binding PeakDiffYAxes}"
                                                 Sections="{Binding PeakDiffSections}"
                                                 VisualElements="{Binding PeakDiffAnnotations}"
                                                 ZoomMode="X" />
                        </DockPanel>
                    </Border>
                    <Border Grid.Row="1" Classes="section">
                        <DockPanel>
                            <TextBlock DockPanel.Dock="Top" Classes="sectionHeader" Text="Probability Distribution" />
                            <lvc:CartesianChart x:Name="ProbabilityChart"
                                                 Series="{Binding ProbabilitySeries}"
                                                 XAxes="{Binding ProbabilityXAxes}"
                                                 YAxes="{Binding ProbabilityYAxes}"
                                                 Sections="{Binding ProbabilitySections}"
                                                 VisualElements="{Binding ProbabilityAnnotations}"
                                                 ZoomMode="X" />
                        </DockPanel>
                    </Border>
                </Grid>

                <Border Grid.Column="1" Classes="section">
                    <DockPanel>
                        <TextBlock DockPanel.Dock="Top" Classes="sectionHeader" Text="Spectra Overlay" />
                        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Center" Spacing="4" Margin="0,4,0,0">
                            <Button Content="Reset Zoom" Command="{Binding ResetOverlayZoomCommand}" />
                            <Button Content="Fit to Reference" Command="{Binding FitOverlayZoomToReferenceCommand}" />
                        </StackPanel>
                        <lvc:CartesianChart Series="{Binding OverlaySeries}" XAxes="{Binding OverlayXAxes}" YAxes="{Binding OverlayYAxes}" ZoomMode="XY" />
                    </DockPanel>
                </Border>
            </Grid>
```

- [ ] **Step 3: Replace the "Automated Analysis Results" placeholder** (currently `MainWindow.axaml:146-155`):

```xml
                    <Border Grid.Column="1" Classes="section">
                        <DockPanel>
                            <TextBlock DockPanel.Dock="Top" Classes="sectionHeader" Text="Automated Analysis Results" />
                            <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Center" Spacing="8">
                                <CheckBox Content="Actives" IsChecked="{Binding IsActivesFilterChecked}" />
                                <CheckBox Content="Inactives" IsChecked="{Binding IsInactivesFilterChecked}" />
                            </StackPanel>
                            <Grid ColumnDefinitions="*,*">
                                <lvc:PieChart Grid.Column="0" Series="{Binding ActivesGaugeSeries}" MaxValue="{Binding DatasetSpectra.Count}" />
                                <lvc:PieChart Grid.Column="1" Series="{Binding InactivesGaugeSeries}" MaxValue="{Binding DatasetSpectra.Count}" />
                            </Grid>
                        </DockPanel>
                    </Border>
```

- [ ] **Step 4: Replace the "Manual Analysis Results" placeholder** (currently `MainWindow.axaml:156-161`):

```xml
                    <Border Grid.Column="2" Classes="section">
                        <DockPanel>
                            <TextBlock DockPanel.Dock="Top" Classes="sectionHeader" Text="Manual Analysis Results" />
                            <Button DockPanel.Dock="Bottom" Content="Reset All Man. Flags" Command="{Binding ResetAllManualFlagsCommand}" HorizontalAlignment="Stretch" Margin="0,4,0,0" />
                            <lvc:CartesianChart Series="{Binding ManualResultsSeries}" />
                        </DockPanel>
                    </Border>
```

- [ ] **Step 5: Replace the player-nav column** (currently `MainWindow.axaml:164-190`):

```xml
                <Grid Grid.Column="1" RowDefinitions="Auto,*">
                    <Grid Grid.Row="0" ColumnDefinitions="*,*">
                        <StackPanel Grid.Column="0" Orientation="Horizontal" Margin="4" Spacing="4">
                            <TextBlock Text="Current Experiment:" VerticalAlignment="Center" />
                            <TextBlock Text="{Binding CurrentExperimentNumber}" VerticalAlignment="Center" />
                            <TextBlock Text="{Binding CurrentCounterText}" VerticalAlignment="Center" />
                        </StackPanel>
                        <StackPanel Grid.Column="1" Orientation="Horizontal" Margin="4" Spacing="4">
                            <TextBlock Text="Go To Experiment:" VerticalAlignment="Center" />
                            <TextBox Width="60" Text="{Binding GoToExperimentText}" />
                            <Button Content="Go" Command="{Binding GoToExperimentCommand}" />
                        </StackPanel>
                    </Grid>
                    <Grid Grid.Row="1" ColumnDefinitions="*,*">
                        <Grid Grid.Column="0" ColumnDefinitions="*,*,*,*" Margin="4">
                            <Button Grid.Column="0" Content="|&lt;" Command="{Binding FirstCommand}" HorizontalAlignment="Stretch" />
                            <Button Grid.Column="1" Content="&lt;" Command="{Binding PreviousCommand}" HorizontalAlignment="Stretch" />
                            <Button Grid.Column="2" Content="&gt;" Command="{Binding NextCommand}" HorizontalAlignment="Stretch" />
                            <Button Grid.Column="3" Content="&gt;|" Command="{Binding LastCommand}" HorizontalAlignment="Stretch" />
                        </Grid>
                        <StackPanel Grid.Column="1" Margin="4" Spacing="2">
                            <TextBlock Text="{Binding CurrentManualStatusText}" FontSize="10" />
                            <TextBlock Text="{Binding CurrentAutomaticStatusText}" FontSize="10" />
                            <TextBlock Text="{Binding CurrentPeakDifference, StringFormat='ΔPeaks: {0}'}" FontSize="10" />
                            <StackPanel Orientation="Horizontal" Spacing="4">
                                <Button Content="Mark Active" Command="{Binding MarkActiveCommand}" />
                                <Button Content="Mark Inactive" Command="{Binding MarkInactiveCommand}" />
                                <Button Content="Reset" Command="{Binding ResetManualStatusCommand}" />
                            </StackPanel>
                            <Button Content="Export" Command="{Binding OpenResultsWindowCommand}" HorizontalAlignment="Stretch" />
                        </StackPanel>
                    </Grid>
                </Grid>
```

- [ ] **Step 6: Full build.**

```bash
cd dotnet && dotnet build CspAnalyzer.sln
```

Expected: Build succeeded, 0 errors. If `FindControl<CartesianChart>("PeakDiffChart")` fails to resolve at runtime later, double check the `x:Name` attributes above match exactly (`PeakDiffChart`/`ProbabilityChart`).

- [ ] **Step 7: Full test suite.**

```bash
cd dotnet && dotnet test CspAnalyzer.sln
```

Expected: all PASS.

- [ ] **Step 8: Commit (Tasks 11+12 together).**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs
git commit -m "S10b: wire charts, player nav, filter, and manual overrides into MainWindow"
```

---

### Task 13: Manual verification + close out S10b

**Files:**
- Modify: `docs/superpowers/SESSIONS.md`

No code changes - this task is verification and bookkeeping.

- [ ] **Step 1: Full solution build and test, from a clean state.**

```bash
cd dotnet && dotnet build CspAnalyzer.sln && dotnet test CspAnalyzer.sln
```

Expected: build succeeds, all tests across `BackendInterop.Tests`, `CspAnalyzer.Desktop.Tests` PASS.

- [ ] **Step 2: Run the app and walk through the golden path.**

```bash
DISPLAY=:0 dotnet run --project dotnet/CspAnalyzer.Desktop/CspAnalyzer.Desktop.csproj
```

Manually, against the real local `CSPv2/Demo-dataset` (git-ignored, kept locally per S5):
1. Load Reference, Load Dataset - confirm the Peak-Diff chart now populates immediately (no run needed) with visible threshold-zone shading and text callouts.
2. Run CSP - confirm the Probability chart and the two gauges populate after the run completes.
3. Click a bar in either chart - confirm the player jumps to that experiment (Current Experiment number changes) and the spectra-overlay chart updates.
4. Use First/Previous/Next/Last and Go-To-Experiment - confirm bounds are respected (buttons disable at the ends) and the overlay/labels update each time.
5. Check "Actives" - confirm the player now only cycles through active experiments, and unchecking/checking "Inactives" swaps it correctly (never both checked).
6. Click "Mark Active" on the current experiment - confirm the Manual Analysis Results chart updates and the manual-status label turns green.
7. Click "Reset All Man. Flags" - confirm the confirmation dialog appears, and that answering "Yes" resets everything back to "Not set" while "No" leaves it untouched (test both).
8. Click "Export" - confirm `ResultsWindow` now shows the manual flags you just set (this is the S10 window this whole session was unblocking).

Take a screenshot (`gnome-screenshot`) of at least one populated state (post-run, mid-navigation) as evidence, same as prior sessions' verification.

- [ ] **Step 3: Check off S10b in `docs/superpowers/SESSIONS.md`** - change `- [ ] **S10b**` to `- [x] **S10b**`, and append a completion note in the same style as S7-S10's entries (what was built, what was verified, anything deferred/simplified - specifically call out: per-bar conditional coloring was intentionally simplified to zone-shading only rather than the legacy Mapper-based per-point fill, since LiveChartsCore 2.x's per-point styling model differs from LiveCharts1's).

- [ ] **Step 4: Commit.**

```bash
git add docs/superpowers/SESSIONS.md
git commit -m "S10b: mark done in SESSIONS.md"
```

---

## Self-Review Notes

- **Spec coverage:** every scope item from the design spec has a task - sort fix (Task 2), nav+filter (Task 3-4), manual overrides (Task 5-6), peak-diff/probability/gauges/overlay charts (Task 7-10), click-nav+zoom-sync (Task 8, 11), wiring (Task 12), verification (Task 13).
- **Known, deliberate deviation from the spec's chart-fidelity section:** per-bar conditional `Fill` (legacy's `Mapper.Fill(item => ...)` coloring bars red past a threshold) is dropped in favor of the `RectangularSection` zone shading alone, which conveys the same threshold information without needing LiveChartsCore 2.x's different (and, after verification above, less directly analogous) per-point styling model. This is called out explicitly in Task 13's SESSIONS.md note rather than silently diverging from "full fidelity."
- **Type consistency check:** `RaiseNavigationChanged()` (Task 3) is referenced by Task 6 (manual overrides), Task 8/9 (`RunAsync`), and Task 10 (`RebuildOverlayPoints`) - same public no-arg signature throughout. `BuildPeakDiffChart()`/`BuildProbabilityChart()`/`BuildGauges()`/`BuildOverlayAxes()`/`RebuildOverlayPoints()` are all public no-arg methods on `MainViewModel`, consistent everywhere they're called (production code and tests).
