namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Derives dataset name + experiment number from a peaklist.xml path of the
/// form .../&lt;dsname&gt;/&lt;expNo&gt;/pdata/&lt;procNo&gt;/peaklist.xml, ported from
/// CSPv2/Form1.cs's Read_spectrum "NUKE-PROOF file name fetcher".
///
/// Deliberate deviation from the original: Form1.cs parsed EXP_NUMBER via
/// `directories[l].Remove(directories[l].Length - 1)` - removing the last
/// character of the experiment folder name before parsing as int. Against
/// the real Demo-dataset (e.g. an "11" folder) that drops a digit (11 -> 1).
/// EXP_NUMBER is a display/tracking label only - never fed into the SVM
/// feature vectors - so this port parses the full folder name instead of
/// replicating what looks like an off-by-one bug with no equivalence
/// consequence.
/// </summary>
public static class PeaklistPathInfo
{
    public static (string DsName, int ExpNumber) Resolve(string peaklistXmlPath)
    {
        string[] segments = peaklistXmlPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        int pdataIndex = Array.IndexOf(segments, "pdata");
        if (pdataIndex < 2)
        {
            throw new FormatException(
                $"'{peaklistXmlPath}' does not match the expected <dataset>/<expNo>/pdata/... layout");
        }

        string dsName = segments[pdataIndex - 2];
        string expSegment = segments[pdataIndex - 1];
        if (!int.TryParse(expSegment, out int expNumber))
        {
            throw new FormatException(
                $"experiment folder '{expSegment}' in '{peaklistXmlPath}' is not a number");
        }

        return (dsName, expNumber);
    }
}
