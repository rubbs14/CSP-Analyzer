using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SkiaSharp;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10: backs ResultsWindow, the port of CSPv2/FormOutputTable.cs. Built
/// fresh from the reference/dataset/run-results snapshot MainViewModel
/// passes in when the window is opened - no back-reference to
/// MainViewModel, so this is independently constructible and (mechanically)
/// testable without a live MainViewModel or Window.
/// </summary>
public partial class ResultsViewModel : ViewModelBase
{
    // CSPv2/FormOutputTable.cs's SolidColorBrush fields use
    // System.Windows.Media.Color.FromArgb(a, r, g, b); SkiaSharp's SKColor
    // constructor is (r, g, b, a) - reordered here, not a color change.
    private static readonly SKColor ActiveAutoColor = new(45, 161, 63, 200);
    private static readonly SKColor InactiveAutoColor = new(225, 9, 20, 180);
    private static readonly SKColor ActiveManualColor = new(123, 217, 157, 200);
    private static readonly SKColor InactiveManualColor = new(199, 137, 137, 180);
    private static readonly SKColor NotSetManualColor = new(178, 178, 178, 180);

    private readonly IFilePickerService _filePicker;
    private readonly PeaklistSpectrum _reference;
    private readonly IReadOnlyList<PeaklistSpectrum> _datasetSpectra;
    private readonly IReadOnlyList<SpectrumResult> _runResults;

    public ObservableCollection<ResultRow> Rows { get; } = new();

    [ObservableProperty]
    private int _totalExperiments;

    [ObservableProperty]
    private int _activesAuto;

    [ObservableProperty]
    private int _inactivesAuto;

    [ObservableProperty]
    private int _activesManual;

    [ObservableProperty]
    private int _inactivesManual;

    [ObservableProperty]
    private int _notSetManual;

    [ObservableProperty]
    private ISeries[] _overviewSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _autoSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _manualSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private string _exportStatusText = "";

    public ResultsViewModel(
        IFilePickerService filePicker,
        PeaklistSpectrum reference,
        IReadOnlyList<PeaklistSpectrum> datasetSpectra,
        IReadOnlyList<SpectrumResult> runResults)
    {
        _filePicker = filePicker;
        _reference = reference;
        _datasetSpectra = datasetSpectra;
        _runResults = runResults;
        Rebuild();
    }

    [RelayCommand]
    private void Refresh() => Rebuild();

    private static bool _fontResolverInitialized;

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        string? path = await _filePicker.PickSaveFileAsync("csp_results.csv", "csv");
        if (path is null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Name,Dataset,Total Read Peaks,Min Intensity (AU),Max Intensity (AU),Peak Difference to Reference,Probability,Automatic Analysis,Manual Flag");
        foreach (ResultRow row in Rows)
        {
            sb.AppendLine(string.Join(",",
                CsvField(row.Name), CsvField(row.Dataset), row.TotalReadPeaks.ToString(CultureInfo.InvariantCulture),
                row.MinIntensity.ToString(CultureInfo.InvariantCulture), row.MaxIntensity.ToString(CultureInfo.InvariantCulture),
                row.PeakDifference?.ToString(CultureInfo.InvariantCulture) ?? "none",
                row.Probability?.ToString(CultureInfo.InvariantCulture) ?? "none",
                CsvField(row.AutomaticAnalysis ?? "none"),
                CsvField(row.ManualFlag)));
        }

        await File.WriteAllTextAsync(path, sb.ToString());
        ExportStatusText = $"Exported CSV to {path}";
    }

    private static string CsvField(string value) => value.Contains(',') ? $"\"{value}\"" : value;

    [RelayCommand]
    private async Task ExportXlsxAsync()
    {
        string? path = await _filePicker.PickSaveFileAsync("csp_results.xlsx", "xlsx");
        if (path is null)
        {
            return;
        }

        await Task.Run(() => WriteXlsx(path));
        ExportStatusText = $"Exported XLSX to {path}";
    }

    private void WriteXlsx(string path)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add("CSP_Output");

        string[] headers =
        {
            "Name", "Dataset", "Total Read Peaks", "Min Intensity (AU)", "Max Intensity (AU)",
            "Peak Difference to Reference", "Probability", "Automatic Analysis", "Manual Flag",
        };
        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        int rowIndex = 2;
        foreach (ResultRow row in Rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.Name;
            sheet.Cell(rowIndex, 2).Value = row.Dataset;
            sheet.Cell(rowIndex, 3).Value = row.TotalReadPeaks;
            sheet.Cell(rowIndex, 4).Value = row.MinIntensity;
            sheet.Cell(rowIndex, 5).Value = row.MaxIntensity;
            sheet.Cell(rowIndex, 6).Value = row.PeakDifference?.ToString(CultureInfo.InvariantCulture) ?? "none";
            sheet.Cell(rowIndex, 7).Value = row.Probability?.ToString(CultureInfo.InvariantCulture) ?? "none";
            sheet.Cell(rowIndex, 8).Value = row.AutomaticAnalysis ?? "none";
            sheet.Cell(rowIndex, 9).Value = row.ManualFlag;
            rowIndex++;
        }

        workbook.SaveAs(path);
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        string? path = await _filePicker.PickSaveFileAsync("csp_results.pdf", "pdf");
        if (path is null)
        {
            return;
        }

