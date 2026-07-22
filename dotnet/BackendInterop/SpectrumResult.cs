using System.Text.Json;
using System.Text.Json.Serialization;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// One entry of processed_spectra.json, as written by
/// backend/io.py:json_constructor. `IsActive` is a plain JSON bool - the S1
/// tuple-formatting bug this used to require a regex workaround for
/// (Form1.cs:1522-1523) is fixed on the python side, so plain
/// System.Text.Json deserialization is enough.
/// </summary>
public sealed class SpectrumResult
{
    [JsonPropertyName("EXP_NUMBER")]
    public int ExpNumber { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("activePseudoprobability")]
    public double ActivePseudoprobability { get; set; }

    public static SpectrumResult[] ParseArray(string json) =>
        JsonSerializer.Deserialize<SpectrumResult[]>(json)
        ?? throw new JsonException("processed_spectra.json deserialized to null");
}
