using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class SanityTests
{
    [Fact]
    public void MainViewModel_constructs_with_default_null_services()
    {
        var vm = new MainViewModel();

        Assert.False(vm.IsReferenceLoaded);
        Assert.Empty(vm.DatasetSpectra);
    }
}
