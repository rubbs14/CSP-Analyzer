using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace CspAnalyzer.Desktop.Views;

public partial class ResultsWindow : Window
{
    public ICommand CloseCommand { get; }

    public ResultsWindow()
    {
        InitializeComponent();

        CloseCommand = new RelayCommand(Close);

        // Added here rather than in the <Window.KeyBindings> XAML block -
        // see the comment above that block in ResultsWindow.axaml for why:
        // RelativeSource=Self bindings on KeyBinding.Command don't resolve
        // since KeyBinding isn't part of the visual tree.
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Q, KeyModifiers.Control), Command = CloseCommand });
    }
}
