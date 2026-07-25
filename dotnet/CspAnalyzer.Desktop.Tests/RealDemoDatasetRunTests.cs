using System.IO;
using System.Threading.Tasks;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

/// <summary>
/// End-to-end run against the real bundled CSPv2/Demo-dataset (re-tracked
/// in git so both dev checkouts and packaged installs ship it - see
/// docs/superpowers/specs/2026-07-25-bundle-demo-dataset-design.md),
/// exercising LoadReferenceCommand -> LoadDatasetCommand -> RunCommand the
/// same way S9's throwaway harness and S10b's screenshot pass did manually,
/// but as a permanent automated test. Skips itself if no python backend is
/// resolvable (BackendEnvironment.PythonExecutable), matching
/// BackendInterop.Tests' integration-test convention - see
/// BackendCliRunnerIntegrationTests.cs.
/// </summary>
public class RealDemoDatasetRunTests
{
    private sealed class FixedFolderFilePickerService(string referenceXmlPath, string datasetFolder) : IFilePickerService
    {
        public Task<string?> PickXmlFileAsync(string title) => Task.FromResult<string?>(referenceXmlPath);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(datasetFolder);
        public Task<string?> PickSaveFileAsync(string suggestedFileName, string extension) => Task.FromResult<string?>(null);
    }

    [Fact]
    public async Task Run_against_real_demo_dataset_produces_known_S8_S10_verified_results()
    {
        if (BackendEnvironment.PythonExecutable is null)
        {
            return; // no python backend resolvable on this machine/CI job - see class doc.
        }

        string demoDatasetRoot = Path.Combine(BackendEnvironment.RepoRoot, "CSPv2", "Demo-dataset");
        string referenceXml = Path.Combine(demoDatasetRoot, "gpHUB1_FR_REF_pool1_130416", "11", "pdata", "1", "peaklist.xml");
        string datasetFolder = Path.Combine(demoDatasetRoot, "gpHUB1_FS_pool1_130416");

        var vm = new MainViewModel(
            new FixedFolderFilePickerService(referenceXml, datasetFolder),
            new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(),
            new NullShortcutsWindowService(), new NullHelpWindowService(), new NullInfoDialogService(),
            new SettingsService());

        await vm.LoadReferenceCommand.ExecuteAsync(null);
        Assert.Equal(83, vm.ReferencePeakCount);

        await vm.LoadDatasetCommand.ExecuteAsync(null);
        Assert.Equal(64, vm.DatasetSpectra.Count);

        await vm.RunCommand.ExecuteAsync(null);

        Assert.True(vm.RunCompletedSuccessfully, vm.RunStatusText);
        Assert.Equal(64, vm.RunResults.Count);
        Assert.Equal(1, vm.ActivesAutoCount);
        Assert.Equal(63, vm.InactivesAutoCount);
    }
}
