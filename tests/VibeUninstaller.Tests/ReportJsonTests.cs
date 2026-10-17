using VibeUninstaller.Cli;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;

namespace VibeUninstaller.Tests;

public class ReportJsonTests
{
    [Fact]
    public void App_reports_use_camel_case_and_readable_text()
    {
        var json = ReportJson.Serialize(AppReport.From(TestData.SevenZip()));

        Assert.Contains("\"name\": \"7-Zip 24.09 (x64)\"", json);
        Assert.Contains("\"source\": \"registry\"", json);
        Assert.Contains("\"scope\": \"machine\"", json);
        Assert.Contains("\"installLocation\":", json);
        Assert.DoesNotContain("\\u002B", json);
        Assert.DoesNotContain("\\u0026", json);
    }

    [Fact]
    public void Reports_omit_absent_values()
    {
        var json = ReportJson.Serialize(AppReport.From(new InstalledApp
        {
            Id = "reg|HKCU|Thing",
            DisplayName = "Thing",
        }));

        Assert.DoesNotContain("\"version\"", json);
        Assert.DoesNotContain("\"installLocation\"", json);
    }

    [Fact]
    public void Leftover_reports_map_tiers_and_kinds()
    {
        var item = new LeftoverItem
        {
            Path = @"HKLM\SOFTWARE\7-Zip",
            Kind = SystemItemKind.RegistryKey,
            Tier = RiskTier.Safe,
            Score = 0.8333333,
            Reason = "Folder name matches the app",
            SizeBytes = null,
        };

        var report = LeftoverReport.From(item);

        Assert.Equal("registry", report.Kind);
        Assert.Equal("safe", report.Tier);
        Assert.Equal(0.83, report.Score);
    }

    [Fact]
    public void Scan_reports_count_every_tier()
    {
        var scan = new LeftoverScanResult
        {
            Items = new[]
            {
                new LeftoverItem { Path = @"C:\ProgramData\A", Kind = SystemItemKind.Directory, Tier = RiskTier.Safe, Score = 1, Reason = "r" },
                new LeftoverItem { Path = @"C:\ProgramData\B", Kind = SystemItemKind.Directory, Tier = RiskTier.Moderate, Score = 1, Reason = "r" },
                new LeftoverItem { Path = @"C:\ProgramData\C", Kind = SystemItemKind.Directory, Tier = RiskTier.Advanced, Score = 1, Reason = "r" },
            },
            RegistryVanishedCount = 0,
            FilesVanishedCount = 0,
        };

        var report = ScanReport.From("static", scan, 4096);

        Assert.Equal(3, report.Total);
        Assert.Equal(1, report.Safe);
        Assert.Equal(1, report.Moderate);
        Assert.Equal(1, report.Advanced);
        Assert.Equal(4096, report.SizeBytes);
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData(0L, "0 B")]
    [InlineData(999L, "999 B")]
    [InlineData(2048L, "2 KB")]
    [InlineData(5919744L, "5.6 MB")]
    [InlineData(1099511627776L, "1.0 TB")]
    public void Sizes_are_formatted_for_humans(long? bytes, string expected)
    {
        Assert.Equal(expected, SizeFormat.Bytes(bytes));
    }
}

public class CliOptionsTests
{
    [Fact]
    public void Value_options_accept_a_separate_token()
    {
        var options = CliOptions.Parse(new[] { "list", "--search", "visual", "studio" }, new[] { "search" });

        Assert.Equal("visual", options.Value("search"));
        Assert.Equal(new[] { "studio" }, options.Positionals);
    }

    [Fact]
    public void Value_options_accept_an_inline_value()
    {
        var options = CliOptions.Parse(new[] { "list", "--limit=25" }, new[] { "limit" });

        Assert.Equal("25", options.Value("limit"));
    }

    [Fact]
    public void Boolean_options_work_with_and_without_a_value()
    {
        var options = CliOptions.Parse(new[] { "list", "--json", "--include-system=false" }, null, new[] { "json", "include-system" });

        Assert.True(options.Has("json"));
        Assert.False(options.Has("missing"));
        Assert.Equal("false", options.Value("include-system"));
    }

    [Fact]
    public void Unknown_options_fail_loudly()
    {
        var exception = Assert.Throws<CliUsageException>(() => CliOptions.Parse(new[] { "list", "--wat" }));

        Assert.Contains("--wat", exception.Message);
    }

    [Fact]
    public void Options_that_need_a_value_fail_when_it_is_missing()
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(new[] { "list", "--search" }, new[] { "search" }));
    }

    [Fact]
    public void Numeric_options_report_bad_values()
    {
        var options = CliOptions.Parse(new[] { "list", "--limit", "many" }, new[] { "limit" });

        Assert.Null(options.IntValue("limit", out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Enum_options_are_case_insensitive()
    {
        var options = CliOptions.Parse(new[] { "scan", "app", "--min-tier", "MODERATE" }, new[] { "min-tier" });

        Assert.Equal(RiskTier.Moderate, options.EnumValue("min-tier", RiskTier.Safe, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Positionals_are_required_and_validated()
    {
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(new[] { "uninstall" }).RequirePositional(0, "application name"));
        Assert.Throws<CliUsageException>(() => CliOptions.Parse(new[] { "uninstall", "a", "b" }).EnsureNoExtraPositionals(1));
    }

    [Fact]
    public void Tier_names_map_to_the_risk_enum()
    {
        Assert.Equal(RiskTier.Safe, ConsoleUi.ParseTier("safe"));
        Assert.Equal(RiskTier.Moderate, ConsoleUi.ParseTier("medium"));
        Assert.Equal(RiskTier.Advanced, ConsoleUi.ParseTier("advanced"));
        Assert.Equal(RiskTier.Advanced, ConsoleUi.ParseTier(null));
    }
}