        await Task.Run(() => WritePdf(path));
        ExportStatusText = $"Exported PDF to {path}";
    }

    private void WritePdf(string path)
    {
        if (!_fontResolverInitialized)
        {
            string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "DejaVuSans.ttf");
            GlobalFontSettings.FontResolver = new DejaVuFontResolver(File.ReadAllBytes(fontPath));
            _fontResolverInitialized = true;
        }

        string[] headers = { "Name", "Dataset", "Peaks", "Min Int.", "Max Int.", "Peak Diff", "Probability", "Auto", "Manual" };
        double[] columnWidths = { 60, 150, 50, 55, 55, 55, 65, 55, 90 };
        var titleFont = new XFont("DejaVu Sans", 16, XFontStyleEx.Bold);
        var headerFont = new XFont("DejaVu Sans", 9, XFontStyleEx.Bold);
        var cellFont = new XFont("DejaVu Sans", 9, XFontStyleEx.Regular);

        using var document = new PdfDocument();
        PdfPage page = NewLandscapePage(document);
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double y = DrawPageHeader(gfx, titleFont, headerFont, headers, columnWidths);

        foreach (ResultRow row in Rows)
        {
            if (y > page.Height.Point - 40)
            {
                gfx.Dispose();
                page = NewLandscapePage(document);
                gfx = XGraphics.FromPdfPage(page);
                y = DrawPageHeader(gfx, titleFont, headerFont, headers, columnWidths);
            }

            double x = 20;
            string[] cells =
            {
                row.Name, row.Dataset, row.TotalReadPeaks.ToString(CultureInfo.InvariantCulture),
                row.MinIntensity.ToString("F0", CultureInfo.InvariantCulture), row.MaxIntensity.ToString("F0", CultureInfo.InvariantCulture),
                row.PeakDifference?.ToString(CultureInfo.InvariantCulture) ?? "none",
                row.Probability?.ToString(CultureInfo.InvariantCulture) ?? "none",
                row.AutomaticAnalysis ?? "none", row.ManualFlag,
            };
            for (int i = 0; i < cells.Length; i++)
            {
                gfx.DrawString(cells[i], cellFont, XBrushes.Black, new XRect(x, y, columnWidths[i], 16), XStringFormats.CenterLeft);
                x += columnWidths[i];
            }
            y += 16;
        }

        gfx.Dispose();
        document.Save(path);
    }

    private static PdfPage NewLandscapePage(PdfDocument document)
    {
        PdfPage page = document.AddPage();
        page.Orientation = PdfSharp.PageOrientation.Landscape;
        return page;
    }

    private static double DrawPageHeader(XGraphics gfx, XFont titleFont, XFont headerFont, string[] headers, double[] columnWidths)
    {
        gfx.DrawString("CSP Analysis Report", titleFont, XBrushes.Black, new XPoint(20, 30));
        gfx.DrawString(DateTime.Now.ToString("f"), headerFont, XBrushes.Black, new XPoint(20, 48));

        double x = 20;
        const double y = 70;
        for (int i = 0; i < headers.Length; i++)
        {
            gfx.DrawString(headers[i], headerFont, XBrushes.Black, new XRect(x, y, columnWidths[i], 18), XStringFormats.CenterLeft);
            x += columnWidths[i];
        }
        return y + 20;
    }

    private void Rebuild()
    {
        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(_reference, _datasetSpectra, _runResults);

        Rows.Clear();
        foreach (ResultRow row in rows)
        {
            Rows.Add(row);
        }

        TotalExperiments = Rows.Count - 1;
        ActivesAuto = Rows.Count(r => r.AutomaticAnalysis == "Active");
        InactivesAuto = Rows.Count(r => r.AutomaticAnalysis == "Inactive");
        ActivesManual = Rows.Count(r => r.ManualFlag == "ACTIVE (MAN)");
        InactivesManual = Rows.Count(r => r.ManualFlag == "INACTIVE (MAN)");
        NotSetManual = Rows.Count(r => r.ManualFlag == "Not set");

        OverviewSeries = new ISeries[]
        {
            PieSlice("Actives", ActivesAuto, ActiveAutoColor),
            PieSlice("Inactives", InactivesAuto, InactiveAutoColor),
            PieSlice("Manual: Not set", NotSetManual, NotSetManualColor),
            PieSlice("Manual: Actives", ActivesManual, ActiveManualColor),
            PieSlice("Manual: Inactives", InactivesManual, InactiveManualColor),
        };

        AutoSeries = new ISeries[]
        {
            PieSlice("Actives", ActivesAuto, ActiveAutoColor),
            PieSlice("Inactives", InactivesAuto, InactiveAutoColor),
        };

        ManualSeries = new ISeries[]
        {
            PieSlice("Manual: Actives", ActivesManual, ActiveManualColor),
            PieSlice("Manual: Inactives", InactivesManual, InactiveManualColor),
            PieSlice("Manual: Not set", NotSetManual, NotSetManualColor),
        };
    }

    private static PieSeries<int> PieSlice(string name, int value, SKColor color) => new()
    {
        Name = name,
        Values = new[] { value },
        Fill = new SolidColorPaint(color),
    };
}
