using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class HelpViewModelTests
{
    private static HelpViewModel MakeValidViewModel() => new()
    {
        NMaxText = "135",
        NMinText = "105",
        HMaxText = "11",
        HMinText = "6",
        MiText = "0.0001",
        PpNumText = "90",
    };

    [Fact]
    public void GenerateCommand_CanExecute_FalseWhenAnyFieldEmpty()
    {
        var vm = MakeValidViewModel();
        vm.PpNumText = "";

        Assert.False(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void GenerateCommand_CanExecute_FalseWhenFieldNonNumeric()
    {
        var vm = MakeValidViewModel();
        vm.PpNumText = "ninety";

        Assert.False(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void GenerateCommand_CanExecute_TrueWhenAllSixFieldsValid()
    {
        var vm = MakeValidViewModel();

        Assert.True(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void Generate_BuildsExpectedTopSpinCommandString()
    {
        var vm = MakeValidViewModel();

        vm.GenerateCommand.Execute(null);

        Assert.Equal(
            "1 F1P 135; 2 F1P 11; 1 F2P 105; 2 F2P 6; MI 0.0001; PPNUM 90; pp2d nodia",
            vm.GeneratedCommandText);
    }

    [Fact]
    public void ResetCommand_ClearsAllInputsAndGeneratedText()
    {
        var vm = MakeValidViewModel();
        vm.GenerateCommand.Execute(null);

        vm.ResetCommand.Execute(null);

        Assert.Equal("", vm.NMaxText);
        Assert.Equal("", vm.NMinText);
        Assert.Equal("", vm.HMaxText);
        Assert.Equal("", vm.HMinText);
        Assert.Equal("", vm.MiText);
        Assert.Equal("", vm.PpNumText);
        Assert.Equal("", vm.GeneratedCommandText);
    }
}
