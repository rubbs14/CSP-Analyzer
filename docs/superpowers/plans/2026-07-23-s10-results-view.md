# S10 Results View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port `CSPv2/FormOutputTable.cs` (results table + 3 pie charts) to a new
Avalonia `ResultsWindow`, opened from `MainWindow`'s "Export" button, backed by
`MainViewModel.RunResults` (populated by S9).

**Architecture:** A pure-C# join (`ResultsBuilder.Build`) in `BackendInterop`
turns `RunResults` + the already-loaded `DatasetSpectra`/`ReferenceSpectrum`
into `ResultRow`s. `ResultsViewModel` (Desktop) wraps that join, derives pie
chart series (LiveChartsCore) and summary counts, and exposes CSV/XLSX/PDF
export commands. `ResultsWindow.axaml` binds a `DataGrid` + 3 `PieChart`
controls to it. `IResultsWindowService` (mirrors S8's `IFilePickerService`
pattern) keeps `MainViewModel` from constructing `Window`s directly.

**Tech Stack:** .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm 8.4.2,
LiveChartsCore.SkiaSharpView.Avalonia 2.0.5, Avalonia.Controls.DataGrid
11.2.3, ClosedXML 0.105.0, PDFsharp 6.2.4, xunit.

## Global Constraints

- Target framework `net8.0` everywhere (matches every existing `dotnet/*.csproj`).
- Pin new Avalonia-family packages to **11.2.3** exactly, matching the
  existing `Avalonia`/`Avalonia.Desktop`/`Avalonia.Themes.Fluent` pins in
  `CspAnalyzer.Desktop.csproj` (S7 hit real breakage from an unpinned
  Avalonia template pulling a mismatched major version - see
  `[[csp-analyzer-upgrade]]` memory / `SESSIONS.md` S7 history. Confirmed via
  the NuGet API that `Avalonia.Controls.DataGrid` 11.2.3 exists).
- `ClosedXML` (MIT) and `PDFsharp` (MIT) only for export - **not** QuestPDF
  (revenue-capped community license, rejected during S10 design).
- No pickled/binary assets beyond the one bundled font file (Task 5) -
  everything else stays plain text/code, matching this repo's established
  git-hygiene stance (`[[csp-analyzer-upgrade]]` S5).
- Business logic that doesn't need Avalonia stays in `dotnet/BackendInterop`
  and is TDD'd in `dotnet/BackendInterop.Tests` (established S8/S9 pattern -
  no test project references `CspAnalyzer.Desktop` yet, and this plan doesn't
  add one; Desktop-side code is verified manually by running the app, same as
  S7-S9).
- Full design reference: `docs/superpowers/specs/2026-07-23-sub-project-3-s10-results-view-design.md`.

---

## Task 1: `ResultRow` + `ResultsBuilder` (pure join logic)

**Files:**
- Create: `dotnet/BackendInterop/ResultRow.cs`
- Create: `dotnet/BackendInterop/ResultsBuilder.cs`
- Test: `dotnet/BackendInterop.Tests/ResultsBuilderTests.cs`

**Interfaces:**
- Consumes: `PeaklistSpectrum` (`ExpNumber`, `DsName`, `TotReadPeaks`,
  `Peaklist: List<Peak>`, `UserSelection`) and `SpectrumResult`
  (`ExpNumber`, `IsActive`, `ActivePseudoprobability`) - both already exist
  in `dotnet/BackendInterop`.
- Produces: `ResultRow` record and `ResultsBuilder.Build(PeaklistSpectrum
  reference, IReadOnlyList<PeaklistSpectrum> datasetSpectra,
  IReadOnlyList<SpectrumResult> runResults) -> IReadOnlyList<ResultRow>`,
  used by Task 4's `ResultsViewModel`.

- [ ] **Step 1: Write `ResultRow`**

```csharp
namespace CspAnalyzer.BackendInterop;

/// <summary>
/// One row of the S10 results table - mirrors CSPv2/FormOutputTable.cs's
/// GenerateTable columns exactly (Name/Dataset/Total Read Peaks/Min-Max
/// Intensity/Peak Difference/Probability/Automatic Analysis/Manual Flag).
/// The reference row (built directly by ResultsBuilder.Build, not joined)
/// leaves PeakDifference/Probability/AutomaticAnalysis null, matching the
/// old table's literal "none" for that row.
/// </summary>
public sealed record ResultRow(
    string Name,
    string Dataset,
    int TotalReadPeaks,
    double MinIntensity,
    double MaxIntensity,
    int? PeakDifference,
    double? Probability,
    string? AutomaticAnalysis,
    string ManualFlag);
```

- [ ] **Step 2: Write `ResultsBuilder`**

```csharp
using System.Linq;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Joins loaded dataset spectra with their classification results by
/// EXP_NUMBER - the two collections are populated separately (S8's
/// LoadDatasetAsync vs S9's RunAsync) and only come together here, for
/// display. A dataset spectrum with no matching run result is omitted
/// rather than throwing; that shouldn't happen given S9's flow (every
/// loaded spectrum is sent to the backend and every input produces one
/// output row), but silently dropping an orphan is safer for a display
/// join than crashing the whole results window over it.
/// </summary>
public static class ResultsBuilder
{
    public static IReadOnlyList<ResultRow> Build(
        PeaklistSpectrum reference,
        IReadOnlyList<PeaklistSpectrum> datasetSpectra,
        IReadOnlyList<SpectrumResult> runResults)
    {
        var rows = new List<ResultRow>
        {
            new(
                Name: "Reference",
                Dataset: reference.DsName,
                TotalReadPeaks: reference.TotReadPeaks,
                MinIntensity: reference.Peaklist.Min(p => p.Intensity),
                MaxIntensity: reference.Peaklist.Max(p => p.Intensity),
                PeakDifference: null,
                Probability: null,
                AutomaticAnalysis: null,
                ManualFlag: "none"),
        };

        Dictionary<int, SpectrumResult> resultsByExp =
            runResults.ToDictionary(r => r.ExpNumber);

        foreach (PeaklistSpectrum spectrum in datasetSpectra)
        {
            if (!resultsByExp.TryGetValue(spectrum.ExpNumber, out SpectrumResult? result))
            {
                continue;
            }

            rows.Add(new ResultRow(
                Name: spectrum.ExpNumber.ToString(),
                Dataset: spectrum.DsName,
                TotalReadPeaks: spectrum.TotReadPeaks,
                MinIntensity: spectrum.Peaklist.Min(p => p.Intensity),
                MaxIntensity: spectrum.Peaklist.Max(p => p.Intensity),
                PeakDifference: spectrum.TotReadPeaks - reference.TotReadPeaks,
                Probability: Math.Round(result.ActivePseudoprobability, 2),
                AutomaticAnalysis: result.IsActive ? "Active" : "Inactive",
                ManualFlag: spectrum.UserSelection));
        }

        return rows;
    }
}
```

