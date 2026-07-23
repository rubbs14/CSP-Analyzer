# Sub-project 3 — Results view (S10)

Status: **design approved, not yet implemented.**

## Problem

`MainViewModel.RunResults` (an `ObservableCollection<SpectrumResult>`, populated
by S9's `RunCommand`) has nowhere to go. The old WinForms app showed results in
a separate popup, `CSPv2/FormOutputTable.cs`: a `DataGridView` table (one row
per experiment plus a reference row) and three `LiveCharts.WinForms.PieChart`
controls (`pieChartAll` "Overview", `pieChartAuto` "Automatic Analysis",
`pieChartManual` "Manual analysis"), opened non-modally by `Form1.cs`'s
`buttonExport_Click`. It also had "To Excel" (Microsoft.Office.Interop.Excel,
Windows-only COM) and "Print" (`System.Drawing.Printing` GDI+, Windows-centric)
buttons.

S10 ports this to Avalonia. It does **not** port `Form1`'s own embedded charts
(`cartesianChart1`/`cartesianChartPeakDiff` peak-diff and probability bar
charts, `cartesianChart2` spectra-overlay scatter) - those are a separate
concern (raw peaklist N/H/intensity data, not `RunResults`) already reserved
as placeholders in `MainWindow.axaml`'s chart zone, and are deferred to S11.

## Scope

**In scope:**
- A new `ResultsWindow` (non-modal Avalonia `Window`), opened from
  `MainWindow`'s existing unwired "Export" button
  (`MainWindow.axaml` bottom bar, current-experiment/player section).
- A results table matching `FormOutputTable.GenerateTable`'s columns: Name,
  Dataset, Total Read Peaks, Min/Max Intensity, Peak Difference to Reference,
  Probability, Automatic Analysis, Manual Flag. Row 0 is always the reference
  spectrum.
- Three pie charts reproducing `piechart`/`piechartauto`/`piechartmanual`'s
  exact series shape (Overview: 5 slices - Actives/Inactives/Manual:NotSet/
  Manual:Actives/Manual:Inactives; Automatic: 2 slices; Manual: 3 slices) plus
  the summary count labels (`labelTotExp`, `labelAutoActives`, etc.).
- Export: CSV (baseline), real `.xlsx` (ClosedXML), and PDF report (PDFsharp) -
  replacing the old Excel-interop and GDI+ print buttons with cross-platform
  equivalents. No print dialog/preview - straight save-to-file.
- A `Refresh` button, mirroring `buttonRefresh_Click`.

**Out of scope (deferred to S11 or later):**
- `Form1`'s embedded peak-diff/probability bar charts and spectra-overlay
  scatter chart.
- Manual override UI (the player-nav buttons + checkboxes that let a user set
  `UserSelection` to `ACTIVE (MAN)`/`INACTIVE (MAN)`/`Not set` per experiment
  in the old app, at `Form1.cs` around the `MAN_ACTIVES`/`MAN_INACTIVES`
  lists). No S-session names this yet; add one to `SESSIONS.md` when this
  design is implemented, so it isn't lost. Until it exists, every
  `PeaklistSpectrum.UserSelection` stays at its default `"Not set"` - the
  Manual Flag column and Manual pie chart show real, correctly-shaped output
  (100% Not-set), it's just that nothing can change it yet. This is real
  behavior, not a stub that needs special-casing: once a future session adds
  the override UI and starts mutating `UserSelection`, S10's table/chart code
  needs no changes to pick it up.

## Data model

New pure-C# join, no Avalonia dependency, added to `dotnet/BackendInterop`
(same reasoning as S8/S9: keep testable business logic out of the
Desktop project, which has no test project referencing it yet):

```csharp
public sealed record ResultRow(
    string Name,               // "Reference" or ExpNumber.ToString()
    string Dataset,
    int TotalReadPeaks,
    double MinIntensity,
    double MaxIntensity,
    int? PeakDifference,       // null for the reference row ("none" in old UI)
    double? Probability,       // null for the reference row
    string? AutomaticAnalysis, // "Active"/"Inactive", null for reference
    string ManualFlag);        // PeaklistSpectrum.UserSelection

public static class ResultsBuilder
{
    public static IReadOnlyList<ResultRow> Build(
        PeaklistSpectrum reference,
        IReadOnlyList<PeaklistSpectrum> datasetSpectra,
        IReadOnlyList<SpectrumResult> runResults);
}
```

`Build` joins `datasetSpectra` and `runResults` by `ExpNumber` (inner join -
a dataset spectrum with no matching run result, which shouldn't happen given
S9's flow, is simply omitted rather than crashing). Peak difference is
`spectrum.TotReadPeaks - reference.TotReadPeaks`, matching
`FormOutputTable.cs:96`.

TDD in `dotnet/BackendInterop.Tests/ResultsBuilderTests.cs`: empty dataset,
single row, reference-only fields, a dataset spectrum with no matching run
result (omitted), peak-difference sign in both directions.

## Desktop components

- **`ResultsViewModel`** (`dotnet/CspAnalyzer.Desktop/ViewModels/`): takes the
  reference spectrum, dataset spectra, and run results at construction time
  (passed in by `MainViewModel` when opening the window - no shared mutable
  state, no back-reference to `MainViewModel`). Exposes:
  - `ObservableCollection<ResultRow> Rows`
  - Summary counts (`TotalExperiments`, `ActivesAuto`, `InactivesAuto`,
    `ActivesManual`, `InactivesManual`, `NotSetManual`)
  - `ISeries[] OverviewSeries`, `AutoSeries`, `ManualSeries`
    (LiveChartsCore.SkiaSharpView types) built from the same counts
  - `RefreshCommand` - re-runs `ResultsBuilder.Build` against the same source
    collections (useful once manual-override mutation exists; a no-op-looking
    but harmless refresh today)
  - `ExportCsvCommand` / `ExportXlsxCommand` / `ExportPdfCommand`

- **`IResultsWindowService`** (`dotnet/CspAnalyzer.Desktop/Services/`,
  mirrors S8's `IFilePickerService` pattern): `Show(ResultsViewModel vm)`.
  Avalonia implementation constructs `new ResultsWindow { DataContext = vm
  }.Show()` (non-modal, matches old `f3.Show()`). Keeps `MainViewModel` from
  constructing `Window`s directly, consistent with the existing service
  abstraction used for file pickers.

- **`IFilePickerService`** gains `PickSaveFileAsync(string suggestedName,
  string extension)` wrapping `IStorageProvider.SaveFilePickerAsync`, needed
  by the three export commands.

- **`MainViewModel`** gains `OpenResultsWindowCommand`
  (`CanExecute: RunResults.Count > 0`), constructs a `ResultsViewModel` from
  `ReferenceSpectrum`/`DatasetSpectra`/`RunResults` and calls
  `IResultsWindowService.Show`. Wires the existing unwired "Export" button in
  `MainWindow.axaml`.

- **`ResultsWindow.axaml`**: `Avalonia.Controls.DataGrid` (new dependency -
  not in Avalonia core) bound to `Rows`, with a style selector coloring
  Active/Inactive rows (green/red, matching
  `dataGridView1_CellFormatting`'s `LightGreen`/`PaleVioletRed`). Three
  `LiveChartsCore.SkiaSharpView.Avalonia.PieChart` controls bound to the
  three `ISeries[]` properties, with the same fill colors as
  `FormOutputTable.cs`'s brushes (`ActiveAutoFill`, `InactiveAutoFill`,
  `ActiveManualFill`, `InactiveManualFill`, `NotSetManualFill`). Summary
  labels. Refresh / Export CSV / Export XLSX / Export PDF buttons.

## Export formats

- **CSV**: `File.WriteAllText`, no dependency, straightforward
  column-per-`ResultRow`-property dump with a header row.
- **XLSX**: ClosedXML (MIT, fully free, no revenue cap - confirmed via its
  published LICENSE) - one worksheet, header row + `Rows`, mirrors the old
  Excel-interop button's cell-by-cell copy but without needing Excel
  installed.
- **PDF**: PDFsharp 6.x (MIT, fully free, targets net8.0/net9.0/net10.0,
  confirmed cross-platform including Linux) - a simple table-drawing report
  (title "CSP Analysis Report", date, then the table, paginated), replacing
  `printDocument1_PrintPage`'s hand-rolled GDI+ pagination logic with
  PDFsharp's drawing API. No print dialog/preview/spooler involved - this is
  "save a PDF," not "print," which fits the old button's actual value (a
  shareable report) without the platform-specific parts.

QuestPDF was considered and rejected: its free tier is capped at $1M annual
revenue (a source-available license, not OSI-approved MIT) - not
unconditionally free the way ClosedXML and PDFsharp are.

## New dependencies

- `LiveChartsCore.SkiaSharpView.Avalonia`
- `Avalonia.Controls.DataGrid`
- `ClosedXML`
- `PDFsharp`

## Testing / verification

- `dotnet/BackendInterop.Tests/ResultsBuilderTests.cs`: real unit tests for
  the join logic (see Data model above).
- Manual verification (same pattern as S7-S9 - no UI automation tool
  available on this box): run the app against the real local
  `CSPv2/Demo-dataset`, complete a real Run, click Export, confirm the table
  and all three pie charts' counts match `RunResults`, click Refresh, export
  CSV/XLSX/PDF and confirm each file is valid and opens correctly, screenshot
  the window.
