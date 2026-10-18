using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

    /// <summary>Keeps the multi-selection in the view model for batch actions.</summary>
    private void AppsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SetSelection(AppsGrid.SelectedItems.OfType<AppItemViewModel>());

    /// <summary>
    /// Right-clicking a row keeps an existing multi-selection intact; clicking a
    /// row outside the selection selects just that row so the context menu is clear.
    /// </summary>
    private void Row_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && !row.IsSelected)
        {
            AppsGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    /// <summary>Left-clicking the theme button opens its theme list.</summary>
    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e) => _ = UninstallAsync(ViewModel.SelectedApp);

    private async void UninstallSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedApps;
        if (selected.Count == 0)
        {
            ShowSelectHint();
            return;
        }

        if (selected.Count == 1)
        {
            await UninstallAsync(selected[0]);
            return;
        }

        var window = new BatchUninstallWindow(selected.Select(item => item.App).ToList()) { Owner = this };
        window.ShowDialog();

        if (window.ViewModel.SomethingChanged)
        {
            await ViewModel.RefreshAsync();
        }
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedApp is not { } item)
        {
            ShowSelectHint();
            return;
        }

        var preview = new ScanPreviewWindow(item.App) { Owner = this };
        preview.ShowDialog();

        if (preview.SomethingChanged)
        {
            await ViewModel.RefreshAsync();
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenInstallFolderCommand.Execute(null);

    private void CopyCommand_Click(object sender, RoutedEventArgs e)
    {
        var command = ViewModel.SelectedApp?.App.UninstallString;
        if (string.IsNullOrWhiteSpace(command))
        {
            command = ViewModel.SelectedApp?.App.QuietUninstallString;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        try
        {
            Clipboard.SetText(command);
        }
        catch (Exception)
        {
            // Another process can hold the clipboard open; not worth failing over.
        }
    }

    private async void AppsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        await UninstallAsync(ViewModel.SelectedApp);

    private async Task UninstallAsync(AppItemViewModel? item)
    {
        if (item is null)
        {
            ShowSelectHint();
            return;
        }

        var flow = new UninstallFlowWindow(item.App) { Owner = this };
        flow.ShowDialog();

        if (flow.ViewModel.SomethingChanged)
        {
            await ViewModel.RefreshAsync();
        }
    }

    private void ShowSelectHint() =>
        MessageBox.Show(this, "Select an application from the list first.", "VibeUninstaller",
            MessageBoxButton.OK, MessageBoxImage.Information);
}
