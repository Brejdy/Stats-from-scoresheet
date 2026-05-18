using System.Drawing;

namespace StatsFromScoresheet;

internal sealed record RosterTemplate(
    string Name,
    RectangleF RosterArea,
    RectangleF FirstRow,
    float RowHeight,
    int RowCount,
    RectangleF NameColumn,
    RectangleF NumberColumn,
    RectangleF FoulsColumn);
