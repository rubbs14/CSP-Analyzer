# S11b — Settings Persistence Design

## Context

`dotnet/CspAnalyzer.Desktop` has accumulated several pieces of UI state that
reset to hardcoded defaults on every launch:

- **Appearance** (added in S10b): light/dark/system theme and a background
  color swatch, both implemented purely in `MainWindow.axaml.cs` code-behind
  (`OnThemeLightClick`/`OnThemeDarkClick`/`OnThemeSystemClick`,
  `OnBackgroundColorClick`/`OnBackgroundColorResetClick`) — no ViewModel
  property, no storage. Covered today by
  `CspAnalyzer.Desktop.Tests/MainWindowAppearanceTests.cs`.
- **Import filter thresholds** (from S8/S9): `MainViewModel` fields
  `_referenceIntensityThreshold` (default 5000), `_datasetIntensityThreshold`
  (default 2000), `NMin`/`NMax` (100/140), `HMin`/`HMax` (5/12) — hardcoded
  literals, restored by `ResetFilters` to the same literals.
- **Manual probability threshold** (S10b): `ManualProbabilityThreshold`,
  `null` by default (auto-computed via `ComputeAutoProbabilityThreshold()`).
- **Bins-per-array-dimension override**: currently always passed as `null`
  (no user-facing override exists yet).
- **Window geometry**: `MainWindow.axaml` hardcodes `Width="1400"
  Height="820"`, no position/maximized-state memory.

SESSIONS.md scopes S11b as "Settings persistence (incl. S10b's Appearance
theme/color choices, currently in-memory only)." This design extends that
scope to the other in-memory-only settings above, per user decision during
brainstorming, but explicitly excludes last-used file/folder paths and any
model-dir/python-executable override — those remain out of scope for this
session.

S11 already established the cross-platform path idiom this design reuses:
`BackendEnvironment`'s conda-path probing uses
`Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)` combined
with `RuntimeInformation.IsOSPlatform(...)` branching for Windows/macOS/Linux.

## Goals

- Persist across app restarts: appearance (theme + background color), the
  six import-filter values, `ManualProbabilityThreshold`,
  `BinsPerArrayDimension` override, and window size/position/maximized state.
- Reuse existing patterns: plain `System.Text.Json` POCO + bare
  `JsonSerializer.Serialize`/`Deserialize<T>()`, no custom
  `JsonSerializerOptions` — matching `BackendInterop`'s
  `PeaklistSpectrum`/`SpectrumResult`.
- Missing, corrupt, or old-schema settings file → silently fall back to
  today's hardcoded defaults (log a diagnostic, never crash, never prompt).
- Single save point on app exit — no per-keystroke/per-slider-tick writes.

## Non-goals

- Last-used file/folder picker directories.
- Model-dir / python-executable user overrides.
- Settings UI beyond what already exists (no new "Settings" window/dialog).
- Migrating/versioning the settings schema (a schema-mismatch is just
  treated as "corrupt" → defaults; no upgrade path needed yet).

## Architecture

New files under `dotnet/CspAnalyzer.Desktop/Services/`:

### `AppSettings.cs`

Plain POCO, default-constructible with today's hardcoded values as its
defaults (so `new AppSettings()` alone reproduces current app behavior):

```csharp
public class AppSettings
{
    public string ThemeVariant { get; set; } = "System"; // "Light" | "Dark" | "System"
    public string? BackgroundColorHex { get; set; }       // null = no override

    public double WindowWidth { get; set; } = 1400;
    public double WindowHeight { get; set; } = 820;
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public string WindowState { get; set; } = "Normal";   // "Normal" | "Maximized"

    public double ReferenceIntensityThreshold { get; set; } = 5000;
    public double DatasetIntensityThreshold { get; set; } = 2000;
    public int NMin { get; set; } = 100;
    public int NMax { get; set; } = 140;
    public int HMin { get; set; } = 5;
    public int HMax { get; set; } = 12;

    public double? ManualProbabilityThreshold { get; set; }
    public int? BinsPerArrayDimension { get; set; }
}
```

### `SettingsService.cs`

```csharp
public class SettingsService
{
    public static string SettingsFilePath { get; } // computed once, see below
    public AppSettings Load();          // never throws; defaults on any failure
    public void Save(AppSettings settings); // catches + logs, never throws
}
```

