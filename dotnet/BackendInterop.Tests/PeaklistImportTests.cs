using System.Globalization;
using System.Text.Json;
using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

public class PeaklistSpectrumJsonTests
{
    [Fact]
    public void SerializeAll_uses_the_exact_keys_backend_io_json_parser_reads()
    {
        // Mirrors backend/io.py:json_parser's expected shape - it reads
        // spectrum["JSON_Data"], spectrum["EXP_NUMBER"], spectrum["PEAKLIST"]
        // and peak["F1"]/["F2"]/["INTENSITY"] by exact key.
        var spectrum = new PeaklistSpectrum
        {
            JsonData = "Reference",
            ExpNumber = 11,
            DsName = "gpHUB1_FR_REF_pool1_130416",
            PpInfo = "some info",
            TotPeaks = 2,
            TotReadPeaks = 1,
            Peaklist = { new Peak { Number = 1, F1 = 121.33, F2 = 7.95, Intensity = 23499, ExpIdentifier = "11" } },
        };

        string json = PeaklistSpectrum.SerializeAll(new[] { spectrum });
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement[0];

        Assert.Equal("Reference", element.GetProperty("JSON_Data").GetString());
        Assert.Equal(11, element.GetProperty("EXP_NUMBER").GetInt32());
        var peak = element.GetProperty("PEAKLIST")[0];
        Assert.Equal(121.33, peak.GetProperty("F1").GetDouble(), precision: 5);
        Assert.Equal(7.95, peak.GetProperty("F2").GetDouble(), precision: 5);
        Assert.Equal(23499, peak.GetProperty("INTENSITY").GetDouble(), precision: 5);
    }
}

public class PeaklistPathInfoTests
{
    [Fact]
    public void Resolve_extracts_dataset_name_and_experiment_number_from_a_pdata_path()
    {
        // Real Demo-dataset layout: <dsname>/<expNo>/pdata/1/peaklist.xml
        string path = Path.Combine("gpHUB1_FR_REF_pool1_130416", "11", "pdata", "1", "peaklist.xml");

        var (dsName, expNumber) = PeaklistPathInfo.Resolve(path);

        Assert.Equal("gpHUB1_FR_REF_pool1_130416", dsName);
        Assert.Equal(11, expNumber);
    }

    [Fact]
    public void Resolve_throws_when_path_has_no_pdata_segment()
    {
        string path = Path.Combine("some", "unrelated", "path.xml");

        Assert.Throws<FormatException>(() => PeaklistPathInfo.Resolve(path));
    }
}

public class PeaklistXmlParserTests
{
    private const string SampleXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <PeakList modified="2018-02-23T12:32:02">
          <PeakList2D>
            <PeakList2DHeader expNo="2" name="gpHUB1_FR_REF_pool1_130416" procNo="1">
              <PeakPickDetails># F2 peak picking range ( ppm )
        # MI=0.00918564, MAXI=1.0</PeakPickDetails>
            </PeakList2DHeader>
            <Peak2D F1="121.3322" F2="7.9486" intensity="23498.62" type="0"/>
            <Peak2D F1="90.0000" F2="7.9486" intensity="23561.75" type="0"/>
            <Peak2D F1="121.9645" F2="8.3867" intensity="100.00" type="0"/>
          </PeakList2D>
        </PeakList>
        """;

    private static readonly PeakImportFilter FullRangeFilter = new(
        IntensityThreshold: 0, NMin: 100, NMax: 130, HMin: 0, HMax: 20);

    [Fact]
    public void Parse_keeps_only_peaks_within_the_N_H_range_and_intensity_threshold()
    {
        var (peaks, totalFound, _) = PeaklistXmlParser.Parse(SampleXml, FullRangeFilter, expNumber: 11);

        // Peak at F1=90.0 falls outside NMin=100..NMax=130, must be dropped.
        Assert.Equal(3, totalFound);
        Assert.Equal(2, peaks.Count);
        Assert.All(peaks, p => Assert.InRange(p.F1, 100, 130));
    }

    [Fact]
    public void Parse_drops_peaks_below_the_intensity_threshold()
    {
        var filter = FullRangeFilter with { IntensityThreshold = 1000 };

        var (peaks, _, _) = PeaklistXmlParser.Parse(SampleXml, filter, expNumber: 11);

        // Only the intensity=100.00 peak is excluded by the threshold (also within N range).
        Assert.Single(peaks);
        Assert.Equal(121.3322, peaks[0].F1, precision: 4);
    }

    [Fact]
    public void Parse_rounds_F1_F2_to_5_decimals_and_intensity_to_whole_number()
    {
        var (peaks, _, _) = PeaklistXmlParser.Parse(SampleXml, FullRangeFilter, expNumber: 11);

        Assert.Equal(121.3322, peaks[0].F1, precision: 5);
        Assert.Equal(23499, peaks[0].Intensity, precision: 0);
    }

    [Fact]
    public void Parse_extracts_PeakPickDetails_text_as_PpInfo()
    {
        var (_, _, ppInfo) = PeaklistXmlParser.Parse(SampleXml, FullRangeFilter, expNumber: 11);

        Assert.Contains("MI=0.00918564", ppInfo);
    }

    [Fact]
    public void Parse_reports_a_placeholder_PpInfo_when_PeakPickDetails_is_absent()
    {
        const string xmlWithoutHeader = """
            <PeakList><PeakList2D><Peak2D F1="121.0" F2="7.0" intensity="500" type="0"/></PeakList2D></PeakList>
            """;

        var (_, _, ppInfo) = PeaklistXmlParser.Parse(xmlWithoutHeader, FullRangeFilter, expNumber: 1);

        Assert.Equal("Peak Picking Info not available", ppInfo);
    }

    [Fact]
    public void Parse_stamps_each_kept_peak_with_the_experiment_identifier()
    {
        var (peaks, _, _) = PeaklistXmlParser.Parse(SampleXml, FullRangeFilter, expNumber: 42);

        Assert.All(peaks, p => Assert.Equal("42", p.ExpIdentifier));
    }
}

public class PeaklistImporterTests
{
    private const string SampleXml = """
        <PeakList><PeakList2D>
          <PeakList2DHeader><PeakPickDetails>info</PeakPickDetails></PeakList2DHeader>
          <Peak2D F1="121.3322" F2="7.9486" intensity="23498.62" type="0"/>
        </PeakList2D></PeakList>
        """;

    [Fact]
    public void Import_reads_a_real_file_and_produces_a_fully_populated_spectrum()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "peaklist-import-test-" + Guid.NewGuid());
        string dsDir = Path.Combine(tempDir, "gpHUB1_FR_REF_pool1_130416", "11", "pdata", "1");
        Directory.CreateDirectory(dsDir);
        string xmlPath = Path.Combine(dsDir, "peaklist.xml");
        File.WriteAllText(xmlPath, SampleXml);

        try
        {
            var filter = new PeakImportFilter(IntensityThreshold: 0, NMin: 0, NMax: 200, HMin: 0, HMax: 20);
            var spectrum = PeaklistImporter.Import(xmlPath, filter, jsonData: "Reference");

            Assert.Equal("Reference", spectrum.JsonData);
            Assert.Equal("gpHUB1_FR_REF_pool1_130416", spectrum.DsName);
            Assert.Equal(11, spectrum.ExpNumber);
            Assert.Equal(1, spectrum.TotPeaks);
            Assert.Equal(1, spectrum.TotReadPeaks);
            Assert.Single(spectrum.Peaklist);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
