namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Reads one peaklist.xml file into a PeaklistSpectrum, combining
/// PeaklistPathInfo (dataset name/experiment number from the path) and
/// PeaklistXmlParser (the Peak2D transform). This is the direct port of
/// CSPv2/Form1.cs's per-file call `read_spectrum.Read_spectrum(path, ...)`.
/// </summary>
public static class PeaklistImporter
{
    public static PeaklistSpectrum Import(string peaklistXmlPath, PeakImportFilter filter, string jsonData)
    {
        var (dsName, expNumber) = PeaklistPathInfo.Resolve(peaklistXmlPath);
        string xmlContent = File.ReadAllText(peaklistXmlPath);
        var (peaks, totalPeaksFound, ppInfo) = PeaklistXmlParser.Parse(xmlContent, filter, expNumber);

        return new PeaklistSpectrum
        {
            JsonData = jsonData,
            ExpNumber = expNumber,
            DsName = dsName,
            PpInfo = ppInfo,
            TotPeaks = totalPeaksFound,
            TotReadPeaks = peaks.Count,
            Peaklist = peaks,
        };
    }
}
