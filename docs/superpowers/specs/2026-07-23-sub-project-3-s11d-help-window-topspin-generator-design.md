# S11d — Help Window + TopSpin Command Generator Design

## Context

Split out of S11c's original scope during that session's brainstorming
(see `2026-07-23-sub-project-3-s11c-keyboard-shortcuts-design.md`'s
Context section): the Help window is a large, mostly-independent chunk —
static Q&A content plus a real mini-tool (6 inputs, Generate/Copy/Reset,
string building) ported from `CSPv2/FormHelp.cs`.

S11c already left the ground wired for this session:
- `MainWindow.axaml`'s sidebar has an inert `<Button Content="Help" />`
  (no `Command`) next to the now-wired About/Shortcuts buttons.
- `ShortcutsWindow.axaml`'s "Loading Data/Processing" section lists `H` as
  "Show Help Guide" bundled with `Enter`/`I`, all three tagged
  "(not yet implemented)".
- The `IAboutWindowService`/`IShortcutsWindowService` +
  `Avalonia*`/`Null*` + `MainViewModel.SecondaryWindows.cs` pattern is
  established and this session follows it exactly for Help.

## Goals

- New modeless `HelpWindow`, opened via the sidebar's existing inert
  "Help" button and via a new guarded `H` keybinding.
- Static "Tips and Tricks" content ported from `FormHelp.resx`, reworded
  where the legacy text describes the old stack inaccurately (verified
  against the current `backend/` — see Content section).
- A real TopSpin peak-picking command generator: 6 numeric inputs,
  Generate/Reset/Copy, replacing legacy's keystroke-filtering validation
  with real numeric parsing/`CanExecute` guards, and fixing a legacy
  label bug (`PPMPNUM` shown in one label vs. the actual `PPNUM` token
  used in the generated command — the command builder already only ever
  emitted `PPNUM`; this session's port uses `PPNUM` consistently
  everywhere including the label).
- Update `ShortcutsWindow.axaml`'s row 18 so `H` → "Show Help Guide" no
  longer reads "(not yet implemented)"; `Enter`/`I` stay marked N/I
  (unrelated, out of scope — see S11c's design doc for why Enter is
  unmappable and no Information window exists).

## Non-goals

- The "Peak lists extractor" mini-app legacy text describes is a
  *separate* legacy tool, not part of this codebase. Its paragraph is
  kept as informational text only — no button, no functionality.
- `Enter`/`I` keybindings (Load Reference/Dataset focus-context-dependent
  behavior; Information window) — unrelated to Help, still N/I per S11c.
- Any content or generator behavior beyond what `FormHelp.cs` already
  does (no new tips, no new generator fields).

## Content (ported + reworded)

Source: `CSPv2/FormHelp.resx` (`label4`/`label5`/`label10`/`label14`
string resources) + `FormHelp.Designer.cs` layout.

**Common issues:**

1. *"No actives found after analysis"* — legacy text blamed a specific
   "SMOTE-ENN processing method" for struggling above ~150 peaks. Grepping
   `backend/` confirms no SMOTE-ENN anywhere — the current pipeline is
   scaler → PCA → SVM (`backend/README.md`). Reworded to describe the
   real cause generically: incorrect/lousy peak picking producing feature
   vectors the classifier wasn't trained to separate well, without naming
   a specific algorithm that no longer exists in this codebase.
2. *"Memory error exception(s)"* — legacy blamed "the Conda environment...
   we chose to build our own Python environment." Still accurate: the
   backend still runs via a dedicated `csp_modern` conda env
   (`BackendEnvironment`/`CondaPythonPaths`, S11). Reworded only to name
   it correctly (`csp_modern` conda environment invoked as a subprocess)
   rather than the vaguer legacy phrasing; same guidance (free memory,
   rerun).
3. *"Unable to display analysis results"* — legacy blamed temp-folder
   permissions. Kept, generic wording (still plausible: the .NET app
   still writes a temp JSON file for the backend run, per S9).

**Tricks — Peak picking in TopSpin:** kept verbatim (`label10`'s
explanation + the example command in `label12`
`"1 F1P 135; 2 F1P 11; 1 F2P 105; 2 F2P 6; MI 0.0001; PPNUM 90; pp2d nodia"`
— note the example already uses `PPNUM`, only the *field label* in
legacy had the `PPMPNUM` typo).

