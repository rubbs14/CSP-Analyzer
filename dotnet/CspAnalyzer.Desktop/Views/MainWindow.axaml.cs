using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using CspAnalyzer.Desktop.Models;
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

    public void ApplyAppearanceSettings(AppSettings settings)
    {
        (Application.Current ?? throw new InvalidOperationException()).RequestedThemeVariant = settings.ThemeVariant switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        if (settings.BackgroundColorHex is string hex)
        {
            Background = new SolidColorBrush(Color.Parse(hex));
        }
        else
        {
            ClearValue(BackgroundProperty);
        }

        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        if (settings.WindowX is int x && settings.WindowY is int y)
        {
            Position = new PixelPoint(x, y);
        }

        WindowState = settings.WindowState == "Maximized"
            ? Avalonia.Controls.WindowState.Maximized
            : Avalonia.Controls.WindowState.Normal;
    }

    public void PopulateAppearanceSettings(AppSettings settings)
    {
        ThemeVariant? current = Application.Current?.RequestedThemeVariant;
        settings.ThemeVariant = current == ThemeVariant.Light ? "Light"
            : current == ThemeVariant.Dark ? "Dark"
            : "System";

        settings.BackgroundColorHex = Background is SolidColorBrush brush ? brush.Color.ToString() : null;

        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
        settings.WindowX = Position.X;
        settings.WindowY = Position.Y;
        settings.WindowState = WindowState == Avalonia.Controls.WindowState.Maximized ? "Maximized" : "Normal";
    }
}
