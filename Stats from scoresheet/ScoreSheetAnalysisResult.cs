using System.Drawing;

namespace StatsFromScoresheet;

public sealed class ScoreSheetAnalysisResult : IDisposable
{
    public required Bitmap PreviewImage { get; init; }
    public required IReadOnlyList<PlayerStats> Players { get; init; }
    public required string DiagnosticsDirectory { get; init; }
    public required string Message { get; init; }

    public void Dispose()
    {
        PreviewImage.Dispose();
    }
}
