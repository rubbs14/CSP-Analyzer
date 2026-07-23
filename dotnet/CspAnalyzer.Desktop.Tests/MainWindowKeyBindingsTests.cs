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
            new NullAboutWindowService(), recording);
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.Control);

        Assert.Equal(1, recording.ShowCallCount);
    }

    // CancelRun's only real effect (_runCts?.Cancel()) isn't observable
    // through public state without a live subprocess (RunAsync returns
    // before creating _runCts when no python env is found, which is always
    // true in this test environment). Verified structurally instead: the
    // KeyBinding for "T" resolves to the exact same command instance the
    // ViewModel exposes, which is what makes the real key press wire to
    // the real command in production.
    [AvaloniaFact]
    public void T_IsBoundToCancelRunCommand()
    {
        (MainWindow window, MainViewModel vm) = NewWindow();

        KeyBinding binding = Assert.Single(window.KeyBindings, kb => kb.Gesture.Key == Key.T && kb.Gesture.KeyModifiers == KeyModifiers.None);

        Assert.Same(vm.CancelRunCommand, binding.Command);
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
}
