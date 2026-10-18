using System.Collections.Generic;
using System.Windows;
using VibeUninstaller.App.ViewModels;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.App.Views;

/// <summary>Window that runs a batch uninstall for several applications.</summary>
public partial class BatchUninstallWindow : Window
{
    public BatchUninstallWindow(IReadOnlyList<InstalledApp> apps)
    {
        InitializeComponent();
        ViewModel = new BatchUninstallViewModel(apps);
        DataContext = ViewModel;
    }

    public BatchUninstallViewModel ViewModel { get; }

    private async void Start_Click(object sender, RoutedEventArgs e) => await ViewModel.StartAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel.CancelCommand.Execute(null);

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ViewModel.SelectAllCommand.Execute(null);

    private void SelectNone_Click(object sender, RoutedEventArgs e) => ViewModel.SelectNoneCommand.Execute(null);

    private void SelectRecommended_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectRecommendedCommand.Execute(null);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
