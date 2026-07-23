using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelNavigationTests
{
    // A fake picker that returns a fixed reference file / dataset folder,
    // so LoadReferenceAsync/LoadDatasetAsync can be exercised against real
    // temp-directory fixtures without a live Avalonia file dialog.
    private sealed class FixedFolderFilePickerService(string referenceXmlPath, string datasetFolder) : IFilePickerService
    {
        public Task<string?> PickXmlFileAsync(string title) => Task.FromResult<string?>(referenceXmlPath);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(datasetFolder);
        public Task<string?> PickSaveFileAsync(string suggestedFileName, string extension) => Task.FromResult<string?>(null);
    }

    // F1=120 (within default NMin=100/NMax=140), F2=8 (within default
    // HMin=5/HMax=12), intensity=9000 (>= default DatasetIntensityThreshold
    // 2000) - matches MainViewModel's default import filter so the peak
    // survives PeaklistXmlParser.Parse without needing custom thresholds.
    private static string WritePeaklistXml(string expNumberFolder, string datasetRoot)
    {
        string subfolder = Path.Combine(datasetRoot, expNumberFolder, "pdata", "1");
        Directory.CreateDirectory(subfolder);
        string path = Path.Combine(subfolder, "peaklist.xml");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="utf-8"?>
            <peaklist>
              <PeakList2D>
                <Peak2D F1="120.0" F2="8.0" intensity="9000" Number="1"/>
              </PeakList2D>
            </peaklist>
            """);
        return path;
    }

    [Fact]
    public async Task LoadDatasetAsync_sorts_experiments_by_ExpNumber_not_directory_listing_order()
    {
        string root = Directory.CreateTempSubdirectory("csp_nav_test_").FullName;
        string refXml = WritePeaklistXml("1", Path.Combine(root, "ref_ds"));

        string dsRoot = Path.Combine(root, "ds");
        // "9" sorts AFTER "10" lexically (Directory.GetDirectories'
        // enumeration order on most filesystems), but must come first
        // numerically - a fixture where alphabetical and numeric order
        // actually disagree, unlike same-length numbers.
        WritePeaklistXml("9", dsRoot);
        WritePeaklistXml("10", dsRoot);

        var vm = new MainViewModel(new FixedFolderFilePickerService(refXml, dsRoot), new NullResultsWindowService());
        await vm.LoadReferenceCommand.ExecuteAsync(null);
        await vm.LoadDatasetCommand.ExecuteAsync(null);

        Assert.Equal(new[] { 9, 10 }, vm.DatasetSpectra.Select(s => s.ExpNumber));
    }

    private static PeaklistSpectrum MakeSpectrum(int expNumber) => new()
    {
        ExpNumber = expNumber,
        DsName = "ds",
        TotReadPeaks = 10 + expNumber,
        Peaklist = { new Peak { Number = 1, F1 = 120, F2 = 8, Intensity = 9000 } },
    };

    private static MainViewModel MakeViewModelWithDataset(int refTotReadPeaks, params int[] expNumbers)
    {
        var vm = new MainViewModel();
        vm.ReferenceSpectrum = new PeaklistSpectrum { ExpNumber = 1, DsName = "ref", TotReadPeaks = refTotReadPeaks };
        foreach (int exp in expNumbers)
        {
            vm.DatasetSpectra.Add(MakeSpectrum(exp));
        }
        vm.RaiseNavigationChanged();
        return vm;
    }

    [Fact]
    public void CurrentView_defaults_to_the_full_dataset_when_no_filter_is_set()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102, 103);

        Assert.Equal(3, vm.CurrentView.Count);
        Assert.Equal(101, vm.CurrentSpectrum!.ExpNumber);
        Assert.Equal("1 / 3", vm.CurrentCounterText);
    }

    [Fact]
    public void CurrentView_filters_to_actives_only_using_RunResults_IsActive()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102, 103);
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 101, IsActive = true });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 102, IsActive = false });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 103, IsActive = true });
        vm.RaiseNavigationChanged();

        vm.IsActivesFilterChecked = true;

        Assert.Equal(2, vm.CurrentView.Count);
        Assert.All(vm.CurrentView, s => Assert.True(s.ExpNumber is 101 or 103));
    }

    [Fact]
    public void Checking_Inactives_filter_unchecks_Actives_filter()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101);

        vm.IsActivesFilterChecked = true;
        vm.IsInactivesFilterChecked = true;

        Assert.False(vm.IsActivesFilterChecked);
        Assert.True(vm.IsInactivesFilterChecked);
    }

    [Fact]
    public void CurrentPeakDifference_is_TotReadPeaks_minus_reference()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101);

        Assert.Equal(vm.DatasetSpectra[0].TotReadPeaks - 80, vm.CurrentPeakDifference);
    }

    [Fact]
    public void First_and_Previous_are_disabled_at_index_zero()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102);

        Assert.False(vm.FirstCommand.CanExecute(null));
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.True(vm.NextCommand.CanExecute(null));
        Assert.True(vm.LastCommand.CanExecute(null));
    }

    [Fact]
    public void Next_and_Last_are_disabled_at_the_final_index()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102);

        vm.LastCommand.Execute(null);

        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.False(vm.LastCommand.CanExecute(null));
        Assert.True(vm.PreviousCommand.CanExecute(null));
        Assert.Equal(102, vm.CurrentSpectrum!.ExpNumber);
    }

    [Fact]
    public void All_nav_commands_are_disabled_with_a_single_experiment()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101);

        Assert.False(vm.FirstCommand.CanExecute(null));
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.False(vm.LastCommand.CanExecute(null));
    }

    [Fact]
    public void GoToExperiment_jumps_to_the_matching_experiment()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102, 103);

        vm.GoToExperimentText = "103";
        vm.GoToExperimentCommand.Execute(null);

        Assert.Equal(2, vm.CurrentIndex);
        Assert.Equal("", vm.GoToStatusText);
    }

    [Fact]
    public void GoToExperiment_reports_not_found_and_does_not_move()
    {
        MainViewModel vm = MakeViewModelWithDataset(80, 101, 102);

        vm.GoToExperimentText = "999";
        vm.GoToExperimentCommand.Execute(null);

        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal("Experiment 999 not found.", vm.GoToStatusText);
    }
}
