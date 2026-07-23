# S11c — Keyboard Shortcuts, Shortcuts Window, About Window Design

## Context

SESSIONS.md originally scoped S11c as "Secondary windows: Help, Shortcuts
(ported from `CSPv2/FormHelp`/`FormShortcuts`)." Auditing the legacy source
during brainstorming found the Shortcuts window is a *reference table for
real keyboard shortcuts* — and this app has zero `KeyBinding`/`KeyGesture`
usage anywhere today (verified by grep). Porting the window as static text
without the shortcuts it describes actually working would ship documentation
for a feature that doesn't exist. Per user decision, this session's scope
grew to: wire the real subset of legacy shortcuts that map to commands this
app already has, build a Shortcuts window that documents both the wired
ones and the ones that don't map to anything yet, and (since it's another
inert sidebar button in the same row) wire up a minimal About window.

**The Help window (with its Q&A content and the interactive TopSpin
command-generator tool) is deferred to a separate S11c-follow-up session**
(tentatively "S11d" in SESSIONS.md) — it's a large, mostly-independent
chunk of work (a real mini-tool: 6 inputs, Generate/Copy/Reset, string
building) and doesn't depend on anything in this session.

## Goals

- Real `Window.KeyBindings` wired on `MainWindow` and `ResultsWindow` for
  every legacy shortcut that maps unambiguously to a command this app
  already has (list in full below).
- A new modeless `ShortcutsWindow` (opened via the sidebar's existing
  inert "Shortcuts" button, and via Ctrl+K) listing every legacy shortcut,
  grouped the same way legacy grouped them, with the subset that has no
  real feature yet visually marked "Not yet implemented" rather than
  wired to nothing or silently dropped.
- A new modeless `AboutWindow` (opened via the sidebar's existing inert
  "About" button): app name, the existing "Developed by R. Byrne and
  R. Fino" string (reused from `MainWindow.axaml`'s footer), assembly
  version.
- One small, faithful gap-fill: legacy had a third, distinct "reset
  everything" shortcut (Ctrl+Alt+O, "Reset All Imp.Controls to Default")
  beyond the two individual resets (`ResetImportControls`,
  `ResetPeakFiltering`) already ported in S8/S9 — add the composite
  command so this shortcut has something real to bind to.

## Non-goals

- The Help window, its Q&A content, and the TopSpin command generator —
  separate session.
- Any legacy shortcut with no existing corresponding command/feature —
  listed as "Not yet implemented" in the Shortcuts window, not invented.
- "Load Reference/Dataset = Enter" — legacy's Enter was context-dependent
  on which textbox had focus; this app's loads are plain buttons with no
  equivalent context to resolve. Not portable without inventing a new
  composite feature; listed as unmapped.
- `ResultsWindow`'s Print/Export/Refresh shortcuts already are the correct
  scope for this session (legacy scoped them to `FormOutputTable`, which
  `ResultsWindow` replaces) — but no *other* `ResultsWindow`-only features
  beyond wiring those three existing commands to keys.

## Full shortcut mapping

Legend: **Wired** = real `KeyBinding` added this session. **N/I** = listed
in `ShortcutsWindow` as "Not yet implemented," no binding added.

### MainWindow

