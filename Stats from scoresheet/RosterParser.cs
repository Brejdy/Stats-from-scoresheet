using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Tesseract;

namespace StatsFromScoresheet;

internal sealed class RosterParser : IDisposable
{
    private static readonly Regex NumberRegex = new(@"\d{1,2}", RegexOptions.Compiled);

    private readonly TesseractEngine textEngine;
    private readonly TesseractEngine numberEngine;

    public RosterParser()
    {
        textEngine = new TesseractEngine(@"./tessdata", "ces+eng", EngineMode.Default);
        textEngine.DefaultPageSegMode = PageSegMode.SingleLine;

        numberEngine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
        numberEngine.SetVariable("tessedit_char_whitelist", "0123456789");
        numberEngine.DefaultPageSegMode = PageSegMode.SingleWord;
    }

    public IReadOnlyList<PlayerStats> Parse(Bitmap page, TeamSide side, string diagnosticsDirectory)
    {
        var template = side == TeamSide.HomeA ? Templates.TeamA : Templates.TeamB;
        var rosterDirectory = Path.Combine(diagnosticsDirectory, template.Name);
        Directory.CreateDirectory(rosterDirectory);

        using var rosterImage = Crop(page, Scale(template.RosterArea, page.Size));
        rosterImage.Save(Path.Combine(rosterDirectory, "roster.png"), System.Drawing.Imaging.ImageFormat.Png);

        var players = new List<PlayerStats>();
        for (var rowIndex = 0; rowIndex < template.RowCount; rowIndex++)
        {
            var rowBounds = template.FirstRow with
            {
                Y = template.FirstRow.Y + template.RowHeight * rowIndex
            };

            using var rowImage = Crop(page, Scale(rowBounds, page.Size));
            rowImage.Save(Path.Combine(rosterDirectory, $"row_{rowIndex + 1:00}.png"), System.Drawing.Imaging.ImageFormat.Png);

            var player = ParseRow(page, template, rowBounds, rowIndex, rosterDirectory);
            if (ShouldKeep(player))
            {
                players.Add(player);
            }
        }

        return players;
    }

    public void Dispose()
    {
        textEngine.Dispose();
        numberEngine.Dispose();
    }

    private PlayerStats ParseRow(
        Bitmap page,
        RosterTemplate template,
        RectangleF rowBounds,
        int rowIndex,
        string rosterDirectory)
    {
        using var nameImage = Crop(page, Scale(Project(template.NameColumn, rowBounds), page.Size));
        using var numberImage = Crop(page, Scale(Project(template.NumberColumn, rowBounds), page.Size));
        using var foulsImage = Crop(page, Scale(Project(template.FoulsColumn, rowBounds), page.Size));

        nameImage.Save(Path.Combine(rosterDirectory, $"row_{rowIndex + 1:00}_name.png"), System.Drawing.Imaging.ImageFormat.Png);
        numberImage.Save(Path.Combine(rosterDirectory, $"row_{rowIndex + 1:00}_number.png"), System.Drawing.Imaging.ImageFormat.Png);
        foulsImage.Save(Path.Combine(rosterDirectory, $"row_{rowIndex + 1:00}_fouls.png"), System.Drawing.Imaging.ImageFormat.Png);

        var name = CleanName(ReadText(nameImage, textEngine));
        var number = ReadBestNumber(numberImage);

        return new PlayerStats
        {
            PlayerName = name,
            Number = number,
            Fouls = CountFouls(foulsImage)
        };
    }

    private string ReadBestNumber(Bitmap image)
    {
        var text = ReadText(image, numberEngine);
        var match = NumberRegex.Match(text);
        return match.Success ? match.Value : "";
    }

