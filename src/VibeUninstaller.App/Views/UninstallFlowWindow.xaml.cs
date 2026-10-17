using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using VibeUninstaller.App.ViewModels;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.App.Views;

public partial class UninstallFlowWindow : Window
{
    public UninstallFlowWindow(InstalledApp app)
    {
        ViewModel = new UninstallFlowViewModel(app);
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.Log.CollectionChanged += OnLogChanged;
    }

    public UninstallFlowViewModel ViewModel { get; }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        await ViewModel.StartAsync();
    }

    private void CancelOperation_Click(object sender, RoutedEventArgs e) => ViewModel.CancelCommand.Execute(null);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ViewModel.SelectAllCommand.Execute(null);

    private void SelectNone_Click(object sender, RoutedEventArgs e) => ViewModel.SelectNoneCommand.Execute(null);

    private void SelectRecommended_Click(object sender, RoutedEventArgs e) => ViewModel.SelectRecommendedCommand.Execute(null);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (ViewModel.State is FlowState.Running)
        {
            var answer = MessageBox.Show(
                this,
                "An uninstall is still running. Cancel it and close the window?\n\nThe application may be left partially removed.",
                "VibeUninstaller",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            ViewModel.CancelCommand.Execute(null);
        }

        base.OnClosing(e);
    }
}
