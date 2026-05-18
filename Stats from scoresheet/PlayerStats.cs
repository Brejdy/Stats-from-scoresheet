namespace StatsFromScoresheet;

public sealed class PlayerStats
{
    public string PlayerName { get; set; } = "";
    public string Number { get; set; } = "";
    public int TwoPointersMade { get; set; }
    public int ThreePointersMade { get; set; }
    public int FreeThrowsMade { get; set; }
    public int FreeThrowsTried { get; set; }
    public int Fouls { get; set; }
}