    private static string ReadText(Bitmap image, TesseractEngine engine)
    {
        using var prepared = PrepareForOcr(image);
        var tempFile = Path.Combine(Path.GetTempPath(), $"scoresheet_ocr_{Guid.NewGuid():N}.png");

        try
        {
            prepared.Save(tempFile, System.Drawing.Imaging.ImageFormat.Png);
            using var pix = Pix.LoadFromFile(tempFile);
            using var page = engine.Process(pix);
            return page.GetText().Trim();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static Bitmap PrepareForOcr(Bitmap source)
    {
        using var cleaned = RemovePrintedGrid(source);
        var scaled = new Bitmap(cleaned.Width * 3, cleaned.Height * 3);
        using var graphics = Graphics.FromImage(scaled);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(cleaned, 0, 0, scaled.Width, scaled.Height);
        return scaled;
    }

    private static Bitmap RemovePrintedGrid(Bitmap source)
    {
        var cleaned = new Bitmap(source.Width, source.Height);

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                var saturation = color.GetSaturation();
                var brightness = color.GetBrightness();

                cleaned.SetPixel(x, y, saturation > 0.28 && brightness < 0.88 ? Color.Black : Color.White);
            }
        }

        return cleaned;
    }

    private int CountFouls(Bitmap foulsImage)
    {
        var cellWidth = foulsImage.Width / 5.0;
        var count = 0;

        for (var index = 0; index < 5; index++)
        {
            var left = (int)Math.Round(index * cellWidth);
            var right = (int)Math.Round((index + 1) * cellWidth);
            var cell = new Rectangle(
                left + 6,
                5,
                Math.Max(1, right - left - 12),
                Math.Max(1, foulsImage.Height - 10));

            using var cellImage = Crop(foulsImage, cell);
            if (!string.IsNullOrWhiteSpace(ReadBestNumber(cellImage)))
            {
                count++;
            }
        }

        return count;
    }

    private static bool ShouldKeep(PlayerStats player)
    {
        return !string.IsNullOrWhiteSpace(player.PlayerName)
            || !string.IsNullOrWhiteSpace(player.Number)
            || player.Fouls > 0;
    }

    private static string CleanName(string value)
    {
        value = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
        while (value.Contains("  ", StringComparison.Ordinal))
        {
            value = value.Replace("  ", " ", StringComparison.Ordinal);
        }

        return value;
    }

    private static Rectangle Scale(RectangleF relative, Size imageSize)
    {
        return Rectangle.Round(new RectangleF(
            relative.X * imageSize.Width,
            relative.Y * imageSize.Height,
            relative.Width * imageSize.Width,
            relative.Height * imageSize.Height));
    }

    private static RectangleF Project(RectangleF relativeColumn, RectangleF rowBounds)
    {
        return new RectangleF(
            rowBounds.X + rowBounds.Width * relativeColumn.X,
            rowBounds.Y + rowBounds.Height * relativeColumn.Y,
            rowBounds.Width * relativeColumn.Width,
            rowBounds.Height * relativeColumn.Height);
    }

    private static Bitmap Crop(Bitmap source, Rectangle rectangle)
    {
        rectangle.Intersect(new Rectangle(Point.Empty, source.Size));
        return source.Clone(rectangle, PixelFormat.Format32bppArgb);
    }

    private static class Templates
    {
        public static readonly RosterTemplate TeamA = new(
            Name: "team_a_roster",
            RosterArea: new RectangleF(0.055f, 0.226f, 0.425f, 0.205f),
            FirstRow: new RectangleF(0.057f, 0.233f, 0.410f, 0.017f),
            RowHeight: 0.0177f,
            RowCount: 10,
            NameColumn: new RectangleF(0.205f, 0.02f, 0.380f, 0.86f),
            NumberColumn: new RectangleF(0.622f, 0.04f, 0.052f, 0.82f),
            FoulsColumn: new RectangleF(0.720f, 0.04f, 0.265f, 0.82f));

        public static readonly RosterTemplate TeamB = new(
            Name: "team_b_roster",
            RosterArea: new RectangleF(0.055f, 0.589f, 0.425f, 0.205f),
            FirstRow: new RectangleF(0.057f, 0.598f, 0.410f, 0.017f),
            RowHeight: 0.0177f,
            RowCount: 10,
            NameColumn: new RectangleF(0.205f, 0.02f, 0.380f, 0.86f),
            NumberColumn: new RectangleF(0.622f, 0.04f, 0.052f, 0.82f),
            FoulsColumn: new RectangleF(0.720f, 0.04f, 0.265f, 0.82f));
    }
}
