using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
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