Add `using System;` and `using System.Collections.Generic;` at the top
alongside `using System.Linq;` (the project has `<ImplicitUsings>enable</ImplicitUsings>`,
so these three are actually already implicit - no `using` lines are needed
at all; write the file without them).

- [ ] **Step 3: Write the failing tests**

```csharp
using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

public class ResultsBuilderTests
{
    private static PeaklistSpectrum MakeSpectrum(int expNumber, string dsName, int totReadPeaks, params double[] intensities) =>
        new()
        {
            ExpNumber = expNumber,
            DsName = dsName,
            TotReadPeaks = totReadPeaks,
            Peaklist = intensities.Select((intensity, i) => new Peak { Number = i + 1, Intensity = intensity }).ToList(),
        };

    [Fact]
    public void Build_puts_the_reference_row_first_with_none_for_result_fields()
    {
        PeaklistSpectrum reference = MakeSpectrum(11, "gpHUB1_FR_REF_pool1_130416", 83, 1000, 5000, 23499);

        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(reference, Array.Empty<PeaklistSpectrum>(), Array.Empty<SpectrumResult>());

        ResultRow row = Assert.Single(rows);
        Assert.Equal("Reference", row.Name);
        Assert.Equal("gpHUB1_FR_REF_pool1_130416", row.Dataset);
        Assert.Equal(83, row.TotalReadPeaks);
        Assert.Equal(1000, row.MinIntensity);
        Assert.Equal(23499, row.MaxIntensity);
        Assert.Null(row.PeakDifference);
        Assert.Null(row.Probability);
        Assert.Null(row.AutomaticAnalysis);
        Assert.Equal("none", row.ManualFlag);
    }

    [Fact]
    public void Build_joins_a_dataset_spectrum_with_its_matching_run_result()
    {
        PeaklistSpectrum reference = MakeSpectrum(11, "ref_ds", 80, 100, 200);
        var spectrum = MakeSpectrum(101, "gpHUB1_FS_pool1_130416", 64, 50, 900);
        spectrum.UserSelection = "Not set";
        var result = new SpectrumResult { ExpNumber = 101, IsActive = true, ActivePseudoprobability = 0.9137 };

        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(reference, new[] { spectrum }, new[] { result });

        Assert.Equal(2, rows.Count);
        ResultRow row = rows[1];
        Assert.Equal("101", row.Name);
        Assert.Equal("gpHUB1_FS_pool1_130416", row.Dataset);
        Assert.Equal(64, row.TotalReadPeaks);
        Assert.Equal(50, row.MinIntensity);
        Assert.Equal(900, row.MaxIntensity);
        Assert.Equal(64 - 80, row.PeakDifference);
        Assert.Equal(0.91, row.Probability);
        Assert.Equal("Active", row.AutomaticAnalysis);
        Assert.Equal("Not set", row.ManualFlag);
    }

    [Fact]
    public void Build_omits_a_dataset_spectrum_with_no_matching_run_result()
    {
        PeaklistSpectrum reference = MakeSpectrum(11, "ref_ds", 80, 100, 200);
        var spectrum = MakeSpectrum(101, "ds", 64, 50, 900);

        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(reference, new[] { spectrum }, Array.Empty<SpectrumResult>());

        Assert.Single(rows); // reference row only
    }

    [Fact]
    public void Build_reports_a_negative_peak_difference_when_the_experiment_has_fewer_peaks_than_the_reference()
    {
        PeaklistSpectrum reference = MakeSpectrum(11, "ref_ds", 100, 1, 2);
        var spectrum = MakeSpectrum(101, "ds", 30, 1, 2);
        var result = new SpectrumResult { ExpNumber = 101, IsActive = false, ActivePseudoprobability = 0.1 };

        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(reference, new[] { spectrum }, new[] { result });

        Assert.Equal(-70, rows[1].PeakDifference);
    }
}
```

