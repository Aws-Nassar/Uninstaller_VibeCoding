using System.Windows;
using VibeUninstaller.App.ViewModels;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.App.Views;

public partial class ScanPreviewWindow : Window
{
    private readonly CancellationTokenSource _cancellation = new();

    public ScanPreviewWindow(InstalledApp app)
    {
        InitializeComponent();
        ViewModel = new ScanPreviewViewModel(app);
        DataContext = ViewModel;

        // ContentRendered (not Loaded) so building the window never starts I/O by itself.
        ContentRendered += async (_, _) => await ViewModel.ScanAsync(_cancellation.Token);
        Closed += (_, _) => _cancellation.Cancel();
    }

    public ScanPreviewViewModel ViewModel { get; }

    /// <summary>True when the user ran the uninstaller from here and the system changed.</summary>
    public bool SomethingChanged { get; private set; }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RunUninstaller_Click(object sender, RoutedEventArgs e)
    {
        var flow = new UninstallFlowWindow(ViewModel.App) { Owner = this };
        flow.ShowDialog();
        SomethingChanged = flow.ViewModel.SomethingChanged;
        Close();
    }
}
