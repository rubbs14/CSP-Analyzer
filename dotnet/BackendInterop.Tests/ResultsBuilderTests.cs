using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

public class ResultsBuilderTests
{
    private static PeaklistSpectrum MakeSpectrum(int expNumber, string dsName, int totReadPeaks, params double[] intensities) =>
        new()
        {
            ExpNumber = expNumber,
            DsName = dsName,
            TotReadPeaks = totReadPeaks,
            Peaklist = intensities.Select((intensity, i) => new Peak { Number = i + 1, Intensity = intensity }).ToList(),
        };

    [Fact]
    public void Build_puts_the_reference_row_first_with_none_for_result_fields()
    {
        PeaklistSpectrum reference = MakeSpectrum(11, "gpHUB1_FR_REF_pool1_130416", 83, 1000, 5000, 23499);

        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(reference, Array.Empty<PeaklistSpectrum>(), Array.Empty<SpectrumResult>());

        ResultRow row = Assert.Single(rows);
        Assert.Equal("Reference", row.Name);
        Assert.Equal("gpHUB1_FR_REF_pool1_130416", row.Dataset);
        Assert.Equal(83, row.TotalReadPeaks);
        Assert.Equal(1000, row.MinIntensity);
        Assert.Equal(23499, row.MaxIntensity);
        Assert.Null(row.PeakDifference);
        Assert.Null(row.Probability);
        Assert.Null(row.AutomaticAnalysis);
        Assert.Equal("none", row.ManualFlag);
    }

    [Fact]
    public void Build_joins_a_dataset_spectrum_with_its_matching_run_result()
    {
        PeaklistSpectrum reference = MakeSpectrum(11, "ref_ds", 80, 100, 200);
        var spectrum = MakeSpectrum(101, "gpHUB1_FS_pool1_130416", 64, 50, 900);
        spectrum.UserSelection = "Not set";
        var result = new SpectrumResult { ExpNumber = 101, IsActive = true, ActivePseudoprobability = 0.9137 };

        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(reference, new[] { spectrum }, new[] { result });

        Assert.Equal(2, rows.Count);
        ResultRow row = rows[1];
        Assert.Equal("101", row.Name);
        Assert.Equal("gpHUB1_FS_pool1_130416", row.Dataset);
        Assert.Equal(64, row.TotalReadPeaks);
        Assert.Equal(50, row.MinIntensity);
        Assert.Equal(900, row.MaxIntensity);
        Assert.Equal(64 - 80, row.PeakDifference);
        Assert.Equal(0.91, row.Probability);
        Assert.Equal("Active", row.AutomaticAnalysis);
        Assert.Equal("Not set", row.ManualFlag);
    }

    [Fact]
    public void Build_omits_a_dataset_spectrum_with_no_matching_run_result()
    {
        PeaklistSpectrum reference = MakeSpectrum(11, "ref_ds", 80, 100, 200);
        var spectrum = MakeSpectrum(101, "ds", 64, 50, 900);

        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(reference, new[] { spectrum }, Array.Empty<SpectrumResult>());

        Assert.Single(rows); // reference row only
    }

    [Fact]
    public void Build_reports_a_negative_peak_difference_when_the_experiment_has_fewer_peaks_than_the_reference()
    {
        PeaklistSpectrum reference = MakeSpectrum(11, "ref_ds", 100, 1, 2);
        var spectrum = MakeSpectrum(101, "ds", 30, 1, 2);
        var result = new SpectrumResult { ExpNumber = 101, IsActive = false, ActivePseudoprobability = 0.1 };

        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(reference, new[] { spectrum }, new[] { result });

        Assert.Equal(-70, rows[1].PeakDifference);
    }
}