| Key | Legacy action | Status | Binds to |
|---|---|---|---|
| Enter | Load Reference/Dataset | N/I | — (context-dependent in legacy, unmappable) |
| R | Run CSP Analysis | **Wired** | `RunCommand` |
| Ctrl+K | Show Keyboard Shortcut Window | **Wired** | `OpenShortcutsWindowCommand` (new) |
| H | Show Help Guide | N/I | — (Help window is S11d) |
| I | Show Information Window | N/I | — (no such window exists) |
| Right | Next Spectrum | **Wired** | `NextCommand` |
| Left | Previous Spectrum | **Wired** | `PreviousCommand` |
| Down | Last Spectrum | **Wired** | `LastCommand` (legacy's own mapping, kept as-is) |
| Up | First Spectrum | **Wired** | `FirstCommand` (legacy's own mapping, kept as-is) |
| Ctrl+Alt+R | Show Reference PP info | **Wired** | `ShowReferencePpDetailsCommand` |
| Ctrl+Alt+E | Show Current Exp. PP info | **Wired** | `ShowExperimentPpDetailsCommand` |
| Ctrl+Alt+O | Show Out-of-Import Range Exp. | N/I | — (gesture collision with the mapped action below; that one wins since it's implementable) |
| Ctrl+Alt+F | Show Corrupted Peaklist Exp. | N/I | — (no such list view exists) |
| Ctrl+I | Show Auto Inactives | N/I | — (no such toggle command exists) |
| Ctrl+A | Show Auto Actives | N/I | — (no such toggle command exists) |
| Ctrl+C | Reset Zoom Bar charts | **Wired** | `ResetOverlayZoomCommand` |
| Ctrl+X | Fit Zoom to Reference | **Wired** | `FitOverlayZoomToReferenceCommand` |
| Ctrl+Y | Reset Zoom to Import limits | N/I | — (no distinct 3rd zoom command exists) |
| Ctrl+Alt+Space | Reset Zoom for all Graphs | N/I | — (no distinct 4th zoom command exists) |
| T | Abort CSP Analysis | **Wired** | `CancelRunCommand` |
| Ctrl+R | Reset Application | N/I | — (no global-reset command exists) |
| Ctrl+Q | Close Selected Window/Quit | **Wired** | code-behind `RelayCommand(Close)`, no ViewModel involved |
| N | Reset All Manual Flags | **Wired** | `ResetAllManualFlagsCommand` |
| D | Reset Manual Flag | **Wired** | `ResetManualStatusCommand` |
| S | Mark as Inactive | **Wired** | `MarkInactiveCommand` |
| A | Mark as Active | **Wired** | `MarkActiveCommand` |
| Ctrl+Alt+I | Reset Import Limits | **Wired** | `ResetImportControlsCommand` |
| Ctrl+Alt+T | Reset Intensity Thresholds | **Wired** | `ResetPeakFilteringCommand` |
| Ctrl+Alt+O | Reset All Imp.Controls to Default | **Wired** | `ResetAllImportAndThresholdControlsCommand` (new, see Goals) |
| G | Select Go To Experiment Textbox | **Wired** | code-behind `RelayCommand` calling `GoToExperimentTextBox.Focus()` directly, no ViewModel involved |

### ResultsWindow

| Key | Legacy action | Status | Binds to |
|---|---|---|---|
| Ctrl+E | Export To Excel | **Wired** | `ExportXlsxAsyncCommand` |
| Ctrl+P | Print | **Wired** | `ExportPdfAsyncCommand` (PDF export already replaced GDI+ printing in S10) |
| R | Refresh | **Wired** | `RefreshCommand` |
| Ctrl+Q | Close Selected Window/Quit | **Wired** | code-behind `RelayCommand(Close)` |

(The legacy table's duplicate "Ctrl+E" for a second "Export Data" label
and duplicate "R" across two forms are both accounted for above — one
`Ctrl+E`/`R` pair on `MainWindow`'s own scope doesn't exist since Run
already owns bare `R` there and Export lives only on `ResultsWindow`;
Avalonia's per-`Window` `KeyBindings` collections don't collide across
windows since only the focused window's bindings are active.)

## Architecture

### New commands (`MainViewModel`)

- `OpenAboutWindowCommand` (plain `[RelayCommand]`, no `CanExecute`) —
  calls `_aboutWindowService.Show()`.
- `OpenShortcutsWindowCommand` (plain `[RelayCommand]`) — calls
  `_shortcutsWindowService.Show()`.
- `ResetAllImportAndThresholdControlsCommand` (plain `[RelayCommand]`) —
  calls `ResetImportControls()` then `ResetPeakFiltering()`.

New partial file `MainViewModel.SecondaryWindows.cs` holds the two
`Open*WindowCommand`s and the two new injected service fields
(`_aboutWindowService`, `_shortcutsWindowService`), following the existing
`_confirmDialogService`/`_resultsWindowService` constructor-injection
pattern — `MainViewModel`'s constructor gains two more parameters, with
matching `Null*` defaults on the parameterless constructor.
`ResetAllImportAndThresholdControlsCommand` goes in the existing
`MainViewModel.cs` next to `ResetImportControls`/`ResetPeakFiltering`
(same file they already live in).

### New services (mirroring `IConfirmDialogService`/`IResultsWindowService`)

- `IAboutWindowService { void Show(); }` /
  `AvaloniaAboutWindowService(Window owner)` (constructs
  `new AboutWindow(); window.Show(owner);`) / `NullAboutWindowService`
  (no-op).
- `IShortcutsWindowService { void Show(); }` /
  `AvaloniaShortcutsWindowService(Window owner)` / `NullShortcutsWindowService`.

### New views

- `AboutWindow.axaml`/`.axaml.cs`: static content, no ViewModel needed
  (`DataContext` untouched) — app title, the exact string `"Developed by
  R. Byrne and R. Fino"` (copied from `MainWindow.axaml`'s existing
  footer `TextBlock`), and
  `typeof(AboutWindow).Assembly.GetName().Version` rendered as text.
- `ShortcutsWindow.axaml`/`.axaml.cs`: static content, no ViewModel
  needed — the full mapping table above, grouped into the same category
  headers legacy used (Loading Data/Processing, Player, Reference/Dataset
  info, Spectra Overlay, Zoom/Import Control, Abort/Reset, Manual Flag
  Control, Export Data Window, Import/Threshold controls, plus the
  standalone "G" row), each row showing the key-gesture text + action
  label, with N/I rows visually distinguished (e.g. dimmed text +
  `"(not yet implemented)"` suffix) from **Wired** rows.

### `MainWindow.axaml` changes

- Add `x:Name="GoToExperimentTextBox"` to the existing Go-To-Experiment
  `TextBox` (`MainWindow.axaml:296`) so code-behind can call `.Focus()`.
- Wire the two existing inert buttons:
  `<Button Content="About" Command="{Binding OpenAboutWindowCommand}" />`,
  `<Button Content="Shortcuts" Command="{Binding OpenShortcutsWindowCommand}" />`
  (`H`elp button stays inert — its window is S11d).
- Add a `<Window.KeyBindings>` block with one `<KeyBinding Gesture="..."
  Command="{Binding ...Command}" />` per **Wired** MainWindow row above
  (all `{Binding}` — resolved against `DataContext` at binding-evaluation
  time, so it works even though `DataContext` is assigned after the
  constructor runs, same as every other binding in this file already
  relies on).
- Ctrl+Q and G don't bind to `{Binding}` commands (they're View concerns,
  not ViewModel state) — code-behind (`MainWindow.axaml.cs`) exposes two
  private `RelayCommand` fields (`CommunityToolkit.Mvvm.Input.RelayCommand`,
  already referenced by the project) constructed in the constructor:
  `_closeCommand = new RelayCommand(Close);` and
  `_focusGoToExperimentCommand = new RelayCommand(() =>
  GoToExperimentTextBox.Focus());`, bound via `x:Name` +
  `Command="{Binding #window.CloseCommand}"`-style — concretely: expose
  them as public `ICommand` properties (`CloseCommand`,
  `FocusGoToExperimentCommand`) on the `MainWindow` class itself, and bind
  the `KeyBinding`s with `Command="{Binding CloseCommand,
  RelativeSource={RelativeSource Self}}"` (binding to the `Window`
  instance's own property, not `DataContext`).

### Input-focus caveat

Several **Wired** MainWindow gestures are bare, unmodified letters (`R`,
`T`, `N`, `D`, `S`, `A`, `G`). If one of the sidebar's plain-text `TextBox`
controls (e.g. the `NMin`/`NMax`/`HMin`/`HMax`/threshold inputs) has
keyboard focus, a `Window`-level `KeyBinding` on a bare letter must not
fire while the user is typing normal text into that box — Avalonia's
`TextBox` should consume the routed `KeyDown` for character input before
it bubbles to `Window.KeyBindings`, but this codebase has never had a
bare-letter `KeyBinding` before, so this has never been exercised. This
is a real correctness risk, not a hypothetical: verify it explicitly
during implementation (test: focus the `NMin` `TextBox`, `KeyPressQwerty`
the physical `N` key, assert `ResetAllManualFlagsCommand` did NOT fire and
the character was inserted into the textbox normally). If Avalonia's
default behavior doesn't already protect against this, that's a blocking
finding for this task, not a shippable known-limitation.

### `ResultsWindow.axaml` changes

- Add a `<Window.KeyBindings>` block: Ctrl+E/Ctrl+P/R bound to
  `{Binding ExportXlsxAsyncCommand}`/`{Binding ExportPdfAsyncCommand}`/
  `{Binding RefreshCommand}` (these already exist on `ResultsViewModel`,
  already the `DataContext` there). Ctrl+Q bound the same
  `RelativeSource={RelativeSource Self}` `CloseCommand` pattern as
  `MainWindow`.

### `App.axaml.cs` change

Pass two more constructor arguments to `MainViewModel`:
`new AvaloniaAboutWindowService(window)`,
`new AvaloniaShortcutsWindowService(window)` — same pattern as the
existing three service arguments.

## Testing

Avalonia.Headless's `KeyPressQwerty`/`KeyReleaseQwerty` extension methods
(confirmed present in the installed `Avalonia.Headless` 11.2.3 package,
not used anywhere in this codebase yet) simulate real physical key input
against a headless `TopLevel` — a genuine input-simulation capability this
box has lacked for mouse (`RaiseEvent(ClickEvent)` is a routed-event
shortcut, not physical input) and has had *none* of for keyboard until
now.

- One test per **Wired** row: construct the real window (+ a
  `MainViewModel`/`ResultsViewModel` wired to real fixture data, following
  `MainViewModelNavigationTests`'s temp-directory XML-fixture pattern
  where a loaded dataset is needed — e.g. for Next/Previous/First/Last),
  `window.Show()`, `window.KeyPressQwerty(<PhysicalKey>, <RawInputModifiers>)`
  (+ matching `KeyReleaseQwerty`), assert the resulting ViewModel state
  changed exactly as the bound command would produce (e.g. `CurrentIndex`
  advanced, `RunStatusText` changed, `NMin` back to 100).
- `ResetAllImportAndThresholdControlsCommand`: a plain
  `MainViewModelSettingsTests`-style unit test (no window needed) —
  set both filter groups to non-default values, execute the command,
  assert all six revert to their hardcoded literals.
- `OpenAboutWindowCommand`/`OpenShortcutsWindowCommand`: unit tests
  against a fake `IAboutWindowService`/`IShortcutsWindowService`
  (recording whether `Show()` was called), same style as existing
  `IConfirmDialogService` fakes.
- Ctrl+Q / G: headless `KeyPressQwerty` tests asserting `window.Close()`
  was actually invoked (e.g. via `Closing`/`Closed` event) and
  `GoToExperimentTextBox.IsFocused` respectively.
- `AboutWindow`/`ShortcutsWindow` content: lightweight tests asserting
  the expected static text is present in the visual tree (same
  `GetVisualDescendants` pattern `MainWindowAppearanceTests` already
  uses for finding controls), not a full pixel/screenshot check.
