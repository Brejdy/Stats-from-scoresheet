using System.Drawing;
using System.Drawing.Imaging;
using PdfiumViewer;

namespace StatsFromScoresheet;

public sealed class ScoreSheetAnalyzer
{
    public ScoreSheetAnalysisResult Analyze(string pdfPath, TeamSide teamSide)
    {
        using var document = PdfDocument.Load(pdfPath);
        using var renderedPage = document.Render(
            page: 0,
            width: 3000,
            height: 4000,
            dpiX: 300,
            dpiY: 300,
            flags: PdfRenderFlags.Annotations);

        var diagnosticsDirectory = CreateDiagnosticsDirectory(pdfPath);
        var fullPagePath = Path.Combine(diagnosticsDirectory, "page.png");
        renderedPage.Save(fullPagePath);

        var pageBitmap = new Bitmap(renderedPage);
        IReadOnlyList<PlayerStats> players;
        using (var rosterParser = new RosterParser())
        {
            players = rosterParser.Parse(pageBitmap, teamSide, diagnosticsDirectory);
        }

        var preview = CreatePreview(pageBitmap, teamSide);
        preview.Save(Path.Combine(diagnosticsDirectory, "preview.png"), ImageFormat.Png);

        pageBitmap.Dispose();

        return new ScoreSheetAnalysisResult
        {
            PreviewImage = preview,
            Players = players,
            DiagnosticsDirectory = diagnosticsDirectory,
            Message = BuildCurrentScopeMessage(teamSide, players.Count, diagnosticsDirectory)
        };
    }

    private static string CreateDiagnosticsDirectory(string pdfPath)
    {
        var safeName = Path.GetFileNameWithoutExtension(pdfPath);
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        var directory = Path.Combine(AppContext.BaseDirectory, "analysis-output", safeName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string BuildCurrentScopeMessage(TeamSide teamSide, int playerCount, string diagnosticsDirectory)
    {
        var sideText = teamSide == TeamSide.HomeA ? "domaci A" : "hoste B";

        return
            $"PDF bylo nacteno. Vybrany tym: {sideText}. " +
            $"Ze soupisky bylo predvyplneno {playerCount} radku. " +
            $"Diagnosticky obrazek je ulozen v: {diagnosticsDirectory}";
    }

    private static Bitmap CreatePreview(Bitmap page, TeamSide teamSide)
    {
        var preview = new Bitmap(page);
        using var graphics = Graphics.FromImage(preview);
        using var pen = new Pen(Color.FromArgb(220, Color.OrangeRed), 8);

        var rosterBounds = teamSide == TeamSide.HomeA
            ? new RectangleF(0.055f, 0.226f, 0.425f, 0.205f)
            : new RectangleF(0.055f, 0.589f, 0.425f, 0.205f);

        graphics.DrawRectangle(pen, Scale(rosterBounds, preview.Size));
        return preview;
    }

    private static Rectangle Scale(RectangleF relative, Size imageSize)
    {
        return Rectangle.Round(new RectangleF(
            relative.X * imageSize.Width,
            relative.Y * imageSize.Height,
            relative.Width * imageSize.Width,
            relative.Height * imageSize.Height));
    }
}