- [ ] **Step 4: Run the tests, confirm the first three fail to compile/run (types don't exist yet), then confirm all pass after Step 1-2**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet && dotnet test CspAnalyzer.sln --filter ResultsBuilderTests
```
Expected: 4 passed (after Steps 1-2 are in place; if run before them, the
project fails to build - that's the "confirm it fails" signal here since
these are new types, not a behavior change to existing code).

- [ ] **Step 5: Commit**

```bash
git add dotnet/BackendInterop/ResultRow.cs dotnet/BackendInterop/ResultsBuilder.cs dotnet/BackendInterop.Tests/ResultsBuilderTests.cs
git commit -m "S10: add ResultRow + ResultsBuilder join (RunResults x DatasetSpectra)"
```

---

## Task 2: Add charting + DataGrid dependencies

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/CspAnalyzer.Desktop.csproj`
- Modify: `dotnet/CspAnalyzer.Desktop/App.axaml`

**Interfaces:**
- Consumes: nothing.
- Produces: `LiveChartsCore.SkiaSharpView.Avalonia`'s `PieChart` control and
  `LiveChartsCore`'s `ISeries`/`PieSeries<T>` types (used by Task 4/6);
  `Avalonia.Controls.DataGrid`'s `DataGrid`/`DataGridTextColumn`/`DataGridRow`
  types (used by Task 6).

- [ ] **Step 1: Add the package references**

In `dotnet/CspAnalyzer.Desktop/CspAnalyzer.Desktop.csproj`, inside the
existing `<ItemGroup>` that has `<PackageReference Include="CommunityToolkit.Mvvm" ...>`,
add two more lines:

```xml
    <PackageReference Include="LiveChartsCore.SkiaSharpView.Avalonia" Version="2.0.5" />
    <PackageReference Include="Avalonia.Controls.DataGrid" Version="11.2.3" />
```

- [ ] **Step 2: Register the DataGrid theme**

Avalonia's DataGrid control ships its own theme resources that must be
included explicitly (the docs.avaloniaui.net DataGrid page documents this
pattern). In `dotnet/CspAnalyzer.Desktop/App.axaml`, change:

```xml
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
```

to:

```xml
    <Application.Styles>
        <FluentTheme />
        <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.axaml" />
    </Application.Styles>
```

- [ ] **Step 3: Restore and build to confirm the new packages resolve**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet && dotnet build CspAnalyzer.sln
```
Expected: `Build succeeded.` 0 errors (no code uses the new packages yet,
so this only proves the package references and theme include are valid).

- [ ] **Step 4: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/CspAnalyzer.Desktop.csproj dotnet/CspAnalyzer.Desktop/App.axaml
git commit -m "S10: add LiveChartsCore.SkiaSharpView.Avalonia + Avalonia.Controls.DataGrid deps"
```

---

## Task 3: `IFilePickerService.PickSaveFileAsync`

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/Services/IFilePickerService.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/Services/AvaloniaFilePickerService.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/Services/NullFilePickerService.cs`

**Interfaces:**
- Consumes: `Avalonia.Platform.Storage.IStorageProvider.SaveFilePickerAsync`
  (same `TopLevel.StorageProvider` already used by `AvaloniaFilePickerService`).
- Produces: `IFilePickerService.PickSaveFileAsync(string suggestedFileName,
  string extension) -> Task<string?>`, used by Task 5's export commands.

- [ ] **Step 1: Extend the interface**

In `IFilePickerService.cs`, add a third method:

```csharp
    Task<string?> PickSaveFileAsync(string suggestedFileName, string extension);
```

- [ ] **Step 2: Implement it in `AvaloniaFilePickerService`**

```csharp
    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string extension)
    {
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            FileTypeChoices = new[] { new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = new[] { $"*.{extension}" } } },
        });
        return file?.TryGetLocalPath();
    }
```

- [ ] **Step 3: Implement the no-op in `NullFilePickerService`**

```csharp
    public Task<string?> PickSaveFileAsync(string suggestedFileName, string extension) => Task.FromResult<string?>(null);
```

- [ ] **Step 4: Build to confirm both implementations satisfy the interface**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet && dotnet build CspAnalyzer.sln
```
Expected: `Build succeeded.` (no test exists for this - it's a thin wrapper
over an Avalonia API that needs a live window to exercise for real, same
reasoning S8 used for not testing `AvaloniaFilePickerService`'s existing
two methods; manual verification happens in Task 9).

- [ ] **Step 5: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Services/IFilePickerService.cs dotnet/CspAnalyzer.Desktop/Services/AvaloniaFilePickerService.cs dotnet/CspAnalyzer.Desktop/Services/NullFilePickerService.cs
git commit -m "S10: add IFilePickerService.PickSaveFileAsync for export commands"
```

---

## Task 4: `ResultsViewModel` (rows, summary counts, pie series, Refresh)

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/ResultsViewModel.cs`

