using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainWindowKeyBindingsTests
{
    private static (MainWindow Window, MainViewModel ViewModel) NewWindow()
    {
        var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (window, vm);
    }

    [AvaloniaFact]
    public void CtrlK_OpensShortcutsWindow()
    {
        var recording = new RecordingShortcutsWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), recording, new NullHelpWindowService(),
            new NullInfoDialogService(), new SettingsService());
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.Control);

        Assert.Equal(1, recording.ShowCallCount);
    }

    // CancelRun's only real effect (_runCts?.Cancel()) isn't observable
    // through public state without a live subprocess (RunAsync returns
    // before creating _runCts when no python env is found, which is always
    // true in this test environment), and T is now wrapped in
    // GuardedViewModelCommand like R/N/D/S/A, so it no longer resolves to
    // the exact vm.CancelRunCommand instance (that structural check is
    // gone by design - see MainWindow.axaml.cs). Instead, set IsRunning
    // directly (its setter is public, and CanCancelRun/CanExecute are
    // evaluated live with no caching to invalidate) and inject a real
    // CancellationTokenSource into the private _runCts field via
    // reflection, so a real key press can prove whether CancelRun() -
    // and therefore _runCts.Cancel() - actually ran.
    private static void InjectRunCts(MainViewModel vm, CancellationTokenSource cts) =>
        typeof(MainViewModel)
            .GetField("_runCts", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(vm, cts);

    [AvaloniaFact]
    public void T_GuardedWhileTextBoxFocused_DoesNotCancelRun()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.IsRunning = true;
        var cts = new CancellationTokenSource();
        InjectRunCts(vm, cts);
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.T, RawInputModifiers.None);

        Assert.False(cts.IsCancellationRequested);
    }

    [AvaloniaFact]
    public void T_NotFocused_CancelsRun()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.IsRunning = true;
        var cts = new CancellationTokenSource();
        InjectRunCts(vm, cts);

        window.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.T, RawInputModifiers.None);

        Assert.True(cts.IsCancellationRequested);
    }

    [AvaloniaFact]
    public async Task N_ResetsAllManualFlags()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ds",
            Peaklist = new(),
            UserSelection = "ACTIVE (MAN)",
        });

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.N, RawInputModifiers.None);
        await ((IAsyncRelayCommand)vm.ResetAllManualFlagsCommand).ExecutionTask!;

        Assert.Equal("Not set", vm.DatasetSpectra[0].UserSelection);
    }

    [AvaloniaFact]
    public void CtrlAltI_ResetsImportControls()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.NMin = 1;
        vm.NMax = 2;

        window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.Control | RawInputModifiers.Alt);
        window.KeyReleaseQwerty(PhysicalKey.I, RawInputModifiers.Control | RawInputModifiers.Alt);

        Assert.Equal(100, vm.NMin);
        Assert.Equal(140, vm.NMax);
    }

    [AvaloniaFact]
    public void CtrlAltO_ResetsBothImportAndThresholdControls()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.NMin = 1;
        vm.ReferenceIntensityThreshold = 1;

        window.KeyPressQwerty(PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Alt);
        window.KeyReleaseQwerty(PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Alt);

        Assert.Equal(100, vm.NMin);
        Assert.Equal(5000, vm.ReferenceIntensityThreshold);
    }

    // Ctrl+C/Ctrl+Y/Ctrl+X share the exact same defect class as the bare
    // letters and arrow keys above: Window.KeyBindings intercept at the
    // raw-input stage before a routed KeyDown reaches a focused control, so
    // a plain {Binding ResetBarChartZoomCommand}/{Binding
    // ResetOverlayZoomCommand}/{Binding FitOverlayZoomToReferenceCommand}
    // KeyBinding on Ctrl+C/Ctrl+Y/Ctrl+X always fires the chart-reset
    // commands instead of letting a focused TextBox perform its normal
    // clipboard copy/redo/cut - even with text selected.
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

    [AvaloniaFact]
    public void CtrlX_GuardedWhileTextBoxFocused_DoesNotFitZoomToReference()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ref",
            Peaklist = new() { new CspAnalyzer.BackendInterop.Peak { F1 = 110, F2 = 8 } },
        };
        vm.BuildOverlayAxes();
        vm.OverlayXAxes[0].MinLimit = 999;
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.X, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.X, RawInputModifiers.Control);

        Assert.Equal(999, vm.OverlayXAxes[0].MinLimit);
    }

    [AvaloniaFact]
    public void CtrlX_NotFocused_FitsZoomToReference()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ref",
            Peaklist = new() { new CspAnalyzer.BackendInterop.Peak { F1 = 110, F2 = 8 } },
        };
        vm.BuildOverlayAxes();
        vm.OverlayXAxes[0].MinLimit = 999;

        window.KeyPressQwerty(PhysicalKey.X, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.X, RawInputModifiers.Control);

        Assert.Equal(-(8 + 0.5), vm.OverlayXAxes[0].MinLimit);
    }

    // The input-focus caveat from the design spec: bare-letter KeyBindings
    // (N here) must not fire while a TextBox has focus and the user is
    // typing normal text. GoToExperimentText is the one sidebar TextBox
    // bound to a plain string (the numeric ones reject non-digit input
    // before this even becomes observable), so it's the fixture that can
    // prove a real character got inserted. The dataset fixture's
    // UserSelection staying "ACTIVE (MAN)" (not flipped to "Not set")
    // proves ResetAllManualFlagsCommand did NOT fire while the TextBox had
    // focus - if this assertion fails, that's the blocking finding the
    // design spec calls out, not a shippable known-limitation.
    [AvaloniaFact]
    public void TypingLetterN_InGoToExperimentTextBox_InsertsCharacterAndDoesNotResetManualFlags()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ds",
            Peaklist = new(),
            UserSelection = "ACTIVE (MAN)",
        });
        var goToBox = window.FindControl<Avalonia.Controls.TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        // KeyPressQwerty/KeyReleaseQwerty only raise the raw KeyDown/KeyUp
        // events (confirmed against Avalonia.Headless's HeadlessWindowImpl
        // source) - text insertion is a distinct, independent raw input
        // event that a real keyboard+IME would send alongside the key
        // press, simulated here via KeyTextInput.
        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        window.KeyTextInput("n");
        window.KeyReleaseQwerty(PhysicalKey.N, RawInputModifiers.None);

        Assert.Equal("n", vm.GoToExperimentText);
        Assert.Equal("ACTIVE (MAN)", vm.DatasetSpectra[0].UserSelection);
    }

    [AvaloniaFact]
    public void G_FocusesGoToExperimentTextBox()
    {
        (MainWindow window, _) = NewWindow();
        var textBox = window.FindControl<Avalonia.Controls.TextBox>("GoToExperimentTextBox")!;

        window.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.G, RawInputModifiers.None);

        Assert.True(textBox.IsFocused);
    }

    [AvaloniaFact]
    public void G_GuardedWhileTextBoxFocused_DoesNotStealFocus()
    {
        (MainWindow window, _) = NewWindow();
        var nMinBox = window.FindControl<TextBox>("NMinTextBox")!;
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        nMinBox.Focus();

        window.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.G, RawInputModifiers.None);

        Assert.False(goToBox.IsFocused);
        Assert.True(nMinBox.IsFocused);
    }

    // Arrow keys share the same class of bug the bare letters did: a plain
    // {Binding NextCommand} KeyBinding intercepts Right before a focused
    // TextBox's caret-movement handling ever runs (see the comment above
    // <Window.KeyBindings> in MainWindow.axaml), which breaks ordinary text
    // editing in every sidebar TextBox whenever a dataset is loaded (the
    // normal state of the app, since that's what makes NextCommand
    // CanExecute==true in the first place).
    [AvaloniaFact]
    public void Right_GuardedWhileTextBoxFocused_DoesNotAdvance()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 2, DsName = "ds", Peaklist = new() });
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);

        Assert.Equal(0, vm.CurrentIndex);
    }

    [AvaloniaFact]
    public void Right_NotFocused_AdvancesToNextExperiment()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 2, DsName = "ds", Peaklist = new() });

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);

        Assert.Equal(1, vm.CurrentIndex);
    }

    [AvaloniaFact]
    public void Left_GuardedWhileTextBoxFocused_DoesNotGoToPrevious()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 2, DsName = "ds", Peaklist = new() });
        vm.CurrentIndex = 1;
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);

        Assert.Equal(1, vm.CurrentIndex);
    }

    [AvaloniaFact]
    public void Left_NotFocused_GoesToPreviousExperiment()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 2, DsName = "ds", Peaklist = new() });
        vm.CurrentIndex = 1;

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);

        Assert.Equal(0, vm.CurrentIndex);
    }

    [AvaloniaFact]
    public void Down_GuardedWhileTextBoxFocused_DoesNotGoToLast()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 2, DsName = "ds", Peaklist = new() });
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);

        Assert.Equal(0, vm.CurrentIndex);
    }

    [AvaloniaFact]
    public void Down_NotFocused_GoesToLastExperiment()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 2, DsName = "ds", Peaklist = new() });

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);

        Assert.Equal(1, vm.CurrentIndex);
    }

    [AvaloniaFact]
    public void Up_GuardedWhileTextBoxFocused_DoesNotGoToFirst()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 2, DsName = "ds", Peaklist = new() });
        vm.CurrentIndex = 1;
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);

        Assert.Equal(1, vm.CurrentIndex);
    }

    [AvaloniaFact]
    public void Up_NotFocused_GoesToFirstExperiment()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 2, DsName = "ds", Peaklist = new() });
        vm.CurrentIndex = 1;

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);

        Assert.Equal(0, vm.CurrentIndex);
    }

    // R/D/S/A already had the GuardedViewModelCommand wiring since S11c
    // Task 4, but only N and G had a dedicated test actually pressing the
    // key while a TextBox was focused to prove the guard blocks it - these
    // fill that gap.
    [AvaloniaFact]
    public void R_GuardedWhileTextBoxFocused_DoesNotAttemptRun()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.ReferenceSpectrum = new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ref", Peaklist = new() };
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.R, RawInputModifiers.None);

        Assert.Equal("", vm.RunStatusText);
    }

    [AvaloniaFact]
    public void D_GuardedWhileTextBoxFocused_DoesNotResetManualStatus()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ds",
            Peaklist = new(),
            UserSelection = "ACTIVE (MAN)",
        });
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.D, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.D, RawInputModifiers.None);

        Assert.Equal("ACTIVE (MAN)", vm.DatasetSpectra[0].UserSelection);
    }

    [AvaloniaFact]
    public void S_GuardedWhileTextBoxFocused_DoesNotMarkInactive()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.S, RawInputModifiers.None);

        Assert.Equal("Not set", vm.DatasetSpectra[0].UserSelection);
    }

    [AvaloniaFact]
    public void A_GuardedWhileTextBoxFocused_DoesNotMarkActive()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();
        vm.DatasetSpectra.Add(new CspAnalyzer.BackendInterop.PeaklistSpectrum { ExpNumber = 1, DsName = "ds", Peaklist = new() });
        var goToBox = window.FindControl<TextBox>("GoToExperimentTextBox")!;
        goToBox.Focus();

        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.None);

        Assert.Equal("Not set", vm.DatasetSpectra[0].UserSelection);
    }

    [AvaloniaFact]
    public void CtrlQ_ClosesWindow()
    {
        (MainWindow window, _) = NewWindow();
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        // KeyBinding commands execute synchronously on KeyDown (see
        // KeyboardDevice.ProcessRawEvent), so CloseCommand's Close() call
        // - and the headless platform impl teardown that goes with it -
        // has already happened by the time KeyPressQwerty returns. A
        // subsequent KeyReleaseQwerty targets an already-disposed
        // headless window and throws ("TopLevel must be a headless
        // window"), so it's intentionally omitted here.
        window.KeyPressQwerty(PhysicalKey.Q, RawInputModifiers.Control);

        Assert.True(closed);
    }

    private sealed class RecordingShortcutsWindowService : IShortcutsWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    private sealed class RecordingHelpWindowService : IHelpWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    [AvaloniaFact]
    public void H_OpensHelpWindow()
    {
        var recording = new RecordingHelpWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), recording,
            new NullInfoDialogService(), new SettingsService());
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.H, RawInputModifiers.None);

        Assert.Equal(1, recording.ShowCallCount);
    }

    [AvaloniaFact]
    public void H_GuardedWhileTextBoxFocused_DoesNotOpenHelpWindow()
    {
        var recording = new RecordingHelpWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), recording,
            new NullInfoDialogService(), new SettingsService());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var nMinBox = window.FindControl<TextBox>("NMinTextBox")!;
        nMinBox.Focus();

        window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.H, RawInputModifiers.None);

        Assert.Equal(0, recording.ShowCallCount);
    }

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
}
