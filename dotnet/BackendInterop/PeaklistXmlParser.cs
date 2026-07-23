using System.Globalization;
using System.Xml.Linq;

namespace CspAnalyzer.BackendInterop;

/// <summary>Import range/threshold, mirrors Form1.cs's LoadParameters fields.</summary>
public sealed record PeakImportFilter(
    double IntensityThreshold,
    double NMin,
    double NMax,
    double HMin,
    double HMax);

/// <summary>
/// Parses a peaklist.xml document's Peak2D elements, ported from
/// CSPv2/Form1.cs's SPECTRUM.Read_spectrum. Pure string-in, no file I/O, so
/// it's testable without touching disk - PeaklistImporter adds the file
/// read + path-derived dataset name/experiment number on top.
/// </summary>
public static class PeaklistXmlParser
{
    public static (List<Peak> Peaks, int TotalPeaksFound, string PpInfo) Parse(
        string xmlContent, PeakImportFilter filter, int expNumber)
    {
        var doc = XDocument.Parse(xmlContent);
        var peak2Ds = doc.Descendants("Peak2D").ToList();

        var ppInfoNode = doc.Descendants("PeakPickDetails").FirstOrDefault();
        string ppInfo = ppInfoNode?.Value ?? "Peak Picking Info not available";

        var peaks = new List<Peak>();
        foreach (var element in peak2Ds)
        {
            double f1 = double.Parse(element.Attribute("F1")!.Value, CultureInfo.InvariantCulture);
            double f2 = double.Parse(element.Attribute("F2")!.Value, CultureInfo.InvariantCulture);
            double intensity = double.Parse(element.Attribute("intensity")!.Value, CultureInfo.InvariantCulture);

            bool withinImportLimits = f1 >= filter.NMin && f1 <= filter.NMax
                && f2 >= filter.HMin && f2 <= filter.HMax
                && intensity >= filter.IntensityThreshold;
            if (!withinImportLimits)
            {
                continue;
            }

            peaks.Add(new Peak
            {
                F1 = Math.Round(f1, 5),
                F2 = Math.Round(f2, 5),
                Intensity = Math.Round(intensity, 0),
                Number = peaks.Count + 1,
                ExpIdentifier = expNumber.ToString(CultureInfo.InvariantCulture),
            });
        }

        return (peaks, peak2Ds.Count, ppInfo);
    }
}