**Interfaces:**
- Consumes: `ResultsBuilder.Build` (Task 1), `IFilePickerService` (Task 3,
  stored for Task 5's export commands but not called yet in this task).
- Produces: `ResultsViewModel` with `ObservableCollection<ResultRow> Rows`,
  `int TotalExperiments/ActivesAuto/InactivesAuto/ActivesManual/
  InactivesManual/NotSetManual`, `ISeries[] OverviewSeries/AutoSeries/
  ManualSeries`, `RefreshCommand`, `string ExportStatusText` - all consumed
  by Task 6's `ResultsWindow.axaml`. Constructor:
  `ResultsViewModel(IFilePickerService filePicker, PeaklistSpectrum
  reference, IReadOnlyList<PeaklistSpectrum> datasetSpectra,
  IReadOnlyList<SpectrumResult> runResults)`, consumed by Task 7's
  `MainViewModel.OpenResultsWindow`.

No automated test for this task (see Global Constraints: Avalonia/LiveCharts
view-model wiring is manually verified by running the app, matching S7-S9's
established precedent - there is no test project targeting
`CspAnalyzer.Desktop`, and the count/row logic this wraps is already
covered by Task 1's `ResultsBuilderTests`). Verify by building only; full
behavior is checked in Task 9's manual pass.

- [ ] **Step 1: Write `ResultsViewModel`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10: backs ResultsWindow, the port of CSPv2/FormOutputTable.cs. Built
/// fresh from the reference/dataset/run-results snapshot MainViewModel
/// passes in when the window is opened - no back-reference to
/// MainViewModel, so this is independently constructible and (mechanically)
/// testable without a live MainViewModel or Window.
/// </summary>
public partial class ResultsViewModel : ViewModelBase
{
    // CSPv2/FormOutputTable.cs's SolidColorBrush fields use
    // System.Windows.Media.Color.FromArgb(a, r, g, b); SkiaSharp's SKColor
    // constructor is (r, g, b, a) - reordered here, not a color change.
    private static readonly SKColor ActiveAutoColor = new(45, 161, 63, 200);
    private static readonly SKColor InactiveAutoColor = new(225, 9, 20, 180);
    private static readonly SKColor ActiveManualColor = new(123, 217, 157, 200);
    private static readonly SKColor InactiveManualColor = new(199, 137, 137, 180);
    private static readonly SKColor NotSetManualColor = new(178, 178, 178, 180);

    private readonly IFilePickerService _filePicker;
    private readonly PeaklistSpectrum _reference;
    private readonly IReadOnlyList<PeaklistSpectrum> _datasetSpectra;
    private readonly IReadOnlyList<SpectrumResult> _runResults;

    public ObservableCollection<ResultRow> Rows { get; } = new();

    [ObservableProperty]
    private int _totalExperiments;

    [ObservableProperty]
    private int _activesAuto;

    [ObservableProperty]
    private int _inactivesAuto;

    [ObservableProperty]
    private int _activesManual;

    [ObservableProperty]
    private int _inactivesManual;

    [ObservableProperty]
    private int _notSetManual;

    [ObservableProperty]
    private ISeries[] _overviewSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _autoSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _manualSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private string _exportStatusText = "";

    public ResultsViewModel(
        IFilePickerService filePicker,
        PeaklistSpectrum reference,
        IReadOnlyList<PeaklistSpectrum> datasetSpectra,
        IReadOnlyList<SpectrumResult> runResults)
    {
        _filePicker = filePicker;
        _reference = reference;
        _datasetSpectra = datasetSpectra;
        _runResults = runResults;
        Rebuild();
    }

    [RelayCommand]
    private void Refresh() => Rebuild();

    private void Rebuild()
    {
        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(_reference, _datasetSpectra, _runResults);

        Rows.Clear();
        foreach (ResultRow row in rows)
        {
            Rows.Add(row);
        }

        TotalExperiments = Rows.Count - 1;
        ActivesAuto = Rows.Count(r => r.AutomaticAnalysis == "Active");
        InactivesAuto = Rows.Count(r => r.AutomaticAnalysis == "Inactive");
        ActivesManual = Rows.Count(r => r.ManualFlag == "ACTIVE (MAN)");
        InactivesManual = Rows.Count(r => r.ManualFlag == "INACTIVE (MAN)");
        NotSetManual = Rows.Count(r => r.ManualFlag == "Not set");

        OverviewSeries = new ISeries[]
        {
            PieSlice("Actives", ActivesAuto, ActiveAutoColor),
            PieSlice("Inactives", InactivesAuto, InactiveAutoColor),
            PieSlice("Manual: Not set", NotSetManual, NotSetManualColor),
            PieSlice("Manual: Actives", ActivesManual, ActiveManualColor),
            PieSlice("Manual: Inactives", InactivesManual, InactiveManualColor),
        };

        AutoSeries = new ISeries[]
        {
            PieSlice("Actives", ActivesAuto, ActiveAutoColor),
            PieSlice("Inactives", InactivesAuto, InactiveAutoColor),
        };

        ManualSeries = new ISeries[]
        {
            PieSlice("Manual: Actives", ActivesManual, ActiveManualColor),
            PieSlice("Manual: Inactives", InactivesManual, InactiveManualColor),
            PieSlice("Manual: Not set", NotSetManual, NotSetManualColor),
        };
    }

    private static PieSeries<int> PieSlice(string name, int value, SKColor color) => new()
    {
        Name = name,
        Values = new[] { value },
        Fill = new SolidColorPaint(color),
    };
}
```

(`ImplicitUsings` is enabled on this project too, so `System`,
`System.Collections.Generic`, and `System.Linq` need no explicit `using`.)

- [ ] **Step 2: Build**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet && dotnet build CspAnalyzer.sln
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/ResultsViewModel.cs
git commit -m "S10: add ResultsViewModel (rows, summary counts, pie series)"
```

---

## Task 5: Export commands (CSV / XLSX / PDF)

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/CspAnalyzer.Desktop.csproj`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/ResultsViewModel.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/DejaVuFontResolver.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Assets/Fonts/DejaVuSans.ttf` (binary,
  copied from the local system - see Step 1)

**Interfaces:**
- Consumes: `IFilePickerService.PickSaveFileAsync` (Task 3), `ResultRow`
  (Task 1), `ResultsViewModel.Rows` (Task 4).
- Produces: `ResultsViewModel.ExportCsvCommand` / `ExportXlsxCommand` /
  `ExportPdfCommand`, bound by Task 6's `ResultsWindow.axaml`.

PDFsharp 6's Core build ships no fonts and has no OS font auto-discovery
(unlike the old WinForms app's GDI+, which just used whatever Windows had
installed) - without a font resolver, the first `XFont` construction throws.
This bundles one open, freely-redistributable font (DejaVu Sans, SIL Open
Font License, already installed on this dev box at
`/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf` via the `fonts-dejavu-core`
package) as a checked-in binary asset, so PDF export works identically on
any machine regardless of what fonts are installed there.

- [ ] **Step 1: Copy the font into the repo**

```bash
mkdir -p /home/rubbs/REPOS/CSP-Analyzer/dotnet/CspAnalyzer.Desktop/Assets/Fonts
cp /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /home/rubbs/REPOS/CSP-Analyzer/dotnet/CspAnalyzer.Desktop/Assets/Fonts/DejaVuSans.ttf
```

If this exact path doesn't exist on the machine running this step, find it
first with `fc-list | grep -i "dejavu sans:style=book"` (avoid the Bold/
Oblique/Mono variants) and copy from whatever path that reports instead.

- [ ] **Step 2: Add the ClosedXML/PDFsharp package references and the font's build action**

In `CspAnalyzer.Desktop.csproj`, add to the same `<ItemGroup>` as Task 2's
additions:

```xml
    <PackageReference Include="ClosedXML" Version="0.105.0" />
    <PackageReference Include="PDFsharp" Version="6.2.4" />
```

And add a new `<ItemGroup>` so the font file is copied next to the built
executable:

```xml
  <ItemGroup>
    <Content Include="Assets\Fonts\DejaVuSans.ttf" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 3: Write the font resolver**

```csharp
using PdfSharp.Fonts;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// PDFsharp 6's Core build has no bundled fonts or OS font discovery - see
/// Task 5's note in docs/superpowers/plans/2026-07-23-s10-results-view.md.
/// Always serves the one bundled DejaVu Sans face regardless of the
/// requested family/weight/style, since the PDF report (Task 5's
/// ExportPdfAsync) only ever asks for one face.
/// </summary>
public sealed class DejaVuFontResolver(byte[] fontBytes) : IFontResolver
{
    private const string FaceName = "DejaVuSans";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(FaceName, false, false);

    public byte[] GetFont(string faceName) => fontBytes;
}
```

- [ ] **Step 4: Add the three export commands to `ResultsViewModel`**

Add these `using` lines at the top of `ResultsViewModel.cs`:

```csharp
using System.IO;
using System.Text;
using ClosedXML.Excel;
using CspAnalyzer.Desktop.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
```

Add these members inside the class (after `Refresh`):

```csharp
    private static bool _fontResolverInitialized;

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        string? path = await _filePicker.PickSaveFileAsync("csp_results.csv", "csv");
        if (path is null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Name,Dataset,Total Read Peaks,Min Intensity (AU),Max Intensity (AU),Peak Difference to Reference,Probability,Automatic Analysis,Manual Flag");
        foreach (ResultRow row in Rows)
        {
            sb.AppendLine(string.Join(",",
                CsvField(row.Name), CsvField(row.Dataset), row.TotalReadPeaks,
                row.MinIntensity, row.MaxIntensity,
                row.PeakDifference?.ToString() ?? "none",
                row.Probability?.ToString() ?? "none",
                CsvField(row.AutomaticAnalysis ?? "none"),
                CsvField(row.ManualFlag)));
        }

        await File.WriteAllTextAsync(path, sb.ToString());
        ExportStatusText = $"Exported CSV to {path}";
    }

    private static string CsvField(string value) => value.Contains(',') ? $"\"{value}\"" : value;

    [RelayCommand]
    private async Task ExportXlsxAsync()
    {
        string? path = await _filePicker.PickSaveFileAsync("csp_results.xlsx", "xlsx");
        if (path is null)
        {
            return;
        }

        await Task.Run(() => WriteXlsx(path));
        ExportStatusText = $"Exported XLSX to {path}";
    }

    private void WriteXlsx(string path)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add("CSP_Output");

        string[] headers =
        {
            "Name", "Dataset", "Total Read Peaks", "Min Intensity (AU)", "Max Intensity (AU)",
            "Peak Difference to Reference", "Probability", "Automatic Analysis", "Manual Flag",
        };
        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        int rowIndex = 2;
        foreach (ResultRow row in Rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.Name;
            sheet.Cell(rowIndex, 2).Value = row.Dataset;
            sheet.Cell(rowIndex, 3).Value = row.TotalReadPeaks;
            sheet.Cell(rowIndex, 4).Value = row.MinIntensity;
            sheet.Cell(rowIndex, 5).Value = row.MaxIntensity;
            sheet.Cell(rowIndex, 6).Value = row.PeakDifference?.ToString() ?? "none";
            sheet.Cell(rowIndex, 7).Value = row.Probability?.ToString() ?? "none";
            sheet.Cell(rowIndex, 8).Value = row.AutomaticAnalysis ?? "none";
            sheet.Cell(rowIndex, 9).Value = row.ManualFlag;
            rowIndex++;
        }

        workbook.SaveAs(path);
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        string? path = await _filePicker.PickSaveFileAsync("csp_results.pdf", "pdf");
        if (path is null)
        {
            return;
        }

        await Task.Run(() => WritePdf(path));
        ExportStatusText = $"Exported PDF to {path}";
    }

    private void WritePdf(string path)
    {
        if (!_fontResolverInitialized)
        {
            string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "DejaVuSans.ttf");
            GlobalFontSettings.FontResolver = new DejaVuFontResolver(File.ReadAllBytes(fontPath));
            _fontResolverInitialized = true;
        }

        string[] headers = { "Name", "Dataset", "Peaks", "Min Int.", "Max Int.", "Peak Diff", "Probability", "Auto", "Manual" };
        double[] columnWidths = { 60, 150, 50, 55, 55, 55, 65, 55, 90 };
        var titleFont = new XFont("DejaVu Sans", 16, XFontStyleEx.Bold);
        var headerFont = new XFont("DejaVu Sans", 9, XFontStyleEx.Bold);
        var cellFont = new XFont("DejaVu Sans", 9, XFontStyleEx.Regular);

        var document = new PdfDocument();
        PdfPage page = NewLandscapePage(document);
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double y = DrawPageHeader(gfx, titleFont, headerFont, headers, columnWidths);

        foreach (ResultRow row in Rows)
        {
            if (y > page.Height.Point - 40)
            {
                gfx.Dispose();
                page = NewLandscapePage(document);
                gfx = XGraphics.FromPdfPage(page);
                y = DrawPageHeader(gfx, titleFont, headerFont, headers, columnWidths);
            }

            double x = 20;
            string[] cells =
            {
                row.Name, row.Dataset, row.TotalReadPeaks.ToString(),
                row.MinIntensity.ToString("F0"), row.MaxIntensity.ToString("F0"),
                row.PeakDifference?.ToString() ?? "none",
                row.Probability?.ToString() ?? "none",
                row.AutomaticAnalysis ?? "none", row.ManualFlag,
            };
            for (int i = 0; i < cells.Length; i++)
            {
                gfx.DrawString(cells[i], cellFont, XBrushes.Black, new XRect(x, y, columnWidths[i], 16), XStringFormats.CenterLeft);
                x += columnWidths[i];
            }
            y += 16;
        }

        gfx.Dispose();
        document.Save(path);
    }

    private static PdfPage NewLandscapePage(PdfDocument document)
    {
        PdfPage page = document.AddPage();
        page.Orientation = PdfSharp.PageOrientation.Landscape;
        return page;
    }

    private static double DrawPageHeader(XGraphics gfx, XFont titleFont, XFont headerFont, string[] headers, double[] columnWidths)
    {
        gfx.DrawString("CSP Analysis Report", titleFont, XBrushes.Black, new XPoint(20, 30));
        gfx.DrawString(DateTime.Now.ToString("f"), headerFont, XBrushes.Black, new XPoint(20, 48));

        double x = 20;
        const double y = 70;
        for (int i = 0; i < headers.Length; i++)
        {
            gfx.DrawString(headers[i], headerFont, XBrushes.Black, new XRect(x, y, columnWidths[i], 18), XStringFormats.CenterLeft);
            x += columnWidths[i];
        }
        return y + 20;
    }
