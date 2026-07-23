# S12 — Remaining shortcuts + zoom-binding bug fix (polish)

## Context

S11c ported ~20 of legacy `CSPv2/Form1.cs`'s keyboard shortcuts. Nine legacy
shortcuts were left "(not yet implemented)" in `ShortcutsWindow.axaml`,
bundled across the following rows:

1. `Enter, I` — Load Reference/Dataset (Enter), Show Information Window (I)
2. `Ctrl+Alt+O` (as documented, but see bug below) — Show Out-of-Import Range Exp.
3. `Ctrl+Alt+F` — Show Corrupted Peaklist Exp.
4. `Ctrl+I` — Show Auto Inactives
5. `Ctrl+A` — Show Auto Actives
6. `Ctrl+Y` — Reset Zoom to Import limits
7. `Ctrl+Alt+Space` — Reset Zoom for all Graphs
8. `Ctrl+R` — Reset Application

While researching these, a real bug was found in the existing (S11c) binding
table: legacy `Ctrl+C` resets the two bar charts (Peak-Diff, Probability)
and legacy `Ctrl+Y` resets the spectra-overlay chart to the import N/H
range. S11c bound `Ctrl+C` to `ResetOverlayZoomCommand` (the overlay-reset
behavior, i.e. what legacy calls `Ctrl+Y`) and left `Ctrl+Y` unbound. The
`ShortcutsWindow` row text for `Ctrl+C` ("Reset Zoom Bar charts") was
correct all along — only the binding was wrong. This spec's zoom-reset work
fixes that binding alongside adding the two genuinely-missing zoom commands.

Windows is untestable on this dev box (Linux Mint only). Per user decision,
this session verifies on Linux only (real run + screenshot, as prior
sessions did) and does a static code-audit for Windows-specific risk in any
new code (paths, process env, dialogs) — it does not claim Windows-verified.

## Legacy ground truth (`CSPv2/Form1.cs`)

| Gesture | Legacy handler | Behavior |
|---|---|---|
| `Enter` | `Form1.cs:2874-2884` | `load_ref_button.PerformClick()` if reference not yet loaded, else `load_ds_button.PerformClick()` |
| `I` | `Form1.cs:2888-2891` | `details_button.PerformClick()` → opens `Form2` (About dialog) |
| `Ctrl+A` | `Form1.cs:3007-3014` | toggles `checkBoxActives.Checked`, guarded by `.Visible` |
| `Ctrl+I` | `Form1.cs:3017-3024` | toggles `checkBoxInactives.Checked`, guarded by `.Visible` |
| `Ctrl+C` | `Form1.cs:2997-3002` | resets **both bar charts'** zoom (`ZoomResetChartPeakDiff`/`ZoomResetChartProb`) |
| `Ctrl+Y` | `Form1.cs:2986-2989` | resets **overlay chart's** zoom to import N/H range (`Button_ResetZoomToImport`) |
| `Ctrl+Alt+Space` | `Form1.cs:2977-2983` | resets overlay **and** both bar charts together (all three) |
| `Ctrl+Alt+O` | `Form1.cs:3053-3062` | shows `MessageBox` listing exp numbers in `OOR` (peaklist emptied by import-range filter), guarded by `reference_loaded && ds_loaded && OOR.Any()` |
| `Ctrl+Alt+F` | `Form1.cs:3065-3072` | shows `MessageBox` listing `FAULT_EXP` (dataset subfolders **missing** `pdata\1\peaklist.xml` entirely), same guards + `FAULT_EXP.Any()` |
| `Ctrl+R` | `Form1.cs:2744-2758`, `2862-2865` | Yes/No confirm, then `Application.Restart()` |

`FAULT_EXP` is populated at `Form1.cs:1122-1128`: any dataset subfolder
lacking `pdata\1\peaklist.xml` is added directly (no XML parsing attempted).
`OOR` is populated at `Form1.cs:1259`: a folder whose `peaklist.xml` exists
and parses, but whose filtered peak list ends up empty.

## Design

### 1. Zoom commands (`MainViewModel.Charts.cs`)

- **Fix**: `Ctrl+C` binding in `MainWindow.axaml.cs` moves from
  `ResetOverlayZoomCommand` to a new `ResetBarChartZoomCommand`.
- **New** `ResetBarChartZoomCommand`: sets `PeakDiffXAxes[0]` and
  `ProbabilityXAxes[0]`'s `MinLimit`/`MaxLimit` back to `0`/
  `DatasetSpectra.Count` (the values `BuildPeakDiffChart`/
  `BuildProbabilityChart` set at load time). No-op if axes arrays are
  empty (mirrors existing `ResetOverlayZoomCommand` guard style).
- **New** `Ctrl+Y` binding → existing `ResetOverlayZoomCommand` (unchanged
  implementation, just newly bound).
