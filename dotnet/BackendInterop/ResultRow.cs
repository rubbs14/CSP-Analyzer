namespace CspAnalyzer.BackendInterop;

/// <summary>
/// One row of the S10 results table - mirrors CSPv2/FormOutputTable.cs's
/// GenerateTable columns exactly (Name/Dataset/Total Read Peaks/Min-Max
/// Intensity/Peak Difference/Probability/Automatic Analysis/Manual Flag).
/// The reference row (built directly by ResultsBuilder.Build, not joined)
/// leaves PeakDifference/Probability/AutomaticAnalysis null, matching the
/// old table's literal "none" for that row.
/// </summary>
public sealed record ResultRow(
    string Name,
    string Dataset,
    int TotalReadPeaks,
    double MinIntensity,
    double MaxIntensity,
    int? PeakDifference,
    double? Probability,
    string? AutomaticAnalysis,
    string ManualFlag);
