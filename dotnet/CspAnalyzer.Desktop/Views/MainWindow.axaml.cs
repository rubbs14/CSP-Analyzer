using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using CspAnalyzer.Desktop.ViewModels;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView.Avalonia;

namespace CspAnalyzer.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        this.FindControl<CartesianChart>("PeakDiffChart")!.ChartPointPointerDown += OnChartPointClicked;
        this.FindControl<CartesianChart>("ProbabilityChart")!.ChartPointPointerDown += OnChartPointClicked;
    }

    private void OnChartPointClicked(IChartView chart, ChartPoint point)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.NavigateToChartIndex(point.Index);
        }
    }

    private void OnThemeLightClick(object? sender, RoutedEventArgs e) =>
        (Application.Current ?? throw new InvalidOperationException()).RequestedThemeVariant = ThemeVariant.Light;

    private void OnThemeDarkClick(object? sender, RoutedEventArgs e) =>
        (Application.Current ?? throw new InvalidOperationException()).RequestedThemeVariant = ThemeVariant.Dark;

    private void OnThemeSystemClick(object? sender, RoutedEventArgs e) =>
        (Application.Current ?? throw new InvalidOperationException()).RequestedThemeVariant = ThemeVariant.Default;

    private void OnBackgroundColorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            Background = new SolidColorBrush(Color.Parse(hex));
        }
    }

    private void OnBackgroundColorResetClick(object? sender, RoutedEventArgs e) => ClearValue(BackgroundProperty);
}
