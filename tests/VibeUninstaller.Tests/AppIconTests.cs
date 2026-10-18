using System.IO;
using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

/// <summary>
/// Most programs show a lettermark instead of an icon, so the rules that decide
/// where an icon may come from are worth pinning down.
/// </summary>
public class IconResolverTests
{
    private static InstalledApp App(
        string? displayIcon = null,
        string? installLocation = @"C:\Program Files\Foo",
        string displayName = "Foo",
        string? publisher = "Foo Software") => new()
    {
        Id = "test|foo",
        DisplayName = displayName,
        Publisher = publisher,
        DisplayIcon = displayIcon,
        InstallLocation = installLocation,
    };

    [Fact]
    public void Absolute_display_icons_are_used_first()
    {
        var candidates = IconResolver.Candidates(App(@"C:\Program Files\Foo\foo.exe,3"));

        var first = candidates[0];
        Assert.Equal(IconSourceKind.IconReference, first.Kind);
        Assert.Equal(@"C:\Program Files\Foo\foo.exe", first.Path);
        Assert.Equal(3, first.Index);

        // The install folder stays available as a fallback.
        Assert.Contains(candidates, candidate => candidate.Kind == IconSourceKind.InstallLocation);
    }

    [Fact]
    public void Bare_file_names_are_resolved_against_the_install_folder()
    {
        var candidates = IconResolver.Candidates(App("foo.exe,1", @"C:\Tools\Foo"));

        Assert.Equal(Path.Combine(@"C:\Tools\Foo", "foo.exe"), candidates[0].Path);
        Assert.Equal(1, candidates[0].Index);
    }

    [Theory]
    [InlineData(@"%ProgramFiles%\Foo\foo.exe")]
    [InlineData(@"C:\Foo\foo.exe")]
    [InlineData("\"C:\\Foo\\foo.exe\"")]
    public void Absolute_paths_are_never_combined_with_the_install_folder(string displayIcon)
    {
        Assert.DoesNotContain(
            "Program Files",
            IconResolver.ResolveAgainstInstallLocation(IconLocationParser.Parse(displayIcon)!.Path, @"C:\Program Files\Foo"));
    }

    [Fact]
    public void Launcher_binaries_are_ignored()
    {
        var candidates = IconResolver.Candidates(App(@"C:\Windows\Installer\{ABC}\msiexec.exe,0", @"C:\Apps\Foo"));

        Assert.DoesNotContain(candidates, candidate => candidate.Kind == IconSourceKind.IconReference);
        Assert.Equal(IconSourceKind.InstallLocation, Assert.Single(candidates).Kind);
    }

    [Fact]
    public void An_application_without_any_source_has_no_candidates() =>
        Assert.Empty(IconResolver.Candidates(App(null, null)));

    [Fact]
    public void An_install_folder_without_a_display_icon_is_still_offered()
    {
        var candidate = Assert.Single(IconResolver.Candidates(App(null, @"C:\Apps\Foo")));

        Assert.Equal(IconSourceKind.InstallLocation, candidate.Kind);
        Assert.Equal(@"C:\Apps\Foo", candidate.Path);
    }

    [Theory]
    [InlineData("foo.exe", false)]
    [InlineData("Foo Bar.exe", false)]
    [InlineData("unins000.exe", true)]
    [InlineData("uninstall.exe", true)]
    [InlineData("Updater.exe", true)]
    [InlineData("crashpad_handler.exe", true)]
    [InlineData("vcredist_x64.exe", true)]
    [InlineData("msiexec.exe", true)]
    public void Helper_binaries_are_recognised(string fileName, bool expected) =>
        Assert.Equal(expected, IconResolver.IsHelperExecutable(fileName));

