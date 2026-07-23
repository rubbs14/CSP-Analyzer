using System.Text.Json;
using System.Text.Json.Serialization;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// One spectrum's worth of imported peaks - the input-side counterpart of
/// SpectrumResult. Property names mirror the JSON keys backend/io.py:
/// json_parser reads by exact key (JSON_Data, EXP_NUMBER, PEAKLIST), ported
/// from CSPv2/Form1.cs's SPECTRUM class. Only the fields the import step
/// (Read_spectrum) actually populates are included - isActive/Prob/PEAK_DIFF
/// are classification-result fields Form1 filled in later and aren't needed
/// for the xml-&gt;json transform.
/// </summary>
public sealed class PeaklistSpectrum
{
    [JsonPropertyName("JSON_Data")]
    public string JsonData { get; set; } = "";

    [JsonPropertyName("EXP_NUMBER")]
    public int ExpNumber { get; set; }

    [JsonPropertyName("DS_NAME")]
    public string DsName { get; set; } = "";

    [JsonPropertyName("PP_INFO")]
    public string PpInfo { get; set; } = "";

    [JsonPropertyName("TOT_PEAKS")]
    public int TotPeaks { get; set; }

    [JsonPropertyName("TOT_READ_PEAKS")]
    public int TotReadPeaks { get; set; }

    [JsonPropertyName("UserSelection")]
    public string UserSelection { get; set; } = "Not set";

    [JsonPropertyName("PEAKLIST")]
    public List<Peak> Peaklist { get; set; } = new();

    public static string SerializeAll(IEnumerable<PeaklistSpectrum> spectra) =>
        JsonSerializer.Serialize(spectra);
}