- Path resolution mirrors `BackendEnvironment`'s S11 idiom:
  `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`
  joined with `"CspAnalyzer/settings.json"` (forward-slash segments via
  `Path.Combine`, not string concat), no per-OS branching needed since
  `SpecialFolder.ApplicationData` already resolves correctly per-platform
  (`%AppData%` on Windows, `~/.config` on Linux, `~/Library/Application
  Support` on macOS under .NET's mapping).
- `Load()`: if file missing → `new AppSettings()`. If present, read +
  `JsonSerializer.Deserialize<AppSettings>()` inside try/catch
  (`JsonException`, `IOException`, `UnauthorizedAccessException`) → on any
  exception, log via existing diagnostic logging convention and return
  `new AppSettings()`.
- `Save()`: `Directory.CreateDirectory` on the parent dir (handles
  first-run, no existing `CspAnalyzer/` folder), then
  `JsonSerializer.Serialize` + file write inside try/catch, log on failure,
  never propagate.

## Data flow

**Startup** (`MainWindow` constructor, after `InitializeComponent()`):

1. `var settings = new SettingsService().Load();`
2. Apply theme: call the same logic `OnThemeLightClick`/etc. invoke
   (`Application.Current.RequestedThemeVariant = ...`) directly from
   `settings.ThemeVariant`, no synthetic click event.
3. Apply background: if `settings.BackgroundColorHex != null`, same path as
   `OnBackgroundColorClick`'s brush assignment; else leave default (mirrors
   `OnBackgroundColorResetClick`).
4. Apply window geometry: set `Width`/`Height`/`Position` from
   `WindowWidth/Height/X/Y`; if `X`/`Y` are null (first run), let Avalonia's
   default placement stand. Set `WindowState` last (so `Normal` dimensions
   are already applied before maximizing).
5. Call `MainViewModel.ApplySettings(settings)` — new method that sets the
   existing private filter-threshold fields (`_referenceIntensityThreshold`,
   `NMin`, `NMax`, `HMin`, `HMax`, `_datasetIntensityThreshold`,
   `ManualProbabilityThreshold`, and the bins-per-array-dimension field used
   at [MainViewModel.cs:286](../../../dotnet/CspAnalyzer.Desktop/MainViewModel.cs)
   in place of the current hardcoded `null`) instead of the constructor's
   hardcoded literals. `ResetFilters` is unaffected — it continues to reset
   to the original hardcoded literals, not the loaded settings (reset means
   reset, not "reload from disk").

**Shutdown** (`MainWindow`, subscribe to `Closing` event):

1. Build a fresh `AppSettings` from current live state: theme from
   `Application.Current.RequestedThemeVariant`, background from the
   `Window.Background` brush (null if default/cleared), current
   `Width`/`Height`/`Position`/`WindowState`, and the ViewModel's current
   filter/threshold/bins values (new `MainViewModel.CurrentSettings` getter
   mirroring `ApplySettings`'s field list).
2. `new SettingsService().Save(built)`.

## Error handling

- Load: any failure (missing file, malformed JSON, wrong types, permission
  denied) → `new AppSettings()`, one log line, app proceeds exactly as it
  does today with no settings file at all. No user-facing dialog.
- Save: any failure (read-only directory, disk full, permission denied) →
  one log line, swallow, allow shutdown to proceed. Losing a settings write
  on exit is not fatal to the session that's ending.
- No schema versioning: a future field rename/removal that breaks
  deserialization is indistinguishable from "corrupt" and falls back to
  defaults. Acceptable for this app's low-stakes settings.

## Testing

`CspAnalyzer.Desktop.Tests`:

- `SettingsServiceTests.cs`: round-trip save→load equality; missing file →
  defaults; malformed JSON on disk → defaults (no throw); `Save` creates the
  parent directory when absent; `Save` followed by `Load` on a real temp
  path (inject/override the settings path for test isolation rather than
  touching the real `ApplicationData` location).
- `AppSettingsTests.cs`: `new AppSettings()` matches today's hardcoded
  defaults (locks in the "no settings file = current behavior" guarantee).
- `MainViewModelSettingsTests.cs`: `ApplySettings` overwrites the filter
  fields; `CurrentSettings` reflects live ViewModel state; `ResetFilters`
  still resets to the original hardcoded literals regardless of what was
  loaded.
- Extend `MainWindowAppearanceTests.cs`: loading settings with a given
  `ThemeVariant`/`BackgroundColorHex` applies them at startup the same way
  the existing click handlers would.

No new manual/GUI verification pass beyond what these unit tests cover —
consistent with S10b's note that this box has no GUI automation tool
(`xdotool`); a manual run + restart to visually confirm persistence remains
a nice-to-have but not a blocking verification step.
