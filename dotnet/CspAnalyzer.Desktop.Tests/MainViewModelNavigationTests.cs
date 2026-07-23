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
}
