using System;
using System.Windows.Forms;

namespace StatsFromScoresheet;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            RunHeadlessAnalysis(args);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void RunHeadlessAnalysis(string[] args)
    {
        var pdfPath = args[0];
        var side = args.Length > 1 && args[1].Equals("B", StringComparison.OrdinalIgnoreCase)
            ? TeamSide.AwayB
            : TeamSide.HomeA;

        var analyzer = new ScoreSheetAnalyzer();
        using var result = analyzer.Analyze(pdfPath, side);
        var rosterStore = new TeamRosterStore();
        rosterStore.Load();

        Console.WriteLine(result.Message);
        foreach (var player in result.Players)
        {
            if (rosterStore.TryGetName(player.Number, out var knownName))
            {
                player.PlayerName = knownName;
            }

            Console.WriteLine($"{player.Number}\t{player.PlayerName}\tF:{player.Fouls}");
        }
    }
}