```

- [ ] **Step 5: Build**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet && dotnet build CspAnalyzer.sln
```
Expected: `Build succeeded.` (Real functional verification - does the PDF
actually open, does the font render - happens in Task 9, since it needs a
real `IFilePickerService.PickSaveFileAsync` call from a live window; there's
no automated test for this by design, see Task 4's note.)

- [ ] **Step 6: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/CspAnalyzer.Desktop.csproj dotnet/CspAnalyzer.Desktop/ViewModels/ResultsViewModel.cs dotnet/CspAnalyzer.Desktop/Services/DejaVuFontResolver.cs dotnet/CspAnalyzer.Desktop/Assets/Fonts/DejaVuSans.ttf
git commit -m "S10: add CSV/XLSX/PDF export commands, bundle DejaVu Sans for cross-platform PDF text"
```

---

## Task 6: `ResultsWindow` view

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Converters/AutoAnalysisRowBrushConverter.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml`
- Create: `dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml.cs`

**Interfaces:**
- Consumes: `ResultsViewModel` (Task 4) as `x:DataType`/`DataContext`.
- Produces: `ResultsWindow` (a `Window`), constructed by Task 7's
  `AvaloniaResultsWindowService`.

No `Design.DataContext` on this window (unlike `MainWindow.axaml`) -
`ResultsViewModel` has no parameterless constructor (it always needs a real
reference/dataset/results snapshot to be meaningful) and manufacturing a
fake one just for the XAML designer preview isn't worth it given this
project verifies views by actually running the app (S7-S9 precedent), not
via the XAML designer.

- [ ] **Step 1: Write the row-coloring converter**

```csharp
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CspAnalyzer.Desktop.Converters;

/// <summary>
/// DataGridRow background for ResultsWindow - reproduces
/// CSPv2/FormOutputTable.cs's dataGridView1_CellFormatting
/// (LightGreen/PaleVioletRed for Active/Inactive) as a per-row Background
/// instead of per-cell, since Avalonia's DataGrid styles rows more
/// naturally than WinForms' per-cell formatting event did.
/// </summary>
public sealed class AutoAnalysisRowBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value as string switch
        {
            "Active" => Brushes.LightGreen,
            "Inactive" => new SolidColorBrush(Color.FromRgb(219, 112, 147)), // PaleVioletRed
            _ => Brushes.Transparent,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

(`ImplicitUsings` covers `System`/`System.Globalization` here too.)

- [ ] **Step 2: Write `ResultsWindow.axaml`**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:CspAnalyzer.Desktop.ViewModels"
        xmlns:conv="using:CspAnalyzer.Desktop.Converters"
        xmlns:lvc="using:LiveChartsCore.SkiaSharpView.Avalonia"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d" d:DesignWidth="1200" d:DesignHeight="800"
        x:Class="CspAnalyzer.Desktop.Views.ResultsWindow"
        x:DataType="vm:ResultsViewModel"
        Title="Export Data"
        Width="1200" Height="800">

    <!--
    S10 port of CSPv2/FormOutputTable.cs: a results table (port of
    dataGridView1) plus 3 pie charts (port of pieChartAll/pieChartAuto/
    pieChartManual). See docs/superpowers/specs/
    2026-07-23-sub-project-3-s10-results-view-design.md.
    -->

    <Window.Resources>
        <conv:AutoAnalysisRowBrushConverter x:Key="AutoAnalysisRowBrushConverter" />
    </Window.Resources>

    <Grid RowDefinitions="Auto,*">

        <Grid Grid.Row="0" ColumnDefinitions="*,*,*,Auto" Height="220" Margin="4">
            <Border Grid.Column="0" BorderThickness="1" BorderBrush="Gray" Margin="2">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="Overview" HorizontalAlignment="Center" FontWeight="SemiBold" />
                    <lvc:PieChart Series="{Binding OverviewSeries}" LegendPosition="Right" />
                </DockPanel>
            </Border>
            <Border Grid.Column="1" BorderThickness="1" BorderBrush="Gray" Margin="2">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="Automatic Analysis" HorizontalAlignment="Center" FontWeight="SemiBold" />
                    <lvc:PieChart Series="{Binding AutoSeries}" LegendPosition="Right" />
                </DockPanel>
            </Border>
            <Border Grid.Column="2" BorderThickness="1" BorderBrush="Gray" Margin="2">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="Manual Analysis" HorizontalAlignment="Center" FontWeight="SemiBold" />
                    <lvc:PieChart Series="{Binding ManualSeries}" LegendPosition="Right" />
                </DockPanel>
            </Border>
            <Border Grid.Column="3" BorderThickness="1" BorderBrush="Gray" Margin="2" Width="180">
                <StackPanel Margin="6" Spacing="4">
                    <TextBlock Text="Run Info" FontWeight="SemiBold" />
                    <TextBlock Text="{Binding TotalExperiments, StringFormat='Total Exp.: {0}'}" />
                    <TextBlock Text="{Binding ActivesAuto, StringFormat='Actives: {0}'}" />
                    <TextBlock Text="{Binding InactivesAuto, StringFormat='Inactives: {0}'}" />
                    <TextBlock Text="{Binding ActivesManual, StringFormat='Man. Actives: {0}'}" />
                    <TextBlock Text="{Binding InactivesManual, StringFormat='Man. Inactives: {0}'}" />
                    <TextBlock Text="{Binding NotSetManual, StringFormat='Not set: {0}'}" />
                    <Button Content="Refresh" Command="{Binding RefreshCommand}" HorizontalAlignment="Stretch" />
                    <Button Content="Export CSV" Command="{Binding ExportCsvCommand}" HorizontalAlignment="Stretch" />
                    <Button Content="Export XLSX" Command="{Binding ExportXlsxCommand}" HorizontalAlignment="Stretch" />
                    <Button Content="Export PDF" Command="{Binding ExportPdfCommand}" HorizontalAlignment="Stretch" />
                    <TextBlock Text="{Binding ExportStatusText}" TextWrapping="Wrap" FontSize="11" />
                </StackPanel>
            </Border>
        </Grid>

        <DataGrid Grid.Row="1" Margin="4" ItemsSource="{Binding Rows}" AutoGenerateColumns="False" IsReadOnly="True">
            <DataGrid.Styles>
                <Style Selector="DataGridRow">
                    <Setter Property="Background" Value="{Binding AutomaticAnalysis, Converter={StaticResource AutoAnalysisRowBrushConverter}}" />
                </Style>
            </DataGrid.Styles>
            <DataGrid.Columns>
                <DataGridTextColumn Header="Name" Binding="{Binding Name}" />
                <DataGridTextColumn Header="Dataset" Binding="{Binding Dataset}" />
                <DataGridTextColumn Header="Total Read Peaks" Binding="{Binding TotalReadPeaks}" />
                <DataGridTextColumn Header="Min Intensity (AU)" Binding="{Binding MinIntensity}" />
                <DataGridTextColumn Header="Max Intensity (AU)" Binding="{Binding MaxIntensity}" />
                <DataGridTextColumn Header="Peak Difference to Reference" Binding="{Binding PeakDifference}" />
                <DataGridTextColumn Header="Probability" Binding="{Binding Probability}" />
                <DataGridTextColumn Header="Automatic Analysis" Binding="{Binding AutomaticAnalysis}" />
                <DataGridTextColumn Header="Manual Flag" Binding="{Binding ManualFlag}" />
            </DataGrid.Columns>
        </DataGrid>
    </Grid>
</Window>
```

- [ ] **Step 3: Write the code-behind**

```csharp
using Avalonia.Controls;

namespace CspAnalyzer.Desktop.Views;

public partial class ResultsWindow : Window
{
    public ResultsWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Build**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet && dotnet build CspAnalyzer.sln
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Converters/AutoAnalysisRowBrushConverter.cs dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml dotnet/CspAnalyzer.Desktop/Views/ResultsWindow.axaml.cs
git commit -m "S10: add ResultsWindow view (table + 3 pie charts)"
```

---

## Task 7: Wire it up (`IResultsWindowService`, `MainViewModel`, `MainWindow`)

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Services/IResultsWindowService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/AvaloniaResultsWindowService.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/NullResultsWindowService.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml`
- Modify: `dotnet/CspAnalyzer.Desktop/App.axaml.cs`

**Interfaces:**
- Consumes: `ResultsViewModel` (Task 4), `ResultsWindow` (Task 6).
- Produces: `MainViewModel.OpenResultsWindowCommand`, bound by
  `MainWindow.axaml`'s Export button.

- [ ] **Step 1: `IResultsWindowService`**

```csharp
using CspAnalyzer.Desktop.ViewModels;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Opens the S10 results window without MainViewModel constructing an
/// Avalonia Window directly - mirrors IFilePickerService's reasoning (S8):
/// keeps the ViewModel usable in a no-window context (design-time, tests).
/// </summary>
public interface IResultsWindowService
{
    void Show(ResultsViewModel viewModel);
}
```

- [ ] **Step 2: `AvaloniaResultsWindowService`**

```csharp
using Avalonia.Controls;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaResultsWindowService(Window owner) : IResultsWindowService
{
    public void Show(ResultsViewModel viewModel)
    {
        var window = new ResultsWindow { DataContext = viewModel };
        window.Show(owner);
    }
}
```

- [ ] **Step 3: `NullResultsWindowService`**

```csharp
using CspAnalyzer.Desktop.ViewModels;

namespace CspAnalyzer.Desktop.Services;

/// <summary>No-op for the Avalonia design-time DataContext, where no real window exists.</summary>
public sealed class NullResultsWindowService : IResultsWindowService
{
    public void Show(ResultsViewModel viewModel)
    {
    }
}
```

- [ ] **Step 4: Wire `MainViewModel`**

Change the two constructors from:

```csharp
    public MainViewModel() : this(new NullFilePickerService())
    {
    }

    public MainViewModel(IFilePickerService filePicker)
    {
        _filePicker = filePicker;
    }
```

to:

```csharp
    public MainViewModel() : this(new NullFilePickerService(), new NullResultsWindowService())
    {
    }

    public MainViewModel(IFilePickerService filePicker, IResultsWindowService resultsWindowService)
    {
        _filePicker = filePicker;
        _resultsWindowService = resultsWindowService;
    }
```

Add the field next to `_filePicker`:

```csharp
    private readonly IResultsWindowService _resultsWindowService;
```

Add the command (anywhere after `RunAsync`/`CancelRun`, e.g. at the end of
the class before the closing brace):

```csharp
    private bool CanOpenResultsWindow() => RunResults.Count > 0;

    [RelayCommand(CanExecute = nameof(CanOpenResultsWindow))]
    private void OpenResultsWindow()
    {
        var resultsViewModel = new ResultsViewModel(_filePicker, ReferenceSpectrum!, DatasetSpectra.ToList(), RunResults.ToList());
        _resultsWindowService.Show(resultsViewModel);
    }
```

In `RunAsync`, inside the `if (result.IsSuccess)` branch, right after the
`foreach (SpectrumResult r in parsed) { RunResults.Add(r); }` loop, add:

```csharp
                OpenResultsWindowCommand.NotifyCanExecuteChanged();
```

- [ ] **Step 5: Wire the Export button in `MainWindow.axaml`**

Find (in the bottom bar's player/export section):

```xml
                            <Button Content="Export" />
```

Change to:

```xml
                            <Button Content="Export" Command="{Binding OpenResultsWindowCommand}" />
```

- [ ] **Step 6: Wire construction in `App.axaml.cs`**

Change:

```csharp
            var window = new MainWindow();
            window.DataContext = new MainViewModel(new AvaloniaFilePickerService(window));
            desktop.MainWindow = window;
```

to:

```csharp
            var window = new MainWindow();
            window.DataContext = new MainViewModel(new AvaloniaFilePickerService(window), new AvaloniaResultsWindowService(window));
            desktop.MainWindow = window;
```

- [ ] **Step 7: Build and run the existing test suite (nothing here has new automated tests, but this must not break Task 1's or the pre-existing suite)**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet && dotnet build CspAnalyzer.sln && dotnet test CspAnalyzer.sln
```
Expected: `Build succeeded.` and all existing + Task 1 tests pass (the
BackendCliRunner integration tests self-skip if the `csp_modern` conda env
isn't present on the machine running this - that's expected, not a failure).

- [ ] **Step 8: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Services/IResultsWindowService.cs dotnet/CspAnalyzer.Desktop/Services/AvaloniaResultsWindowService.cs dotnet/CspAnalyzer.Desktop/Services/NullResultsWindowService.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml dotnet/CspAnalyzer.Desktop/App.axaml.cs
git commit -m "S10: wire Export button to open ResultsWindow via IResultsWindowService"
```

---

## Task 8: Update `SESSIONS.md`

**Files:**
- Modify: `docs/superpowers/SESSIONS.md`

**Interfaces:** none (documentation only).

- [ ] **Step 1: Check off S10 and add the deferred-work session**

Change:

```markdown
- [ ] **S10** — Results view: tables + charts (replace LiveCharts/WinForms with an
  Avalonia charting approach). Port FormOutputTable. `MainViewModel.RunResults`
  (populated by S9) is ready to bind.
```

to:

```markdown
- [x] **S10** — Results view: `ResultsWindow` (table + 3 pie charts, port of
  `FormOutputTable`), opened from `MainWindow`'s "Export" button. Charting via
  LiveChartsCore.SkiaSharpView.Avalonia. CSV/XLSX (ClosedXML)/PDF (PDFsharp,
  bundled DejaVu Sans font) export replace the old Excel-interop/GDI+ print
  buttons. See `docs/superpowers/specs/2026-07-23-sub-project-3-s10-results-view-design.md`.
- [ ] **S10b** — Form1's own embedded charts, deferred from S10: peak-diff/
  probability bar charts + spectra-overlay scatter (raw peaklist N/H/intensity
  data, not `RunResults` - separate from `ResultsWindow`). Also the manual-
  override workflow (ACTIVE (MAN)/INACTIVE (MAN)/Not-set toggles via
  player-nav buttons + checkboxes, `Form1.cs`'s `MAN_ACTIVES`/`MAN_INACTIVES`)
  so `ResultsWindow`'s Manual Flag column/pie chart have real data instead of
  always showing 100% Not-set.
```

- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/SESSIONS.md
git commit -m "S10: mark done in SESSIONS.md, add S10b for deferred charts/manual-override"
```

---

## Task 9: Manual end-to-end verification

**Files:** none (verification only - no commit unless a check below fails
and needs a fix, in which case fix it, re-run the relevant earlier task's
build/test step, and commit that fix separately with a message describing
what was wrong).

- [ ] **Step 1: Full build + test**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet && dotnet build CspAnalyzer.sln && dotnet test CspAnalyzer.sln
```
Expected: 0 build errors, all tests pass (BackendCliRunner integration
tests only if `csp_modern` conda env is present on this machine).

- [ ] **Step 2: Run the app and complete a real run**

```bash
cd /home/rubbs/REPOS/CSP-Analyzer/dotnet/CspAnalyzer.Desktop && DISPLAY=:0 dotnet run
```
In the running app: Load Reference and Load Dataset against the real local
`CSPv2/Demo-dataset`, click "Run CSP", wait for it to finish (this is the
same manual flow S9 verified - 83 reference peaks / 64 experiments / a real
run classifying all 64).

- [ ] **Step 3: Open and verify the results window**

Click "Export". Confirm: the window opens; the table has 65 rows (1
reference + 64 experiments); the "Total Exp." label reads 64; the Overview/
Automatic/Manual pie charts render with slices that visually match the
Actives/Inactives counts; Active rows are green-tinted, Inactive rows are
red-tinted; the Manual pie chart is 100% "Manual: Not set" (expected -
S10b hasn't been built yet). Take a screenshot (`gnome-screenshot`, matching
S7-S9's verification pattern).

- [ ] **Step 4: Verify Refresh and all three exports**

Click "Refresh" - table/charts should look unchanged (no data has changed
since load). Click "Export CSV", save to a temp path, confirm the file has
a header row plus 65 data rows and opens as valid CSV. Click "Export XLSX",
confirm the file opens (e.g. `libreoffice --calc <path>` or just confirm
`unzip -l <path>` lists `xl/worksheets/sheet1.xml`, proving it's a real
xlsx and not a renamed CSV). Click "Export PDF", confirm the file starts
with `%PDF-` (`head -c 5 <path>`) and, if a PDF viewer is available, that
"CSP Analysis Report" and the table render with visible glyphs (proving the
DejaVu font resolver actually worked, not just that a file was written).

- [ ] **Step 5: Record the outcome**

If every check in Steps 3-4 passes, S10 is done - no further action needed
here. If anything fails, treat it as a bug in whichever earlier task
produced the broken behavior: fix it there, re-run that task's build/test
step, and commit the fix with a message naming what was actually wrong
(not a generic "fix bug" message).

---

## Self-review notes

- **Spec coverage:** table (Task 1, 4, 6), 3 pie charts (Task 4, 6), CSV/
  XLSX/PDF export (Task 5), Refresh (Task 4), Export button wiring (Task 7),
  QuestPDF rejection documented (Global Constraints), Manual Flag stub
  behavior (Task 1's `ResultsBuilder`, verified in Task 9 Step 3), deferred
  bar charts/scatter/manual-override tracked in `SESSIONS.md` (Task 8) - all
  design sections are covered.
- **Placeholder scan:** no TBD/TODO; every code step is complete, runnable
  code, not a description of code.
- **Type consistency:** `ResultRow`'s field names (`Name`, `Dataset`,
  `TotalReadPeaks`, `MinIntensity`, `MaxIntensity`, `PeakDifference`,
  `Probability`, `AutomaticAnalysis`, `ManualFlag`) are identical from
  Task 1 through Task 5 (export code) and Task 6 (XAML bindings).
  `ResultsBuilder.Build`'s signature matches its Task 4 call site exactly.
  `IFilePickerService.PickSaveFileAsync(string, string)` matches its Task 5
  call sites. `IResultsWindowService.Show(ResultsViewModel)` matches Task 7's
  `MainViewModel.OpenResultsWindow`.
