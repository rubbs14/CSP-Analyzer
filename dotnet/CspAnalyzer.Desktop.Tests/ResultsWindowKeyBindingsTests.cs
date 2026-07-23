using System.Collections.Generic;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class ResultsWindowKeyBindingsTests
{
    private static (ResultsWindow Window, ResultsViewModel ViewModel) NewWindow()
    {
        var reference = new PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ref",
            Peaklist = new() { new Peak { Number = 1, Intensity = 100 } },
        };
        var vm = new ResultsViewModel(new NullFilePickerService(), reference, new List<PeaklistSpectrum>(), new List<SpectrumResult>());
        var window = new ResultsWindow { DataContext = vm };
        window.Show();
        return (window, vm);
    }

    // TotalExperiments/ActivesAuto/etc. are [ObservableProperty] ints that
    // skip their PropertyChanged notification when the new value equals
    // the old one (CommunityToolkit.Mvvm's generated setter does an
    // EqualityComparer check) - and Rebuild() is a pure function of fields
    // that never change in this fixture, so "before vs. after" on those
    // properties is unchanged regardless of whether R actually fires
    // RefreshCommand, does nothing, or the KeyBinding is missing entirely.
    // Rows.Clear()/Add(...), by contrast, are ObservableCollection
    // mutations that always raise CollectionChanged - even when clearing
    // an already-empty collection or re-adding identical rows - so
    // observing a CollectionChanged notification after the key press is a
    // real proof that Rebuild() (and therefore RefreshCommand) ran.
    [AvaloniaFact]
    public void R_InvokesRefreshCommand()
    {
        (ResultsWindow window, ResultsViewModel vm) = NewWindow();
        bool rowsRebuilt = false;
        vm.Rows.CollectionChanged += (_, _) => rowsRebuilt = true;

        window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.R, RawInputModifiers.None);

        Assert.True(rowsRebuilt);
    }

    [AvaloniaFact]
    public void CtrlQ_ClosesWindow()
    {
        (ResultsWindow window, _) = NewWindow();
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
}
