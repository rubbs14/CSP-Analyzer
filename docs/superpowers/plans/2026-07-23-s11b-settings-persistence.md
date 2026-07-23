# S11b Settings Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist Appearance (theme + background color), import-filter thresholds, manual probability threshold, bins-per-array-dimension override, and window geometry across app restarts, replacing today's hardcoded-defaults-every-launch behavior.

**Architecture:** A new `AppSettings` POCO (`dotnet/CspAnalyzer.Desktop/Models/AppSettings.cs`) is loaded/saved as plain JSON by `SettingsService` (`dotnet/CspAnalyzer.Desktop/Services/SettingsService.cs`). `MainViewModel` gains `ApplySettings`/`CurrentSettings` for the filter/threshold fields; `MainWindow` gains `ApplyAppearanceSettings`/`PopulateAppearanceSettings` for theme/color/geometry. `App.axaml.cs` wires it together: load + apply at startup, gather + save on the window's `Closing` event.

**Tech Stack:** .NET 8, Avalonia 11.2.3, CommunityToolkit.Mvvm, `System.Text.Json` (BCL, no new package), xunit + Avalonia.Headless.XUnit for tests.

## Global Constraints

- JSON persistence uses a plain POCO + bare `JsonSerializer.Serialize`/`Deserialize<T>()`, no custom `JsonSerializerOptions` — matches `dotnet/BackendInterop/PeaklistSpectrum.cs` and `SpectrumResult.cs`.
- Settings file path: `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` + `"CspAnalyzer/settings.json"` (via `Path.Combine`), the same `SpecialFolder`-based cross-platform idiom `BackendEnvironment.PythonExecutable` (S11) uses for conda-path probing.
- Missing file, corrupt JSON, or any read/write failure → silently fall back to `new AppSettings()` defaults (which reproduce today's hardcoded literals exactly). Never throw, never crash the app, never show a UI prompt. This codebase has no existing logging framework (verified: no `Console.Error`/`Trace`/`ILogger` usage anywhere) — do not introduce one; a code comment explaining the swallow is sufficient.
- Settings are written to disk only once, in the main window's `Closing` handler. No per-keystroke, per-slider-tick, or per-click writes.
- Out of scope (do not implement): last-used file/folder picker directories, model-dir/python-executable overrides, any new Settings UI/dialog, settings schema versioning or migration.
- `ResetImportControlsCommand`/`ResetPeakFilteringCommand` must keep resetting to their original hardcoded literals (100/140/5/12/5000/2000) regardless of what was loaded from settings — "reset" means reset, not "reload from disk."

---

### Task 1: `AppSettings` model + `SettingsService`

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/Models/AppSettings.cs`
- Create: `dotnet/CspAnalyzer.Desktop/Services/SettingsService.cs`
- Test: `dotnet/CspAnalyzer.Desktop.Tests/AppSettingsTests.cs`
- Test: `dotnet/CspAnalyzer.Desktop.Tests/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `CspAnalyzer.Desktop.Models.AppSettings` (public mutable POCO, all properties listed below, parameterless-constructible with defaults matching today's hardcoded app behavior) and `CspAnalyzer.Desktop.Services.SettingsService` with `SettingsService(string? filePath = null)`, `AppSettings Load()`, `void Save(AppSettings settings)`. Tasks 2-4 depend on both exact type names and the constructor's optional `filePath` parameter (used by tests to avoid touching the real `ApplicationData` folder).

- [ ] **Step 1: Write the failing model defaults test**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/AppSettingsTests.cs
using CspAnalyzer.Desktop.Models;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultConstructor_MatchesTodaysHardcodedAppDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal("System", settings.ThemeVariant);
        Assert.Null(settings.BackgroundColorHex);

        Assert.Equal(1400, settings.WindowWidth);
        Assert.Equal(820, settings.WindowHeight);
        Assert.Null(settings.WindowX);
        Assert.Null(settings.WindowY);
        Assert.Equal("Normal", settings.WindowState);

        Assert.Equal(5000, settings.ReferenceIntensityThreshold);
        Assert.Equal(2000, settings.DatasetIntensityThreshold);
        Assert.Equal(100, settings.NMin);
        Assert.Equal(140, settings.NMax);
        Assert.Equal(5, settings.HMin);
        Assert.Equal(12, settings.HMax);

        Assert.Null(settings.ManualProbabilityThreshold);
        Assert.Null(settings.BinsPerArrayDimension);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter AppSettingsTests`
Expected: FAIL / build error — `AppSettings` does not exist yet.

- [ ] **Step 3: Create `AppSettings`**

```csharp
// dotnet/CspAnalyzer.Desktop/Models/AppSettings.cs
namespace CspAnalyzer.Desktop.Models;

/// <summary>
/// Persisted app state (S11b). Defaults on every property reproduce the
/// hardcoded literals the app used before persistence existed, so
/// "no settings file" and "freshly-defaulted settings" behave identically.
/// </summary>
public class AppSettings
{
    public string ThemeVariant { get; set; } = "System";
    public string? BackgroundColorHex { get; set; }

    public double WindowWidth { get; set; } = 1400;
    public double WindowHeight { get; set; } = 820;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public string WindowState { get; set; } = "Normal";

    public double ReferenceIntensityThreshold { get; set; } = 5000;
    public double DatasetIntensityThreshold { get; set; } = 2000;
    public double NMin { get; set; } = 100;
    public double NMax { get; set; } = 140;
    public double HMin { get; set; } = 5;
    public double HMax { get; set; } = 12;

    public double? ManualProbabilityThreshold { get; set; }
    public int? BinsPerArrayDimension { get; set; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter AppSettingsTests`
Expected: PASS (1/1)

- [ ] **Step 5: Write the failing `SettingsService` tests**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/SettingsServiceTests.cs
using System.IO;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.Services;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class SettingsServiceTests
{
    private static string TempSettingsPath() =>
        Path.Combine(Directory.CreateTempSubdirectory("csp_settings_test_").FullName, "settings.json");

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var service = new SettingsService(TempSettingsPath());

        AppSettings settings = service.Load();

        Assert.Equal(new AppSettings().ThemeVariant, settings.ThemeVariant);
        Assert.Equal(new AppSettings().NMin, settings.NMin);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        string path = TempSettingsPath();
        File.WriteAllText(path, "{ not valid json ][");
        var service = new SettingsService(path);

        AppSettings settings = service.Load();

        Assert.Equal("System", settings.ThemeVariant);
    }

    [Fact]
    public void Save_CreatesParentDirectory_WhenAbsent()
    {
        string path = Path.Combine(Directory.CreateTempSubdirectory("csp_settings_test_").FullName, "nested", "settings.json");
        var service = new SettingsService(path);

        service.Save(new AppSettings());

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        string path = TempSettingsPath();
        var service = new SettingsService(path);
        var original = new AppSettings
        {
            ThemeVariant = "Dark",
            BackgroundColorHex = "#FF1B2A38",
            WindowWidth = 1600,
            WindowHeight = 900,
            WindowX = 50,
            WindowY = 75,
            WindowState = "Maximized",
            ReferenceIntensityThreshold = 4321,
            DatasetIntensityThreshold = 1234,
            NMin = 90,
            NMax = 150,
            HMin = 4,
            HMax = 13,
            ManualProbabilityThreshold = 0.62,
            BinsPerArrayDimension = 32,
        };

        service.Save(original);
        AppSettings loaded = service.Load();

        Assert.Equal(original.ThemeVariant, loaded.ThemeVariant);
        Assert.Equal(original.BackgroundColorHex, loaded.BackgroundColorHex);
        Assert.Equal(original.WindowWidth, loaded.WindowWidth);
        Assert.Equal(original.WindowHeight, loaded.WindowHeight);
        Assert.Equal(original.WindowX, loaded.WindowX);
        Assert.Equal(original.WindowY, loaded.WindowY);
        Assert.Equal(original.WindowState, loaded.WindowState);
        Assert.Equal(original.ReferenceIntensityThreshold, loaded.ReferenceIntensityThreshold);
        Assert.Equal(original.DatasetIntensityThreshold, loaded.DatasetIntensityThreshold);
        Assert.Equal(original.NMin, loaded.NMin);
        Assert.Equal(original.NMax, loaded.NMax);
        Assert.Equal(original.HMin, loaded.HMin);
        Assert.Equal(original.HMax, loaded.HMax);
        Assert.Equal(original.ManualProbabilityThreshold, loaded.ManualProbabilityThreshold);
        Assert.Equal(original.BinsPerArrayDimension, loaded.BinsPerArrayDimension);
    }
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter SettingsServiceTests`
Expected: FAIL / build error — `SettingsService` does not exist yet.

- [ ] **Step 7: Create `SettingsService`**

```csharp
// dotnet/CspAnalyzer.Desktop/Services/SettingsService.cs
using System;
using System.IO;
using System.Text.Json;
using CspAnalyzer.Desktop.Models;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Persists AppSettings as JSON under the OS application-data folder
/// (S11b), following S11's SpecialFolder-based cross-platform idiom
/// (BackendEnvironment.PythonExecutable). Never throws: a missing file,
/// corrupt JSON, or any read/write failure falls back to/silently drops
/// the change rather than crashing the app or a settings-related dialog.
/// No logging framework exists in this codebase (checked - none used
/// anywhere), so failures are swallowed rather than logged.
/// </summary>
public class SettingsService
{
    private readonly string _filePath;

    public SettingsService(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath();
    }

    private static string DefaultFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "CspAnalyzer", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence - losing a settings write on exit isn't fatal.
        }
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter "AppSettingsTests|SettingsServiceTests"`
Expected: PASS (5/5)

- [ ] **Step 9: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Models/AppSettings.cs dotnet/CspAnalyzer.Desktop/Services/SettingsService.cs dotnet/CspAnalyzer.Desktop.Tests/AppSettingsTests.cs dotnet/CspAnalyzer.Desktop.Tests/SettingsServiceTests.cs
git commit -m "S11b: AppSettings model + SettingsService JSON persistence"
```

---

### Task 2: `MainViewModel` settings integration

**Files:**
- Create: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Settings.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs:286` (the `binsPerArrayDimension: null,` line inside `RunAsync`)
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSettingsTests.cs`

**Interfaces:**
- Consumes: `CspAnalyzer.Desktop.Models.AppSettings` (Task 1). Existing `MainViewModel` observable properties `NMin`, `NMax`, `HMin`, `HMax`, `ReferenceIntensityThreshold`, `DatasetIntensityThreshold`, `ManualProbabilityThreshold` (all `double`), and the private field `_manualProbabilityThreshold` (declared in `MainViewModel.Charts.cs`).
- Produces: new observable property `MainViewModel.BinsPerArrayDimension` (`int?`, default `null`), `MainViewModel.CurrentSettings()` (returns a populated `AppSettings` with only the filter/threshold fields set — theme/window fields left at `AppSettings` defaults, since `MainWindow` fills those), and `MainViewModel.ApplySettings(AppSettings settings)` (overwrites the filter/threshold fields from `settings`). Task 4 (`App.axaml.cs`) calls both.

- [ ] **Step 1: Write the failing tests**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSettingsTests.cs
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelSettingsTests
{
    [Fact]
    public void CurrentSettings_ReflectsLiveViewModelState()
    {
        var vm = new MainViewModel
        {
            NMin = 55,
            NMax = 160,
            HMin = 3,
            HMax = 15,
            ReferenceIntensityThreshold = 4000,
            DatasetIntensityThreshold = 1500,
            BinsPerArrayDimension = 24,
        };

        AppSettings settings = vm.CurrentSettings();

        Assert.Equal(55, settings.NMin);
        Assert.Equal(160, settings.NMax);
        Assert.Equal(3, settings.HMin);
        Assert.Equal(15, settings.HMax);
        Assert.Equal(4000, settings.ReferenceIntensityThreshold);
        Assert.Equal(1500, settings.DatasetIntensityThreshold);
        Assert.Equal(24, settings.BinsPerArrayDimension);
    }

    [Fact]
    public void ApplySettings_OverwritesFilterFieldsFromSettings()
    {
        var vm = new MainViewModel();
        var settings = new AppSettings
        {
            NMin = 10,
            NMax = 20,
            HMin = 1,
            HMax = 2,
            ReferenceIntensityThreshold = 999,
            DatasetIntensityThreshold = 888,
            BinsPerArrayDimension = 16,
        };

        vm.ApplySettings(settings);

        Assert.Equal(10, vm.NMin);
        Assert.Equal(20, vm.NMax);
        Assert.Equal(1, vm.HMin);
        Assert.Equal(2, vm.HMax);
        Assert.Equal(999, vm.ReferenceIntensityThreshold);
        Assert.Equal(888, vm.DatasetIntensityThreshold);
        Assert.Equal(16, vm.BinsPerArrayDimension);
    }

    [Fact]
    public void ApplySettings_WithManualProbabilityThreshold_SetsProperty()
    {
        var vm = new MainViewModel();
        var settings = new AppSettings { ManualProbabilityThreshold = 0.72 };

        vm.ApplySettings(settings);

        Assert.Equal(0.72, vm.ManualProbabilityThreshold);
    }

    [Fact]
    public void ApplySettings_WithNullManualProbabilityThreshold_LeavesExistingValueUnchanged()
    {
        var vm = new MainViewModel();
        double before = vm.ManualProbabilityThreshold;
        var settings = new AppSettings { ManualProbabilityThreshold = null };

        vm.ApplySettings(settings);

        Assert.Equal(before, vm.ManualProbabilityThreshold);
    }

    [Fact]
    public void ResetImportControlsCommand_ResetsToHardcodedDefaults_RegardlessOfAppliedSettings()
    {
        var vm = new MainViewModel();
        vm.ApplySettings(new AppSettings { NMin = 1, NMax = 2, HMin = 3, HMax = 4 });

        vm.ResetImportControlsCommand.Execute(null);

        Assert.Equal(100, vm.NMin);
        Assert.Equal(140, vm.NMax);
        Assert.Equal(5, vm.HMin);
        Assert.Equal(12, vm.HMax);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainViewModelSettingsTests`
Expected: FAIL / build error — `BinsPerArrayDimension`, `CurrentSettings`, `ApplySettings` don't exist yet.

- [ ] **Step 3: Add the new partial file**

```csharp
// dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Settings.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CspAnalyzer.Desktop.Models;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// Settings persistence integration (S11b). CurrentSettings/ApplySettings
/// cover only the filter/threshold fields that live on this ViewModel -
/// theme/background/window-geometry are MainWindow's responsibility
/// (ApplyAppearanceSettings/PopulateAppearanceSettings) and get merged
/// into the same AppSettings instance by App.axaml.cs.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    private int? _binsPerArrayDimension;

    public AppSettings CurrentSettings() => new()
    {
        NMin = NMin,
        NMax = NMax,
        HMin = HMin,
        HMax = HMax,
        ReferenceIntensityThreshold = ReferenceIntensityThreshold,
        DatasetIntensityThreshold = DatasetIntensityThreshold,
        ManualProbabilityThreshold = ManualProbabilityThreshold,
        BinsPerArrayDimension = BinsPerArrayDimension,
    };

    public void ApplySettings(AppSettings settings)
    {
        NMin = settings.NMin;
        NMax = settings.NMax;
        HMin = settings.HMin;
        HMax = settings.HMax;
        ReferenceIntensityThreshold = settings.ReferenceIntensityThreshold;
        DatasetIntensityThreshold = settings.DatasetIntensityThreshold;
        BinsPerArrayDimension = settings.BinsPerArrayDimension;

        if (settings.ManualProbabilityThreshold is double threshold)
        {
            // Bypass the property setter, same reason RunAsync does below:
            // OnManualProbabilityThresholdChanged rebuilds charts/gauges
            // that don't exist yet at startup (nothing loaded/run yet).
            _manualProbabilityThreshold = threshold;
            OnPropertyChanged(nameof(ManualProbabilityThreshold));
        }
    }
}
```

- [ ] **Step 4: Wire `BinsPerArrayDimension` into the run call**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, change:

```csharp
                binsPerArrayDimension: null,
```

to:

```csharp
                binsPerArrayDimension: BinsPerArrayDimension,
```

(This has no dedicated test: `BackendCliRunner.RunAsync` is invoked as a direct static call inside `RunAsync` with no seam to intercept the arguments, and no existing test in this project exercises the actual subprocess invocation's arguments. The change is a one-line literal-to-property swap, verifiable by inspection in review.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainViewModelSettingsTests`
Expected: PASS (5/5)

- [ ] **Step 6: Run the full existing MainViewModel test suite to check for regressions**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter "FullyQualifiedName~MainViewModel"`
Expected: PASS, no regressions (all previously-passing tests still pass)

- [ ] **Step 7: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.Settings.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs dotnet/CspAnalyzer.Desktop.Tests/MainViewModelSettingsTests.cs
git commit -m "S11b: MainViewModel.ApplySettings/CurrentSettings + BinsPerArrayDimension"
```

---

### Task 3: `MainWindow` appearance + geometry settings integration

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs`
- Test: `dotnet/CspAnalyzer.Desktop.Tests/MainWindowSettingsTests.cs`

**Interfaces:**
- Consumes: `CspAnalyzer.Desktop.Models.AppSettings` (Task 1).
- Produces: `MainWindow.ApplyAppearanceSettings(AppSettings settings)` (applies theme/background/width/height/position/state to the live window) and `MainWindow.PopulateAppearanceSettings(AppSettings settings)` (writes the window's current theme/background/geometry into the given, already-constructed `AppSettings` instance — mutates in place, does not return a new one). Task 4 calls both; `PopulateAppearanceSettings` is called on the same `AppSettings` object `MainViewModel.CurrentSettings()` already populated with filter fields, so the two calls merge into one object before saving.

- [ ] **Step 1: Write the failing tests**

```csharp
// dotnet/CspAnalyzer.Desktop.Tests/MainWindowSettingsTests.cs
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainWindowSettingsTests
{
    [AvaloniaFact]
    public void ApplyAppearanceSettings_SetsThemeVariant()
    {
        var window = new MainWindow();
        window.Show();

        window.ApplyAppearanceSettings(new AppSettings { ThemeVariant = "Dark" });
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);

        window.ApplyAppearanceSettings(new AppSettings { ThemeVariant = "Light" });
        Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);

        window.ApplyAppearanceSettings(new AppSettings { ThemeVariant = "System" });
        Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void ApplyAppearanceSettings_SetsBackgroundColor_WhenHexProvided()
    {
        var window = new MainWindow();
        window.Show();

        window.ApplyAppearanceSettings(new AppSettings { BackgroundColorHex = "#1B2A38" });

        var brush = Assert.IsType<SolidColorBrush>(window.Background);
        Assert.Equal(Color.Parse("#1B2A38"), brush.Color);
    }

    [AvaloniaFact]
    public void ApplyAppearanceSettings_ClearsBackground_WhenHexIsNull()
    {
        var window = new MainWindow();
        window.Show();
        window.ApplyAppearanceSettings(new AppSettings { BackgroundColorHex = "#1E1E2E" });

        window.ApplyAppearanceSettings(new AppSettings { BackgroundColorHex = null });

        Assert.False(window.Background is SolidColorBrush b && b.Color == Color.Parse("#1E1E2E"));
    }

    [AvaloniaFact]
    public void ApplyAppearanceSettings_SetsWindowSizeAndPosition()
    {
        var window = new MainWindow();
        window.Show();

        window.ApplyAppearanceSettings(new AppSettings { WindowWidth = 1600, WindowHeight = 900, WindowX = 42, WindowY = 84 });

        Assert.Equal(1600, window.Width);
        Assert.Equal(900, window.Height);
        Assert.Equal(new PixelPoint(42, 84), window.Position);
    }

    [AvaloniaFact]
    public void ApplyAppearanceSettings_SetsMaximizedState()
    {
        var window = new MainWindow();
        window.Show();

        window.ApplyAppearanceSettings(new AppSettings { WindowState = "Maximized" });

        Assert.Equal(Avalonia.Controls.WindowState.Maximized, window.WindowState);
    }

    [AvaloniaFact]
    public void PopulateAppearanceSettings_RoundTripsThemeAndColor()
    {
        var window = new MainWindow();
        window.Show();
        window.ApplyAppearanceSettings(new AppSettings { ThemeVariant = "Dark", BackgroundColorHex = "#1B2A38" });

        var gathered = new AppSettings();
        window.PopulateAppearanceSettings(gathered);

        Assert.Equal("Dark", gathered.ThemeVariant);
        var reapplied = new MainWindow();
        reapplied.Show();
        reapplied.ApplyAppearanceSettings(gathered);
        var brush = Assert.IsType<SolidColorBrush>(reapplied.Background);
        Assert.Equal(Color.Parse("#1B2A38"), brush.Color);
    }

    [AvaloniaFact]
    public void PopulateAppearanceSettings_CapturesWindowGeometry()
    {
        var window = new MainWindow();
        window.Show();
        window.ApplyAppearanceSettings(new AppSettings { WindowWidth = 1500, WindowHeight = 850 });

        var gathered = new AppSettings();
        window.PopulateAppearanceSettings(gathered);

        Assert.Equal(1500, gathered.WindowWidth);
        Assert.Equal(850, gathered.WindowHeight);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter MainWindowSettingsTests`
Expected: FAIL / build error — `ApplyAppearanceSettings`/`PopulateAppearanceSettings` don't exist yet.

- [ ] **Step 3: Add the methods to `MainWindow.axaml.cs`**

Add `using CspAnalyzer.Desktop.Models;` to the top of the file, then add these two public methods to the `MainWindow` class (after `OnBackgroundColorResetClick`):

```csharp
    public void ApplyAppearanceSettings(AppSettings settings)
    {
        (Application.Current ?? throw new InvalidOperationException()).RequestedThemeVariant = settings.ThemeVariant switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        if (settings.BackgroundColorHex is string hex)
        {
            Background = new SolidColorBrush(Color.Parse(hex));
        }
        else
        {
            ClearValue(BackgroundProperty);
        }

        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        if (settings.WindowX is int x && settings.WindowY is int y)
        {
            Position = new PixelPoint(x, y);
        }

        WindowState = settings.WindowState == "Maximized"
            ? Avalonia.Controls.WindowState.Maximized
            : Avalonia.Controls.WindowState.Normal;
    }

    public void PopulateAppearanceSettings(AppSettings settings)
    {
        ThemeVariant? current = Application.Current?.RequestedThemeVariant;
        settings.ThemeVariant = current == ThemeVariant.Light ? "Light"
            : current == ThemeVariant.Dark ? "Dark"
            : "System";

        settings.BackgroundColorHex = Background is SolidColorBrush brush ? brush.Color.ToString() : null;

        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
        settings.WindowX = Position.X;
        settings.WindowY = Position.Y;
        settings.WindowState = WindowState == Avalonia.Controls.WindowState.Maximized ? "Maximized" : "Normal";
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter "MainWindowSettingsTests|MainWindowAppearanceTests"`
Expected: PASS, all tests including the pre-existing `MainWindowAppearanceTests` still pass.

- [ ] **Step 5: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/Views/MainWindow.axaml.cs dotnet/CspAnalyzer.Desktop.Tests/MainWindowSettingsTests.cs
git commit -m "S11b: MainWindow.ApplyAppearanceSettings/PopulateAppearanceSettings"
```

---

### Task 4: Wire settings load/apply/save into `App.axaml.cs`

**Files:**
- Modify: `dotnet/CspAnalyzer.Desktop/App.axaml.cs`

**Interfaces:**
- Consumes: `SettingsService` (Task 1), `MainViewModel.ApplySettings`/`CurrentSettings` (Task 2), `MainWindow.ApplyAppearanceSettings`/`PopulateAppearanceSettings` (Task 3).
- Produces: nothing new consumed by later tasks — this is the final integration point.

This task's glue code lives inside `OnFrameworkInitializationCompleted`'s `IClassicDesktopStyleApplicationLifetime` branch, which only runs under the real desktop lifetime — `Avalonia.Headless`-based tests (all `[AvaloniaFact]` tests in this project, via `TestAppBuilder`) never enter that branch, so it cannot get a dedicated automated test without changing the test harness (out of scope). This is a pre-existing gap (the branch has never had a test), not one this task introduces. Verification is a manual run, per the steps below.

- [ ] **Step 1: Modify `OnFrameworkInitializationCompleted`**

Replace the body of `dotnet/CspAnalyzer.Desktop/App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var viewModel = new MainViewModel(
                new AvaloniaFilePickerService(window),
                new AvaloniaResultsWindowService(window),
                new AvaloniaConfirmDialogService(window));
            window.DataContext = viewModel;

            var settingsService = new SettingsService();
            AppSettings settings = settingsService.Load();
            window.ApplyAppearanceSettings(settings);
            viewModel.ApplySettings(settings);

            window.Closing += (_, _) =>
            {
                AppSettings toSave = viewModel.CurrentSettings();
                window.PopulateAppearanceSettings(toSave);
                settingsService.Save(toSave);
            };

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build dotnet/CspAnalyzer.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run the full test suite to confirm no regressions**

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: PASS, all tests (old and new) green.

- [ ] **Step 4: Manual smoke test**

Run: `dotnet run --project dotnet/CspAnalyzer.Desktop`

- Switch theme to Dark, pick a background color swatch, change one import filter value (e.g. NMin), resize/move the window.
- Close the app.
- Re-run `dotnet run --project dotnet/CspAnalyzer.Desktop`.
- Confirm: theme is Dark, background color swatch is applied, the changed import filter value is restored, window size/position match what was left.

- [ ] **Step 5: Commit**

```bash
git add dotnet/CspAnalyzer.Desktop/App.axaml.cs
git commit -m "S11b: wire settings load/apply/save into App startup and window Closing"
```
