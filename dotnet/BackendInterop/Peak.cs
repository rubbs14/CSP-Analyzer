using System.Text.Json.Serialization;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// One kept peak from a peaklist.xml. Property names mirror the JSON keys
/// backend/io.py:json_parser reads (F1/F2/INTENSITY) - see PeaklistSpectrum.
/// </summary>
public sealed class Peak
{
    [JsonPropertyName("NUMBER")]
    public int Number { get; set; }

    [JsonPropertyName("F1")]
    public double F1 { get; set; }

    [JsonPropertyName("F2")]
    public double F2 { get; set; }

    [JsonPropertyName("INTENSITY")]
    public double Intensity { get; set; }

    [JsonPropertyName("EXP_IDENTIFIER")]
    public string ExpIdentifier { get; set; } = "";
}
