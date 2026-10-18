using VibeUninstaller.App.ViewModels;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

public class BatchUninstallViewModelTests
{
    [Fact]
    public void Queues_every_application_in_order()
    {
        var sevenZip = TestData.SevenZip();
        var msi = TestData.MsiApp();

        var viewModel = new BatchUninstallViewModel(new[] { sevenZip, msi });

        Assert.Equal(2, viewModel.Items.Count);
        Assert.Same(sevenZip, viewModel.Items[0].App);
        Assert.Same(msi, viewModel.Items[1].App);
        Assert.All(viewModel.Items, item => Assert.Equal(BatchItemState.Queued, item.State));
        Assert.All(viewModel.Items, item => Assert.Equal("Queued", item.StatusText));
        Assert.Equal("2 applications selected", viewModel.HeaderText);
        Assert.Contains("2 applications", viewModel.StartButtonText, StringComparison.Ordinal);
    }

    [Fact]
    public void Starts_in_the_confirmation_state_without_changes()
    {
        var viewModel = new BatchUninstallViewModel(new[] { TestData.SevenZip() });

        Assert.Equal(BatchState.Confirm, viewModel.State);
        Assert.Equal("Confirm", viewModel.StateName);
        Assert.True(viewModel.CanClose);
        Assert.False(viewModel.SomethingChanged);
        Assert.Empty(viewModel.Leftovers);
        Assert.True(viewModel.UseSilent);
        Assert.True(viewModel.ScanLeftovers);
        Assert.Equal("1 application selected", viewModel.HeaderText);
    }

    [Fact]
    public void Knows_which_installers_support_silent_switches()
    {
        var viewModel = new BatchUninstallViewModel(new[] { TestData.MsiApp() });

        Assert.True(viewModel.Items[0].CanUseSilent);
        Assert.Equal("Contoso Ltd", viewModel.Items[0].Publisher);
    }

    [Fact]
    public void Selection_commands_cover_the_review_tiers()
    {
        var viewModel = new BatchUninstallViewModel(new[] { TestData.SevenZip() });
        viewModel.Leftovers.Add(MakeLeftover(RiskTier.Safe, 100));
        viewModel.Leftovers.Add(MakeLeftover(RiskTier.Moderate, 200));
        viewModel.Leftovers.Add(MakeLeftover(RiskTier.Advanced, 400));

        viewModel.SelectAllCommand.Execute(null);
        Assert.Equal(3, viewModel.SelectedCount);
        Assert.Equal(700, viewModel.SelectedBytes);

        viewModel.SelectNoneCommand.Execute(null);
        Assert.Equal(0, viewModel.SelectedCount);
        Assert.Equal(0, viewModel.SelectedBytes);

        // Advanced items are advisory: "recommended" must skip them.
        viewModel.SelectRecommendedCommand.Execute(null);
        Assert.Equal(2, viewModel.SelectedCount);
        Assert.Equal(300, viewModel.SelectedBytes);
    }

    [Fact]
    public void Running_state_blocks_closing_until_the_batch_stops()
    {
        var viewModel = new BatchUninstallViewModel(new[] { TestData.SevenZip() });

        viewModel.State = BatchState.Running;
        Assert.False(viewModel.CanClose);
        Assert.Equal("Running", viewModel.StateName);

        viewModel.State = BatchState.Done;
        Assert.True(viewModel.CanClose);
    }

    [Fact]
    public void Cancel_before_the_run_is_a_no_op()
    {
        var viewModel = new BatchUninstallViewModel(new[] { TestData.SevenZip() });

        viewModel.CancelCommand.Execute(null);

        Assert.Equal(BatchState.Confirm, viewModel.State);
        Assert.All(viewModel.Items, item => Assert.Equal(BatchItemState.Queued, item.State));
    }

    private static LeftoverItemViewModel MakeLeftover(RiskTier tier, long size) =>
        new(new LeftoverItem
        {
            Path = $@"C:\ProgramData\Test\{tier}.dat",
            Kind = SystemItemKind.File,
            Tier = tier,
            Score = tier == RiskTier.Safe ? 1.0 : 0.5,
            Reason = "synthetic leftover",
            SizeBytes = size,
        });
}