    [Fact]
    public void The_executable_named_after_the_application_wins()
    {
        var files = new[]
        {
            new ExecutableFile(@"C:\Program Files\Foo\unins000.exe", 5_000_000),
            new ExecutableFile(@"C:\Program Files\Foo\crashpad_handler.exe", 9_000_000),
            new ExecutableFile(@"C:\Program Files\Foo\helper.exe", 8_000_000, 1),
            new ExecutableFile(@"C:\Program Files\Foo\foo-cli.exe", 7_000_000, 1),
            new ExecutableFile(@"C:\Program Files\Foo\foo.exe", 2_000_000),
        };

        Assert.Equal(@"C:\Program Files\Foo\foo.exe", IconResolver.PickExecutable(App(), files));
    }

    [Fact]
    public void A_folder_holding_only_helpers_yields_nothing()
    {
        var files = new[]
        {
            new ExecutableFile(@"C:\Program Files\Foo\unins000.exe", 900_000),
            new ExecutableFile(@"C:\Program Files\Foo\setup.exe", 1_200_000),
        };

        Assert.Null(IconResolver.PickExecutable(App(), files));
    }

    [Fact]
    public void Binaries_at_the_install_root_beat_equally_named_ones_deeper_down()
    {
        var files = new[]
        {
            new ExecutableFile(@"C:\Program Files\Foo\sub\app.exe", 1_000_000, 1),
            new ExecutableFile(@"C:\Program Files\Foo\app.exe", 1_000_000, 0),
        };

        Assert.Equal(@"C:\Program Files\Foo\app.exe", IconResolver.PickExecutable(App(), files));
    }

    [Fact]
    public void Publishers_help_pick_a_binary_that_is_not_named_after_the_app()
    {
        var app = App(displayName: "Contoso Suite 2024", publisher: "Contoso Ltd");
        var files = new[]
        {
            new ExecutableFile(@"C:\Program Files\Foo\bootstrapper.exe", 2_000_000),
            new ExecutableFile(@"C:\Program Files\Foo\contoso.exe", 1_000_000),
            new ExecutableFile(@"C:\Program Files\Foo\whatever.exe", 1_100_000),
        };

        Assert.Equal(@"C:\Program Files\Foo\contoso.exe", IconResolver.PickExecutable(app, files));
    }
}

public class AppxLogoParserTests
{
    private const string Manifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
          <Applications>
            <Application Id="App">
              <uap:VisualElements DisplayName="Foo" Description="Foo"
                                  Square150x150Logo="Assets\Square150x150Logo.png"
                                  Square44x44Logo="Assets\Square44x44Logo.png"
                                  BackgroundColor="transparent" />
            </Application>
          </Applications>
          <Properties>
            <Logo>Assets\StoreLogo.png</Logo>
          </Properties>
        </Package>
        """;

    [Fact]
    public void The_application_tile_logo_wins() =>
        Assert.Equal(@"Assets\Square44x44Logo.png", AppxLogoParser.ParseLogoPath(Manifest));

    [Fact]
    public void Forward_slashes_are_normalised() =>
        Assert.Equal(
            @"Assets\Square44x44Logo.png",
            AppxLogoParser.ParseLogoPath(Manifest.Replace(@"Assets\Square44x44Logo.png", "Assets/Square44x44Logo.png")));

    [Fact]
    public void The_package_logo_is_the_fallback()
    {
        var withoutTileLogo = Manifest.Replace("Square44x44Logo=\"Assets\\Square44x44Logo.png\"", string.Empty);

        Assert.Equal(@"Assets\StoreLogo.png", AppxLogoParser.ParseLogoPath(withoutTileLogo));
    }

    [Fact]
    public void Resource_strings_are_not_file_paths()
    {
        const string manifest = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
              <Applications>
                <Application Id="App">
                  <uap:VisualElements DisplayName="Foo" Description="Foo"
                                      Square44x44Logo="ms-resource:Square44x44Logo" />
                </Application>
              </Applications>
            </Package>
            """;

        Assert.Null(AppxLogoParser.ParseLogoPath(manifest));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not xml at all")]
    [InlineData("<Package><Applications>")]
    public void Broken_manifests_are_survived(string? xml) => Assert.Null(AppxLogoParser.ParseLogoPath(xml));
}
