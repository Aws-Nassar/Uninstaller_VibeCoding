using System.Windows;
using System.Windows.Input;
using VibeUninstaller.App.ViewModels;
using VibeUninstaller.App.Views;

namespace VibeUninstaller.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedApp is { } selected)
        {
            _ = ShowUninstallFlowAsync(selected);
        }
        else
        {
            MessageBox.Show(this, "Select an application from the list first.", "VibeUninstaller",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => ViewModel.OpenInstallFolderCommand.Execute(null);

    private async void AppsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.SelectedApp is { } selected)
        {
            await ShowUninstallFlowAsync(selected);
        }
    }

    private async Task ShowUninstallFlowAsync(AppItemViewModel item)
    {
        var flow = new UninstallFlowWindow(item.App) { Owner = this };
        flow.ShowDialog();

        if (flow.ViewModel.SomethingChanged)
        {
            await ViewModel.RefreshAsync();
        }
    }
}
