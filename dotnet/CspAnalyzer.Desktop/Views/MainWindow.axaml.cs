using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.ViewModels;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView.Avalonia;

namespace CspAnalyzer.Desktop.Views;

public partial class MainWindow : Window
{
    public ICommand CloseCommand { get; }

    public ICommand FocusGoToExperimentCommand { get; }

    public MainWindow()
    {
        InitializeComponent();

        CloseCommand = new RelayCommand(Close);
        FocusGoToExperimentCommand = new RelayCommand(
            () => this.FindControl<TextBox>("GoToExperimentTextBox")!.Focus(),
            () => !IsTextBoxFocused());

        // Added here rather than in the <Window.KeyBindings> XAML block -
        // see the comment above that block in MainWindow.axaml for why.
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Q, KeyModifiers.Control), Command = CloseCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.G), Command = FocusGoToExperimentCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.R), Command = GuardedViewModelCommand(vm => vm.RunCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.N), Command = GuardedViewModelCommand(vm => vm.ResetAllManualFlagsCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D), Command = GuardedViewModelCommand(vm => vm.ResetManualStatusCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S), Command = GuardedViewModelCommand(vm => vm.MarkInactiveCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.A), Command = GuardedViewModelCommand(vm => vm.MarkActiveCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.T), Command = GuardedViewModelCommand(vm => vm.CancelRunCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Right), Command = GuardedViewModelCommand(vm => vm.NextCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Left), Command = GuardedViewModelCommand(vm => vm.PreviousCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Down), Command = GuardedViewModelCommand(vm => vm.LastCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Up), Command = GuardedViewModelCommand(vm => vm.FirstCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.C, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ResetBarChartZoomCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Y, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ResetOverlayZoomCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.X, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.FitOverlayZoomToReferenceCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.H), Command = GuardedViewModelCommand(vm => vm.OpenHelpWindowCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.A, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ToggleAutoActivesFilterCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.I, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ToggleAutoInactivesFilterCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Enter), Command = GuardedViewModelCommand(vm => vm.LoadReferenceOrDatasetCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.I), Command = GuardedViewModelCommand(vm => vm.OpenAboutWindowCommand) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.R, KeyModifiers.Control), Command = GuardedViewModelCommand(vm => vm.ResetApplicationCommand) });

        this.FindControl<CartesianChart>("PeakDiffChart")!.ChartPointPointerDown += OnChartPointClicked;
        this.FindControl<CartesianChart>("ProbabilityChart")!.ChartPointPointerDown += OnChartPointClicked;

        // LiveChartsCore's own UpdateFinished event (IChartView) never
        // fired in testing, even well after a real load+run - so this uses
        // Avalonia's own LayoutUpdated instead, which is guaranteed to run
        // after every arrange pass regardless of LiveCharts internals. The
        // chart's real draw margin only exists once its axes have actual
        // content (tick label text/rotation, which depends on the loaded
        // spectra), so a margin computed before that is stale by
        // definition - this re-measures on every layout pass instead of
        // once, so it can't be stale after data loads.
        this.FindControl<CartesianChart>("PeakDiffChart")!.LayoutUpdated += OnPeakDiffChartLayoutUpdated;
    }

    private void OnPeakDiffChartLayoutUpdated(object? sender, EventArgs e)
    {
        var chartControl = this.FindControl<CartesianChart>("PeakDiffChart");
        var overlay = this.FindControl<Grid>("PeakDiffZoneLabels");
        if (chartControl is null || overlay is null || ((IChartView)chartControl).CoreChart is not { } coreChart)
        {
            return;
        }

        LiveChartsCore.Drawing.LvcPoint location = coreChart.DrawMarginLocation;
        LiveChartsCore.Drawing.LvcSize size = coreChart.DrawMarginSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        double right = Math.Max(0, chartControl.Bounds.Width - location.X - size.Width);
        double bottom = Math.Max(0, chartControl.Bounds.Height - location.Y - size.Height);
        var margin = new Thickness(location.X, location.Y, right, bottom);
        if (overlay.Margin != margin)
        {
            overlay.Margin = margin;
        }
    }

    // See the comment above the <Window.KeyBindings> block in
    // MainWindow.axaml: bare single-letter shortcuts must not fire while
    // a TextBox has keyboard focus, so we look up the current ViewModel
    // command dynamically (DataContext isn't set yet when this runs from
    // the constructor for object-initializer callers) and AND its
    // CanExecute with "no TextBox is focused."
    private ICommand GuardedViewModelCommand(Func<MainViewModel, ICommand> select) =>
        new RelayCommand(
            () =>
            {
                if (DataContext is MainViewModel vm)
                {
                    select(vm).Execute(null);
                }
            },
            () => !IsTextBoxFocused() && DataContext is MainViewModel vm && select(vm).CanExecute(null));

    private bool IsTextBoxFocused() => FocusManager?.GetFocusedElement() is TextBox;

    private void OnChartPointClicked(IChartView chart, ChartPoint? point)
    {
        if (DataContext is MainViewModel vm && point is not null)
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
            var color = Color.Parse(hex);
            Background = new SolidColorBrush(color);

            // Most text in this app has no explicit Foreground and relies on
            // the FluentTheme default, which only follows ThemeVariant - not
            // this custom background swatch. Without this, picking a light
            // swatch while the theme stays Dark leaves the (still white)
            // default text unreadable on the new light background.
            (Application.Current ?? throw new InvalidOperationException()).RequestedThemeVariant =
                IsLightColor(color) ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }

    private static bool IsLightColor(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0 > 0.5;

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
            var color = Color.Parse(hex);
            Background = new SolidColorBrush(color);
            (Application.Current ?? throw new InvalidOperationException()).RequestedThemeVariant =
                IsLightColor(color) ? ThemeVariant.Light : ThemeVariant.Dark;
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

        // The assignment above, made before the native platform window
        // exists (App.axaml.cs calls this pre-Show()), gets silently
        // dropped on X11: the window ends up Normal at whatever small
        // default size Avalonia picked, clipping content that assumes a
        // full-size window (S12 bug report: legend text, the gauge panel
        // checkboxes, and the Go-To-Experiment button all clipped by the
        // window edge).
        //
        // Re-applying once on Opened - explicitly sizing/positioning to
        // the primary screen's working area instead of trusting
        // WindowState alone - mostly fixes it, but this X11/WM
        // combination sometimes applies its own late, asynchronous
        // "restore" resize well AFTER Opened fires - not a one-off race
        // in the first second, but observed live drifting the window back
        // down to ~1197x810 (or ~999x676 on another launch) a full 5-10+
        // seconds after startup, long after a short retry window would
        // have already stopped watching. Guarding for a generous 15
        // seconds of wall-clock time after Opened - re-asserting only
        // when the size has actually drifted - reliably outlasts however
        // late the WM's own correction lands, without fighting a
        // deliberate resize the user makes well after startup.
        if (settings.WindowState == "Maximized")
        {
            void ApplyFullScreenGeometry()
            {
                WindowState = Avalonia.Controls.WindowState.Maximized;
                if (Screens.Primary is { } screen)
                {
                    PixelRect area = screen.WorkingArea;
                    Position = new PixelPoint(area.X, area.Y);
                    Width = area.Width / screen.Scaling;
                    Height = area.Height / screen.Scaling;
                }
            }

            void SetMaximizedOnOpen(object? sender, EventArgs e)
            {
                ApplyFullScreenGeometry();
                Opened -= SetMaximizedOnOpen;

                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                var retryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                retryTimer.Tick += (_, _) =>
                {
                    if (Screens.Primary is { } screen &&
                        Math.Abs(Width - screen.WorkingArea.Width / screen.Scaling) > 1)
                    {
                        ApplyFullScreenGeometry();
                    }

                    if (DateTime.UtcNow >= deadline)
                    {
                        retryTimer.Stop();
                    }
                };
                retryTimer.Start();
            }

            Opened += SetMaximizedOnOpen;
        }
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
