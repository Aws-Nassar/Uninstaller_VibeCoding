using System.Windows;
using System.Windows.Threading;
using VibeUninstaller.App.Services;

namespace VibeUninstaller.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Apply the saved color theme before the main window is created.
        ThemeManager.ApplyInitial(Resources);
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}",
            "VibeUninstaller",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }
}
