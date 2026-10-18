using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VibeUninstaller.IconLab;

/// <summary>
/// Draws the application icon. `assets/branding/app.ico` has no vector source, so
/// this is what keeps it reproducible: every shape is expressed as a fraction of
/// the requested size, and the small sizes are drawn differently (no sparkle, no
/// dissolving pixels) so they stay legible in the taskbar.
///
///   dotnet run --project tools/icon-lab                              preview every concept
///   dotnet run --project tools/icon-lab -- appico trash &lt;file.ico&gt;   write the .ico
///
/// The concepts are the three that were shown for review: "trash" (a bin with a
/// sparkle, the one that shipped), "arrow" (a tray with an arrow leaving it) and
/// "pixels" (a V dissolving into squares).
/// </summary>
internal static class Program
{
    private static readonly string[] Concepts = { "arrow", "trash", "pixels" };

    private static readonly int[] IconSizes = { 256, 128, 64, 48, 32, 24, 16 };

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return Preview(".");
        }

        if (args.Length == 3 && args[0].Equals("appico", StringComparison.OrdinalIgnoreCase))
        {
            if (!Concepts.Contains(args[1], StringComparer.OrdinalIgnoreCase))
            {
                return Usage($"unknown concept '{args[1]}'");
            }

            return WriteIcon(args[1].ToLowerInvariant(), args[2]);
        }

        if (args.Length == 2 && args[0].Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            return Preview(args[1]);
        }

        return Usage("unrecognised arguments");
    }

    /// <summary>Writes the three concepts at 256 px plus their small-size strips.</summary>
    private static int Preview(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (IOException exception)
        {
            return Usage(exception.Message);
        }

        foreach (var kind in Concepts)
        {
            File.WriteAllBytes(Path.Combine(directory, $"icon-{kind}-256.png"), Render(kind, 256));
            File.WriteAllBytes(Path.Combine(directory, $"icon-{kind}-strip.png"), Strip(kind));
        }

        File.WriteAllBytes(Path.Combine(directory, "icon-sheet.png"), Sheet());
        Console.WriteLine($"Wrote {Concepts.Length} concepts and the sheet to {Path.GetFullPath(directory)}.");
        return 0;
    }

    /// <summary>Writes the multi-size .ico the executables, windows and installer use.</summary>
    private static int WriteIcon(string kind, string output)
    {
        var images = IconSizes.Select(size => Render(kind, size)).ToArray();

        try
        {
            using var stream = File.Create(output);
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)IconSizes.Length);

            var offset = 6 + (16 * IconSizes.Length);
            for (var i = 0; i < IconSizes.Length; i++)
            {
                // 256 and above are stored with a zero width/height byte.
                writer.Write((byte)(IconSizes[i] >= 256 ? 0 : IconSizes[i]));
                writer.Write((byte)(IconSizes[i] >= 256 ? 0 : IconSizes[i]));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }

            foreach (var image in images)
            {
                writer.Write(image);
            }
        }
        catch (IOException exception)
        {
            return Usage(exception.Message);
        }

        Console.WriteLine($"Wrote {output} with sizes {string.Join(", ", IconSizes)}.");
        return 0;
    }

    private static int Usage(string problem)
    {
        Console.Error.WriteLine($"icon-lab: {problem}");
        Console.Error.WriteLine("usage: icon-lab [preview <directory>]");
        Console.Error.WriteLine($"       icon-lab appico <{string.Join("|", Concepts)}> <file.ico>");
        return 1;
    }

    /// <summary>The preview sheet: each concept at 256 px with its small sizes underneath.</summary>
    private static byte[] Sheet()
    {
        const int width = 1180;
        const int height = 420;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2A, 0x2E, 0x38)), null, new Rect(0, 0, width, height));

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

            for (var i = 0; i < Concepts.Length; i++)
            {
                var left = 40 + (i * 380);
                dc.DrawImage(ToBitmap(Render(Concepts[i], 256)), new Rect(left, 40, 256, 256));

                var x = left;
                foreach (var size in new[] { 64, 48, 32, 16 })
                {
                    dc.DrawImage(ToBitmap(Render(Concepts[i], size)), new Rect(x, 320, size, size));
                    x += size + 16;
                }

                var label = new FormattedText(
                    Concepts[i],
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    18,
                    Brushes.White,
                    96);
                dc.DrawText(label, new Point(left, 22));
            }
        }

        return Encode(visual, width, height);
    }

    private static byte[] Strip(string kind)
    {
        const int height = 80;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2A, 0x2E, 0x38)), null, new Rect(0, 0, 420, height));
            var x = 8.0;
            foreach (var size in new[] { 64, 48, 32, 24, 16 })
            {
                dc.DrawImage(ToBitmap(Render(kind, size)), new Rect(x, (height - size) / 2.0, size, size));
                x += size + 12;
            }
        }

        return Encode(visual, 420, height);
    }

    private static BitmapSource ToBitmap(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }

    private static byte[] Encode(Visual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static byte[] Render(string kind, int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            Tile(dc, size);

            // Every measurement is a fraction of the icon so the shapes stay
            // legible when the shell asks for 16 pixels.
            var u = size / 256.0;
            var pen = new Pen(Brushes.White, Math.Max(1.1, 17 * u))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };

            switch (kind)
            {
                case "trash":
                    Trash(dc, size, u, pen);
                    break;
                case "pixels":
                    Pixels(dc, size, u, pen);
                    break;
                default:
                    Arrow(dc, size, u, pen);
                    break;
            }
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        return png.ToArray();
    }

    private static void Tile(DrawingContext dc, int size)
    {
        var pad = Math.Round(size * 0.045);
        var rect = new Rect(pad, pad, size - (2 * pad), size - (2 * pad));
        var gradient = new LinearGradientBrush(
            Color.FromRgb(0x4C, 0x8D, 0xFF),
            Color.FromRgb(0x8B, 0x5C, 0xF6),
            new Point(0, 0),
            new Point(1, 1));
        dc.DrawRoundedRectangle(gradient, null, rect, size * 0.22, size * 0.22);
    }

    /// <summary>A tray with an arrow leaving it: the leftovers are taken out.</summary>
    private static void Arrow(DrawingContext dc, int size, double u, Pen pen)
    {
        var figure = new PathFigure { StartPoint = new Point(72 * u, 152 * u), IsClosed = false };
        figure.Segments.Add(new LineSegment(new Point(72 * u, 196 * u), true));
        figure.Segments.Add(new LineSegment(new Point(184 * u, 196 * u), true));
        figure.Segments.Add(new LineSegment(new Point(184 * u, 152 * u), true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        dc.DrawGeometry(null, pen, geometry);

        dc.DrawLine(pen, new Point(128 * u, 168 * u), new Point(128 * u, 74 * u));
        dc.DrawLine(pen, new Point(92 * u, 110 * u), new Point(128 * u, 74 * u));
        dc.DrawLine(pen, new Point(164 * u, 110 * u), new Point(128 * u, 74 * u));

        if (size >= 32)
        {
            dc.DrawGeometry(Brushes.White, null, Star(212 * u, 62 * u, 16 * u));
        }
    }

    /// <summary>A bin with a sparkle: the classic uninstaller, kept friendly.</summary>
    private static void Trash(DrawingContext dc, int size, double u, Pen pen)
    {
        dc.DrawLine(pen, new Point(70 * u, 96 * u), new Point(186 * u, 96 * u));

        var handle = new PathFigure { StartPoint = new Point(108 * u, 96 * u), IsClosed = false };
        handle.Segments.Add(new LineSegment(new Point(108 * u, 76 * u), true));
        handle.Segments.Add(new LineSegment(new Point(148 * u, 76 * u), true));
        handle.Segments.Add(new LineSegment(new Point(148 * u, 96 * u), true));
        var handleGeometry = new PathGeometry();
        handleGeometry.Figures.Add(handle);
        dc.DrawGeometry(null, pen, handleGeometry);

        var body = new PathFigure { StartPoint = new Point(84 * u, 120 * u), IsClosed = false };
        body.Segments.Add(new LineSegment(new Point(96 * u, 198 * u), true));
        body.Segments.Add(new LineSegment(new Point(160 * u, 198 * u), true));
        body.Segments.Add(new LineSegment(new Point(172 * u, 120 * u), true));
        var bodyGeometry = new PathGeometry();
        bodyGeometry.Figures.Add(body);
        dc.DrawGeometry(null, pen, bodyGeometry);

        if (size >= 32)
        {
            var ribPen = new Pen(Brushes.White, Math.Max(1.0, pen.Thickness * 0.72));
            dc.DrawLine(ribPen, new Point(115 * u, 140 * u), new Point(119 * u, 182 * u));
            dc.DrawLine(ribPen, new Point(141 * u, 140 * u), new Point(137 * u, 182 * u));
            dc.DrawGeometry(Brushes.White, null, Star(206 * u, 60 * u, 20 * u));
        }
    }

    /// <summary>A bold V dissolving into pixels: the brand mark letting go of leftovers.</summary>
    private static void Pixels(DrawingContext dc, int size, double u, Pen pen)
    {
        var thick = new Pen(Brushes.White, Math.Max(1.6, 30 * u))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        dc.DrawLine(thick, new Point(66 * u, 72 * u), new Point(112 * u, 190 * u));
        dc.DrawLine(thick, new Point(158 * u, 72 * u), new Point(112 * u, 190 * u));

        if (size < 24)
        {
            return;
        }

        var squares = new[]
        {
            (X: 168.0, Y: 74.0, S: 34.0, A: 0.95),
            (X: 202.0, Y: 106.0, S: 26.0, A: 0.75),
            (X: 226.0, Y: 134.0, S: 20.0, A: 0.55),
            (X: 242.0, Y: 158.0, S: 14.0, A: 0.35),
        };

        foreach (var (x, y, s, alpha) in squares)
        {
            var brush = new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 255, 255, 255));
            brush.Freeze();
            dc.DrawRoundedRectangle(brush, null, new Rect(x * u, y * u, s * u, s * u), 4 * u, 4 * u);
        }
    }

    /// <summary>A four pointed sparkle.</summary>
    private static Geometry Star(double x, double y, double radius)
    {
        var figure = new PathFigure { StartPoint = new Point(x, y - radius), IsClosed = true };
        figure.Segments.Add(new LineSegment(new Point(x + (radius * 0.22), y - (radius * 0.22)), true));
        figure.Segments.Add(new LineSegment(new Point(x + radius, y), true));
        figure.Segments.Add(new LineSegment(new Point(x + (radius * 0.22), y + (radius * 0.22)), true));
        figure.Segments.Add(new LineSegment(new Point(x, y + radius), true));
        figure.Segments.Add(new LineSegment(new Point(x - (radius * 0.22), y + (radius * 0.22)), true));
        figure.Segments.Add(new LineSegment(new Point(x - radius, y), true));
        figure.Segments.Add(new LineSegment(new Point(x - (radius * 0.22), y - (radius * 0.22)), true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
