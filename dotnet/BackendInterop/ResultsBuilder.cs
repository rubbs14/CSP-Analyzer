namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Joins loaded dataset spectra with their classification results by
/// EXP_NUMBER - the two collections are populated separately (S8's
/// LoadDatasetAsync vs S9's RunAsync) and only come together here, for
/// display. A dataset spectrum with no matching run result is omitted
/// rather than throwing; that shouldn't happen given S9's flow (every
/// loaded spectrum is sent to the backend and every input produces one
/// output row), but silently dropping an orphan is safer for a display
/// join than crashing the whole results window over it.
/// </summary>
public static class ResultsBuilder
{
    public static IReadOnlyList<ResultRow> Build(
        PeaklistSpectrum reference,
        IReadOnlyList<PeaklistSpectrum> datasetSpectra,
        IReadOnlyList<SpectrumResult> runResults)
    {
        var rows = new List<ResultRow>
        {
            new(
                Name: "Reference",
                Dataset: reference.DsName,
                TotalReadPeaks: reference.TotReadPeaks,
                MinIntensity: reference.Peaklist.Min(p => p.Intensity),
                MaxIntensity: reference.Peaklist.Max(p => p.Intensity),
                PeakDifference: null,
                Probability: null,
                AutomaticAnalysis: null,
                ManualFlag: "none"),
        };

        Dictionary<int, SpectrumResult> resultsByExp =
            runResults.ToDictionary(r => r.ExpNumber);

        foreach (PeaklistSpectrum spectrum in datasetSpectra)
        {
            if (!resultsByExp.TryGetValue(spectrum.ExpNumber, out SpectrumResult? result))
            {
                continue;
            }

            rows.Add(new ResultRow(
                Name: spectrum.ExpNumber.ToString(),
                Dataset: spectrum.DsName,
                TotalReadPeaks: spectrum.TotReadPeaks,
                MinIntensity: spectrum.Peaklist.Min(p => p.Intensity),
                MaxIntensity: spectrum.Peaklist.Max(p => p.Intensity),
                PeakDifference: spectrum.TotReadPeaks - reference.TotReadPeaks,
                Probability: Math.Round(result.ActivePseudoprobability, 2),
                AutomaticAnalysis: result.IsActive ? "Active" : "Inactive",
                ManualFlag: spectrum.UserSelection));
        }

        return rows;
    }
}
