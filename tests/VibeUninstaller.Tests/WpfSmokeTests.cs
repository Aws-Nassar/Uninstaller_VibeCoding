using System.Windows;
using System.Windows.Media;
using VibeUninstaller.App;
using VibeUninstaller.App.Services;
using VibeUninstaller.App.ViewModels;
using VibeUninstaller.App.Views;
using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

/// <summary>
/// Loading XAML is the only way to catch missing StaticResource keys, wrong
/// converter types and broken template bindings, so this test really loads the
/// application resources and constructs every window on an STA thread. It also
/// swaps every theme to prove that the dictionaries parse and that a live
/// switch reaches a loaded window.
/// </summary>
public class WpfSmokeTests
{
    [Fact]
    public void Windows_load_with_all_their_resources()
    {
        Exception? failure = null;
        string stage = "start";
        UninstallFlowViewModel? flowViewModel = null;
        BatchUninstallViewModel? batchViewModel = null;

        var thread = new Thread(() =>
        {
            try
            {
                stage = "application resources";
                var application = new App.App();
                application.InitializeComponent();

                stage = "theme resources";
                if (Application.Current?.TryFindResource("TitleText") is null)
                {
                    throw new InvalidOperationException("The dark theme did not load.");
                }

                stage = "MainWindow";
                var main = new MainWindow();

                stage = "theme switching";
                foreach (var option in ThemeCatalog.All)
                {
                    ThemeManager.Apply(application.Resources, option.Id, persist: false);
                    if (application.TryFindResource("PanelBackground") is not SolidColorBrush)
                    {
                        throw new InvalidOperationException($"Theme '{option.Id}' is missing a brush.");
                    }

                    if (main.Background is not SolidColorBrush windowBackground)
                    {
                        throw new InvalidOperationException($"The window lost its background in '{option.Id}'.");
                    }

                    var expected = ((SolidColorBrush)application.Resources["AppBackground"]).Color;
                    if (windowBackground.Color != expected)
                    {
                        throw new InvalidOperationException(
                            $"Window.Background ({windowBackground.Color}) ignored theme '{option.Id}' ({expected}).");
                    }
                }

                // Leave the default theme active for the rest of the stages.
                ThemeManager.Apply(application.Resources, ThemeCatalog.DefaultId, persist: false);
                main.Close();

                stage = "UninstallFlowWindow";
                var flow = new UninstallFlowWindow(TestData.SevenZip());
                flowViewModel = flow.ViewModel;

                stage = "ScanPreviewWindow";
                var preview = new ScanPreviewWindow(TestData.SevenZip());
                preview.Close();

                stage = "BatchUninstallWindow";
                var batch = new BatchUninstallWindow(new[] { TestData.SevenZip(), TestData.MsiApp() });
                batchViewModel = batch.ViewModel;
                batch.Close();

                stage = "review state with leftovers";
                flow.ViewModel.State = FlowState.Review;
                flow.ViewModel.Leftovers.Add(new LeftoverItemViewModel(new LeftoverItem
                {
                    Path = @"C:\ProgramData\7-Zip\settings.ini",
                    Kind = SystemItemKind.File,
                    Tier = RiskTier.Safe,
                    Score = 1.0,
                    Reason = "Name matches the app",
                    SizeBytes = 1024,
                }));

                flow.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "the UI thread did not finish");
        Assert.True(failure is null, $"XAML failed while loading '{stage}': {failure}");
        Assert.NotNull(flowViewModel);
        Assert.Equal(FlowState.Review, flowViewModel!.State);
        Assert.Single(flowViewModel.Leftovers);
        Assert.NotNull(batchViewModel);
        Assert.Equal(2, batchViewModel!.Items.Count);
        Assert.Equal(ThemeCatalog.DefaultId, ThemeManager.Current.Id);
    }
}