**Peak lists extractor:** `label14`'s paragraph kept verbatim as
informational text (Non-goals).

## Architecture

### New service (mirrors `IAboutWindowService`)

- `IHelpWindowService { void Show(); }`
- `AvaloniaHelpWindowService(Window owner)` — `new HelpWindow();
  window.Show(owner);`
- `NullHelpWindowService` — no-op, for the parameterless
  `MainViewModel()` design-time/test constructor.

### `MainViewModel` changes

- `MainViewModel.SecondaryWindows.cs` gains `_helpWindowService` field
  and:
  ```csharp
  [RelayCommand]
  private void OpenHelpWindow() => _helpWindowService.Show();
  ```
- Constructor gains a 6th parameter `IHelpWindowService helpWindowService`;
  parameterless ctor passes `new NullHelpWindowService()`.
- `App.axaml.cs` passes `new AvaloniaHelpWindowService(window)` as the
  6th argument.

### `HelpViewModel` (new, TopSpin generator only)

Plain `ObservableObject` (CommunityToolkit.Mvvm), no Avalonia
dependencies, instantiated directly by `HelpWindow`'s code-behind
(`DataContext = new HelpViewModel();`) — no DI needed since it's
stateless/self-contained, same reasoning as `AboutWindow` needing no
ViewModel at all, except this one has real state.

- Six string-backed input properties (`NMaxText`, `NMinText`, `HMaxText`,
  `HMinText`, `MiText`, `PpNumText`) — string, not `double`/`int`
  directly, so a `TextBox` can hold an empty or partially-typed value
  without a binding exception; parsed on demand.
- `GenerateCommand` ([RelayCommand] with `CanExecute`): parses all six
  via `double.TryParse` (`NMax`/`NMin`/`HMax`/`HMin`/`MI`) and
  `int.TryParse` (`PPNUM`); `CanExecute` is true only when all six parse.
  On execute, builds:
  ```
  1 F1P {NMax}; 2 F1P {HMax}; 1 F2P {NMin}; 2 F2P {HMin}; MI {MI}; PPNUM {PPNUM}; pp2d nodia
  ```
  (same field→token mapping as legacy's `button_generate`: `textBox_1F1P`
  = NMax, `textBox_2F1P` = HMax, `textBox_1F2P` = NMin, `textBox_2F2P` =
  HMin) into `GeneratedCommandText`.
- `ResetCommand` ([RelayCommand], no guard): clears all six input
  strings and `GeneratedCommandText` — mirrors legacy `button2_Click`.
- Copy is **not** on `HelpViewModel` — clipboard access needs a
  `Visual`/`TopLevel`, which a plain `ObservableObject` doesn't have.
  `HelpWindow.axaml.cs` (code-behind) exposes a public `ICommand
  CopyCommand` property (`RelayCommand`, same `MainWindow.CloseCommand`
  pattern for View-only concerns), constructed in the constructor:
  `new RelayCommand(() => { if (!string.IsNullOrEmpty(_viewModel.GeneratedCommandText)) TopLevel.GetTopLevel(this)!.Clipboard!.SetTextAsync(_viewModel.GeneratedCommandText); })`.
  No `CanExecute` gating — the button stays enabled and simply no-ops on
  empty text, avoiding a second cross-window `CanExecute`-notification
  wire-up for a purely cosmetic disabled-state.
  Bound in XAML via `Command="{Binding CopyCommand, RelativeSource={RelativeSource AncestorType=Window}}"`
  — same `RelativeSource Self`-on-the-Window-instance pattern
  `MainWindow.axaml`'s Ctrl+Q binding already uses.
- The six input `[ObservableProperty]` strings use
  `[NotifyCanExecuteChangedFor(nameof(GenerateCommand))]` so
  `GenerateCommand.CanExecute` re-evaluates as the user types.

### `HelpWindow.axaml`/`.axaml.cs` (new)

`ScrollViewer > StackPanel`, same layout convention as `ShortcutsWindow`
(`Width="700" Height="700"`, roughly matching legacy's 781×688 minus its
borderless custom-close-button chrome — this port uses a normal bordered
`Window` like every other secondary window here, not legacy's
capture-based click-outside-to-close hack):