- **New** `ResetAllZoomCommand` (`Ctrl+Alt+Space`): calls
  `ResetOverlayZoom()` then `ResetBarChartZoom()` directly (not via their
  `ICommand` wrappers, to avoid double `CanExecute` evaluation — same
  pattern as any composite command elsewhere in this codebase, none yet
  exists so this establishes it: private methods called directly, each
  wrapped separately by `[RelayCommand]` for its own binding).

All three zoom commands are guarded via `GuardedViewModelCommand` (existing
mechanism) so they don't fire while a sidebar `TextBox` is focused, per
S11c's established pattern for every non-arrow bare/Ctrl binding.

### 2. Auto Actives/Inactives toggle (`MainViewModel.Navigation.cs`)

`IsActivesFilterChecked`/`IsInactivesFilterChecked` already exist as plain
settable `bool` properties (backed by `CurrentFilter`). `KeyBinding`
requires an `ICommand`, so two new trivial `[RelayCommand]` methods:

```csharp
[RelayCommand]
private void ToggleAutoActivesFilter() => IsActivesFilterChecked = !IsActivesFilterChecked;

[RelayCommand]
private void ToggleAutoInactivesFilter() => IsInactivesFilterChecked = !IsInactivesFilterChecked;
```

Legacy guards these on `checkBoxActives.Visible`/`checkBoxInactives.Visible`
(true once a run has classified the dataset — `Form1.cs:1638`/`1654`). This
port's `Actives`/`Inactives` `CheckBox`es (`MainWindow.axaml:307-308`) have
no `IsVisible` binding today — they're always shown, unlike legacy. Rather
than add visibility toggling (out of scope, not requested), `CanExecute`
for the two new toggle commands uses `RunResults.Count > 0` — the same
"classification available" gate `CanOpenResultsWindow` already uses
(`MainViewModel.cs:374`) — as the closest existing equivalent to legacy's
visibility guard.

Bound to `Ctrl+A`/`Ctrl+I` via `GuardedViewModelCommand` (bare-letter
guard doesn't strictly apply to Ctrl-modified gestures the same way, but
using the same helper keeps the wiring uniform with every other command in
this file).

### 3. Corrupted / out-of-range experiment lists

`MainViewModel.LoadDatasetAsync` (`MainViewModel.cs:183-260`) currently
tracks only counts (`CorruptedXmlPeaklistCount`, `OutOfPeakImportRangeCount`).
Two problems to fix here as part of the same change:

- The "missing `peaklist.xml` entirely" case (`!File.Exists(peaklistPath)`,
  line 215) is currently silently skipped — **no counter, no list**. This is
  legacy's `FAULT_EXP` and is what "Corrupted Peaklist Exp." actually means.
- The existing `CorruptedXmlPeaklistCount` (malformed-but-present XML,
  caught via `XmlException`) has no legacy equivalent — legacy doesn't
  attempt recovery there. Folding it into the same "corrupted" bucket as
  missing-file is a deliberate simplification: both mean "this experiment's
  peaklist couldn't be read," and the shortcut's plain-English label covers
  it without needing a third dialog.

New state:

```csharp
public ObservableCollection<string> CorruptedPeaklistExperiments { get; } = new();
public ObservableCollection<string> OutOfImportRangeExperiments { get; } = new();
```

Populated during `LoadDatasetAsync`: missing-file and `XmlException` cases
both add `Path.GetFileName(dir)` to `CorruptedPeaklistExperiments`;
empty-after-filter case adds `spectrum.ExpNumber.ToString()` to
`OutOfImportRangeExperiments` (matches legacy's `OOR` content — exp
numbers, not paths). Existing `CorruptedXmlPeaklistCount`/
`OutOfPeakImportRangeCount` int properties stay as-is (still used
elsewhere for the Analysis Info summary); the new collections are
additive, not a replacement.

Two new commands, guarded on `reference_loaded && ds_loaded` (i.e.
`IsReferenceLoaded && DatasetSpectra.Count > 0` — no `IsDatasetLoaded`
property exists; `CanRun()` at `MainViewModel.cs:268` already uses this
same `DatasetSpectra.Count > 0` idiom for the equivalent legacy
`ds_loaded` check) `&& <list>.Any()`, matching legacy exactly:

```csharp
[RelayCommand(CanExecute = nameof(CanShowCorruptedPeaklistExp))]
private Task ShowCorruptedPeaklistExpAsync() =>
    _infoDialog.ShowAsync("Corrupted Peaklist Experiments",
        string.Join(Environment.NewLine, CorruptedPeaklistExperiments));
```

(and the out-of-range equivalent). `CanExecute` re-evaluated via
`NotifyCanExecuteChanged()` after `LoadDatasetAsync` populates the lists —
same pattern already used for `RunCommand.NotifyCanExecuteChanged()` after
reference load.

