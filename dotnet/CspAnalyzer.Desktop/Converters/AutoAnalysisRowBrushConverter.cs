using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CspAnalyzer.Desktop.Converters;

/// <summary>
/// DataGridRow background for ResultsWindow - reproduces
/// CSPv2/FormOutputTable.cs's dataGridView1_CellFormatting
/// (LightGreen/PaleVioletRed for Active/Inactive) as a per-row Background
/// instead of per-cell, since Avalonia's DataGrid styles rows more
/// naturally than WinForms' per-cell formatting event did.
/// </summary>
public sealed class AutoAnalysisRowBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "Active" => Brushes.LightGreen,
            "Inactive" => new SolidColorBrush(Color.FromRgb(219, 112, 147)), // PaleVioletRed
            _ => Brushes.Transparent,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