- Title "Help"
- "Tips and Tricks" header, "Common issues" subheader + 3 numbered
  issue blocks (title `TextBlock FontWeight="SemiBold"` + description
  `TextBlock TextWrapping="Wrap"`), reworded text from Content section.
- "Tricks" header, "Peak picking in TopSpin" subheader, explanatory
  paragraph, example command text.
- The generator: `Grid` of 6 labeled `TextBox`es (`x:Name`d for tests:
  `NMaxTextBox`, `NMinTextBox`, `HMaxTextBox`, `HMinTextBox`,
  `MiTextBox`, `PpNumTextBox`) bound to `HelpViewModel`'s six string
  properties, "Generate"/"Reset" buttons bound to
  `GenerateCommand`/`ResetCommand`, a read-only output `TextBox` bound to
  `GeneratedCommandText`, "Copy" button.
- "Peak lists extractor" subheader + paragraph (informational only).
- No `DataContext` set at the `HelpWindow` level for the static content
  (it's all literal XAML text like `ShortcutsWindow`) — only the
  generator section's controls bind to the `HelpViewModel` instance set
  in code-behind.

### `MainWindow.axaml`/`.axaml.cs` changes

- `<Button Content="Help" />` (line 173) → `<Button Content="Help"
  Command="{Binding OpenHelpWindowCommand}" />`.
- New guarded keybinding in `MainWindow.axaml.cs`'s constructor (bare
  letter, same bucket as `R`/`T`/`N`/`D`/`S`/`A` — must not fire while a
  `TextBox` has focus):
  ```csharp
  KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.H), Command = GuardedViewModelCommand(vm => vm.OpenHelpWindowCommand) });
  ```
  Not a plain XAML `{Binding}` in `<Window.KeyBindings>` — bare letters
  in this codebase are always added in code-behind via
  `GuardedViewModelCommand`, per the existing convention documented
  above that block in `MainWindow.axaml` (XAML `<Window.KeyBindings>` is
  reserved for Ctrl/Ctrl+Alt combos that don't collide with normal
  typing).

### `ShortcutsWindow.axaml` change

Row 18 (currently one `TextBlock` covering `Enter, H, I` with a single
trailing "(not yet implemented)") splits into two rows so `H` can read
as wired without misrepresenting `Enter`/`I`:

```xml
<TextBlock Grid.Row="2" Grid.Column="0" Text="H" />
<TextBlock Grid.Row="2" Grid.Column="1" Text="Show Help Guide" />
<TextBlock Grid.Row="3" Grid.Column="0" Text="Enter, I" />
<TextBlock Grid.Row="3" Grid.Column="1" Text="Load Reference/Dataset, Show Information Window (not yet implemented)" TextWrapping="Wrap" />
```

(`Grid.RowDefinitions` for that section grows from 3 to 4 `Auto` rows.)

## Testing

- `HelpViewModelTests.cs` (plain xunit, no `AvaloniaFact` — no Avalonia
  types involved): `GenerateCommand.CanExecute` false with any field
  empty/non-numeric, true with all six valid; executing produces the
  exact expected string (including the `PPNUM` token, asserting the
  legacy `PPMPNUM` typo is gone); `ResetCommand` clears all seven
  properties.
- `HelpWindowTests.cs` (`[AvaloniaFact]`, mirrors `AboutWindowTests`):
  static tips text present in the visual tree; typing valid values into
  the six named `TextBox`es + invoking Generate produces the expected
  text in the output `TextBox`; clicking Copy with non-empty generated
  text sets the clipboard (Avalonia.Headless exposes a settable
  clipboard on the headless `TopLevel` for this); clicking Copy with
  empty generated text is a no-op (doesn't throw, clipboard unchanged).
- `MainViewModelSecondaryWindowsTests.cs`: new `RecordingHelpWindowService`
  case for `OpenHelpWindowCommand`, same pattern as the existing
  About/Shortcuts recording-fake tests.
- `MainWindowKeyBindingsTests.cs`: two new tests mirroring `G`'s pair —
  `H_OpensHelpWindow` (bare press opens it — assert via a recording fake
  service through `DataContext`, not a real window-within-a-window) and
  `H_GuardedWhileTextBoxFocused_DoesNotOpenHelpWindow` (focus e.g.
  `NMinTextBox` first, press `H`, assert the service was *not* called).
