# S10b — Form1's embedded charts + manual-override workflow (design)

Sub-project 3 (Avalonia UI port), session S10b. Continues from S10 (which built
the separate `ResultsWindow` export view). This session ports the parts of
`CSPv2/Form1.cs` that S10 explicitly deferred: the three charts embedded in
the *main* window (peak-difference bar chart, probability bar chart,
spectra-overlay scatter) and the manual-override workflow (mark
active/inactive, player navigation, actives/inactives filtering) that gives
those charts and `ResultsWindow`'s Manual Flag column real data instead of
always showing 100% "Not set".

## Current state (as of S9/S10)

- `MainWindow.axaml`'s chart zone and bottom bar are still placeholder
  `TextBlock`s (see the `(S10)`/`(S9)` markers in the current XAML).
- `MainViewModel` has `ReferenceSpectrum`, `DatasetSpectra`
  (`ObservableCollection<PeaklistSpectrum>`), and `RunResults`
  (`ObservableCollection<SpectrumResult>`), populated by S8's load flow and
  S9's run flow. No navigation state, no filter state, no manual-override
  commands exist yet.
- `PeaklistSpectrum.UserSelection` (default `"Not set"`) already exists and
  already flows through `ResultsBuilder.Build` into `ResultRow.ManualFlag`,
  which `ResultsWindow`'s table/pie charts already read. **No new plumbing
  is needed for `ResultsWindow` to show real data** — it just needs
  `UserSelection` to actually get mutated somewhere, which this session
  provides.
- No test project references `CspAnalyzer.Desktop` yet (noted as a gap in
  S7/S8/S9 session notes).

## Scope

