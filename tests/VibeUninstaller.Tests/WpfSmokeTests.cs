using System.Windows;
using VibeUninstaller.App;
using VibeUninstaller.App.ViewModels;
using VibeUninstaller.App.Views;
using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

/// <summary>
/// Loading XAML is the only way to catch missing StaticResource keys, wrong
/// converter types and broken template bindings, so this test really loads the
/// application resources and constructs both windows on an STA thread.
/// </summary>
public class WpfSmokeTests
{
    [Fact]
    public void Windows_load_with_all_their_resources()
    {
        Exception? failure = null;
        string stage = "start";
        UninstallFlowViewModel? flowViewModel = null;

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
                main.Close();

                stage = "UninstallFlowWindow";
                var flow = new UninstallFlowWindow(TestData.SevenZip());
                flowViewModel = flow.ViewModel;

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
    }
}