Bound to `Ctrl+Alt+F` (corrupted) and `Ctrl+Alt+Y` (out-of-range — legacy's
`Ctrl+Alt+O` is already `ResetAllImportAndThresholdControlsCommand` in this
port, so a free gesture is substituted; `ShortcutsWindow` documents the
port's actual gesture, not the legacy one).

### 4. New `IInfoDialogService`

Mirrors the existing `IConfirmDialogService` pattern exactly
(`Services/IConfirmDialogService.cs`, `AvaloniaConfirmDialogService.cs`,
`NullConfirmDialogService.cs`) but OK-button-only, no return value:

```csharp
public interface IInfoDialogService
{
    Task ShowAsync(string title, string message);
}
```

`AvaloniaInfoDialogService` opens a small window with a `TextBlock`
(wrapped, scrollable if long) and one OK button — same construction
approach as `AvaloniaConfirmDialogService`. `NullInfoDialogService` is a
no-op `Task.CompletedTask`, for design-time/test `MainViewModel`
construction (same role `NullConfirmDialogService` already plays).
`MainViewModel`'s constructor gains one more parameter, consistent with
every prior session's additive constructor pattern (3→5 in S11c, →6 in
S11d, →7 here).

### 5. `Enter` and `I` (About)

- `Enter`, guarded (no textbox focused): calls `LoadReferenceCommand` if
  `!IsReferenceLoaded`, else `LoadDatasetCommand` — matches legacy's
  `load_ds_button.Enabled` check (dataset-load button only enables once
  reference is loaded, so the two conditions are equivalent in this port).
  Implemented as a small new method/command (`LoadReferenceOrDatasetCommand`)
  rather than inline `if` in the key handler, keeping `MainWindow.axaml.cs`
  a thin gesture→command map like every other binding there.
- `I`, guarded: binds directly to the existing `OpenAboutWindowCommand`
  (S11c). No new command needed.

### 6. Reset Application (`Ctrl+R`)

Per user decision: **in-memory reset, not a process relaunch** (no .NET 8
equivalent of WinForms `Application.Restart()` that behaves identically
across a self-contained publish on Windows vs Linux; a full state reset is
user-visibly equivalent and avoids that risk in a polish session).

```csharp
[RelayCommand]
private async Task ResetApplicationAsync()
{
    bool confirmed = await _confirmDialog.ConfirmAsync(
        "Reset Application", "This will clear the loaded reference, dataset, and all results. Continue?");
    if (!confirmed) return;

    ReferenceSpectrum = null;
    DatasetSpectra.Clear();
    RunResults.Clear();
    CurrentFilter = null;
    CorruptedPeaklistExperiments.Clear();
    OutOfImportRangeExperiments.Clear();
    // ... every other field LoadReferenceAsync/LoadDatasetAsync/RunAsync sets, back to construction defaults

    AppSettings settings = await _settingsService.LoadAsync();
    ApplySettings(settings);       // re-applies thresholds/appearance from disk, same as App.axaml.cs startup
}
```

This mirrors what a *real* restart would now actually do, given S11b
persists thresholds/appearance to `settings.json`: a relaunch reloads
saved settings from disk, it does not reset them to hardcoded defaults.
Reusing `ApplySettings`/`SettingsService.LoadAsync` (S11b) for that half
means no new settings logic here, only the data/results clearing is new.

Exact field list finalized during implementation by enumerating everything
`LoadReferenceAsync`/`LoadDatasetAsync`/`RunAsync`/manual-override commands
currently set — this spec establishes the *shape* (confirm → clear
data+results → reapply persisted settings), not a guaranteed-exhaustive
field list.

Bound to `Ctrl+R` (legacy gesture; bare `R` is already `RunCommand`,
`Ctrl+R` is free in this port).

### 7. `ShortcutsWindow.axaml` doc updates

Remove "(not yet implemented)" from all 9 affected rows once wired. No
gesture-label changes needed except the out-of-range row, which must show
this port's actual `Ctrl+Alt+Y` (not legacy's `Ctrl+Alt+O`).

## Testing

TDD as in every prior S3 session (S10b, S11b, S11c, S11d): unit tests per
new command/property in `CspAnalyzer.Desktop.Tests`, covering
`CanExecute` gating (guards + list-non-empty conditions), the
`ResetBarChartZoomCommand`/`ResetAllZoomCommand` axis-value assertions, and
`LoadDatasetAsync`'s new collections populated correctly for
missing-file/malformed-XML/out-of-range cases (three existing test dataset
fixtures already exercise these paths for the count assertions — extend,
don't duplicate).

End-to-end verification: real run against local `CSPv2/Demo-dataset`
(git-ignored, kept locally per S5) with screenshots, as every prior UI
session has done — specifically exercising the two new info dialogs (need
a dataset folder with at least one qualifying corrupted/out-of-range entry;
check whether `Demo-dataset` already has one or a throwaway fixture folder
needs to be added under a git-ignored path for this manual check only).

## Known non-blocking gaps carried forward

Same singleton-window-stacking pattern as `About`/`Shortcuts`/`Help`
(`AvaloniaInfoDialogService` will open a new window per call too) — a
branch-wide pattern predating this session, not introduced or fixed here.
Windows behavior of all S12 additions is code-reviewed for platform risk
but not run-verified (no Windows box available).