1. Sort `DatasetSpectra` by `ExpNumber` after load (small fix to S8's
   `LoadDatasetAsync` — currently unsorted, but chart bar index / player
   `CurrentIndex` / experiment-number labels all need a stable index↔
   experiment mapping, same as legacy's `VALID_DS_SPECTRA.Sort(...)`).
2. Player navigation: `CurrentIndex`, First/Previous/Next/Last commands,
   Go-To-Experiment.
3. Actives/Inactives filter: single `ExperimentFilter?` state (replaces
   legacy's two independently-mutable-but-manually-exclusive bools).
4. Manual-override commands: mark active / mark inactive / reset one /
   reset all (with confirmation).
5. Peak-difference bar chart (buildable at dataset-load time, no run
   needed).
6. Probability bar chart (buildable after a run).
7. Spectra-overlay scatter chart (reference + current experiment + filtered
   overlay).
8. Actives/Inactives radial gauges.
9. Manual-results bar chart (Act/Inact/Not-set (man) counts).
10. `CspAnalyzer.Desktop.Tests` xunit project, TDD for all of the above
    view-model logic that doesn't require a live Avalonia window.

Out of scope for this session (unchanged placeholders / future sessions):
Help/Details/Shortcuts sidebar buttons (S11), python/env path handling
(S11), cross-platform polish (S12).

## Design

### 1. Navigation + filter state (`MainViewModel`)

```csharp
public enum ExperimentFilter { Actives, Inactives }

[ObservableProperty] private ExperimentFilter? _currentFilter; // null = show all
[ObservableProperty] private int _currentIndex;
```

`CurrentView` is a computed `IReadOnlyList<PeaklistSpectrum>` (or a small
record pairing spectrum + result) derived from `DatasetSpectra`,
`RunResults`, and `CurrentFilter`:

- `CurrentFilter == null` → all of `DatasetSpectra`.
- `CurrentFilter == Actives` → spectra whose matching `RunResults` entry has
  `IsActive == true`.
- `CurrentFilter == Inactives` → the complement.

This single nullable enum replaces legacy's `ShowActives`/`ShowInactives`
bool pair, which required each checkbox's handler to manually uncheck the
other and left dead "both true" branches in `update_graphs`/`update_player`
that could never actually be reached through the UI. Two checkbox-styled
toggle buttons in the view set `CurrentFilter` (checking one clears/replaces
the other; unchecking either sets it back to `null`), preserving the
original's look without the hand-rolled exclusion logic.

Filter toggles are only enabled once `RunResults.Count > 0` (mirrors
legacy's `checkBoxActives.Visible = true` happening only post-run) — before
a run, navigation always covers the full dataset.

**Player commands** — bounds are enforced by `CanExecute`, not by a
post-hoc `update_player()` pass that disables buttons after the fact (the
legacy pattern, which only works because nothing else can invoke
`ButtonNEXT_Click` while it's disabled — brittle if any other code path
ever calls the handler directly). `First`/`Previous`/`Next`/`Last` clamp
against `CurrentView.Count`; switching `CurrentFilter` resets `CurrentIndex`
to `0` (with a not-found-in-new-view guard, since the current spectrum may
not be in the new filtered view).

`GoToExperimentCommand(int expNumber)` searches `CurrentView` for a matching
`ExpNumber`; not-found sets a status string (`GoToStatusText`) instead of a
WinForms `MessageBox.Show`.

**Derived display properties** (recomputed whenever `CurrentIndex` or
`CurrentView` changes): `CurrentExperimentNumber`, `CurrentCounterText`
(`"n / max"`), `CurrentPeakDifference`, `CurrentManualStatusText` +
color, `CurrentAutomaticStatusText` + color (only meaningful once
`RunResults` is populated).

### 2. Manual-override commands

```csharp
[RelayCommand] private void MarkActive()    // CurrentSpectrum.UserSelection = "ACTIVE (MAN)"
[RelayCommand] private void MarkInactive()  // CurrentSpectrum.UserSelection = "INACTIVE (MAN)"
[RelayCommand] private void ResetManualStatus() // → "Not set"
[RelayCommand] private async Task ResetAllManualFlags() // confirm, then reset every spectrum
```

`CurrentSpectrum` here means whatever `CurrentView[CurrentIndex]` resolves
to. Every command recomputes the manual-results bar chart's three counts
(`ActivesManualCount`/`InactivesManualCount`/`NotSetManualCount`) from
`DatasetSpectra` after mutating.

`ResetAllManualFlagsCommand` needs a Yes/No confirmation (legacy: WinForms
`MessageBox.Show` with a warning icon). Avalonia has no built-in
`MessageBox`, and per your call this gets a small hand-rolled
`IConfirmDialogService` (mirrors the existing `IFilePickerService` /
`IResultsWindowService` pattern already in `Services/`) rather than a new
NuGet dependency — a minimal Avalonia `Window` with the warning text and
Yes/No buttons, plus a `NullConfirmDialogService` (always confirms) for the
`Design.DataContext` / headless tests.

### 3. Charts (LiveChartsCore.SkiaSharpView.Avalonia — same library S10 already introduced)

**Peak-difference bar chart** — one `ColumnSeries` over
`DatasetSpectra[i].TotReadPeaks - ReferenceSpectrum.TotReadPeaks`, buildable
immediately after `LoadDatasetAsync` (no run dependency, matches legacy
computing `PEAK_DIFF` at dataset-load time). Full fidelity per your choice:
- Y-axis threshold zones at ±25/±40/±80 (Fine/Check/Broken bands, same
  colors as `ResultsWindow`'s existing `SolidColorPaint` palette).
- Floating text annotations ("Safe range", "Check PP", "Broken Spectrum").
- Bar fill colored red when `|diff| > 40`.
- A marker element tracking `CurrentIndex`.

**Probability bar chart** — one `ColumnSeries` over
`RunResults[i].ActivePseudoprobability`, built once `RunResults` is
populated. Threshold zones at 0/0.35/0.75, bars colored by `ProbThreshold`
constant (same value used for `IsActive` classification), same
current-index marker.

Both bar charts: **X-axis zoom stays synced** (panning/zooming one updates
the other's `MinValue`/`MaxValue` — a `RangeChanged` handler, port of
`Axis_RangeChanged`), and **clicking a bar sets `CurrentIndex`** (port of
`ChartOnDataClick`/`ChartPeakDiff_OnDataClick`, using LiveChartsCore's
`ChartPointerDown`/`DataPointerDown` events since the WPF-era `OnDataClick`
API doesn't exist in SkiaSharpView).

**Spectra-overlay scatter chart** — axes titled "1H ppm"/"15N ppm", ranges
from `NMin/NMax/HMin/HMax`, values inverted (`-1 * ppm`) same as legacy.
Series:
- Reference (static, set once `ReferenceSpectrum` loads).
- Current experiment (`ScatterPoint` per peak in `CurrentSpectrum.Peaklist`,
  redrawn on every navigation/filter change).
- Actives overlay / Inactives overlay (only populated when `CurrentFilter`
  is set to the matching value — same conditional-clear/repopulate
  structure as legacy's `update_graphs`, simplified since there's no more
  "both true" branch to handle).

Zoom controls: **Reset zoom** (back to `NMin/NMax/HMin/HMax` import
bounds) and **Fit zoom to reference** (bounds computed from
`ReferenceSpectrum.Peaklist`'s actual F1/F2 range, ± the same padding
legacy used) — both added per your last answer, small button pair next to
the chart.

**Actives/Inactives gauges** — `RadialGaugeSeries` via `GaugeBuilder`
(LiveChartsCore's gauge API, not a WPF `SolidGauge` port — different
control, closest visual equivalent available), populated post-run from
`RunResults.Count(r => r.IsActive)` / the complement, range `0..DatasetSpectra.Count`.

### 4. Testing

New `dotnet/CspAnalyzer.Desktop.Tests` xunit project. Everything in
sections 1–2 (navigation bounds, filter switching, manual-override state
transitions, go-to-experiment found/not-found) is plain `MainViewModel`
logic with no Avalonia window dependency — testable the same way
`BackendInterop.Tests` already tests `ResultsBuilder`. Chart/gauge wiring
(section 3) is exercised the same way S7–S9 verified UI: a manual
`DISPLAY=:0 dotnet run` + screenshot pass, since there's still no GUI
automation tool on this box (per S9's notes).

## Data flow summary

```
LoadDatasetAsync (S8, fixed to sort by ExpNumber)
  → DatasetSpectra populated
  → Peak-diff chart buildable (needs Reference + DatasetSpectra only)
  → Navigation usable (CurrentFilter forced to null pre-run)

RunAsync (S9) completes
  → RunResults populated
  → Probability chart buildable
  → Actives/Inactives gauges buildable
  → Filter toggles become enabled

Mark Active / Mark Inactive / Reset (this session)
  → PeaklistSpectrum.UserSelection mutated
  → Manual-results bar chart recomputed
  → (already wired) ResultsWindow's Manual Flag column/pie shows real data
    when Export is clicked afterward
```
