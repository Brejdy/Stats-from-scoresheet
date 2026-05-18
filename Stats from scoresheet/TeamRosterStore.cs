using System.Text;

namespace StatsFromScoresheet;

public sealed class TeamRosterStore
{
    private readonly Dictionary<string, string> playersByNumber = new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "team-roster.csv");

    public void Load()
    {
        playersByNumber.Clear();

        if (!File.Exists(FilePath))
        {
            return;
        }

        foreach (var line in File.ReadLines(FilePath, Encoding.UTF8).Skip(1))
        {
            var values = ParseCsvLine(line);
            if (values.Count < 2)
            {
                continue;
            }

            AddOrUpdate(values[0], values[1]);
        }
    }

    public bool TryGetName(string number, out string name)
    {
        return playersByNumber.TryGetValue(NormalizeNumber(number), out name!);
    }

    public void AddOrUpdate(string number, string name)
    {
        number = NormalizeNumber(number);
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        playersByNumber[number] = name;
    }

    public int SaveFrom(IEnumerable<PlayerStats> players)
    {
        foreach (var player in players)
        {
            AddOrUpdate(player.Number, player.PlayerName);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        var builder = new StringBuilder();
        builder.AppendLine("Number,Name");
        foreach (var player in playersByNumber.OrderBy(item => SortKey(item.Key)))
        {
            builder.Append(EscapeCsv(player.Key)).Append(',');
            builder.Append(EscapeCsv(player.Value)).AppendLine();
        }

        File.WriteAllText(FilePath, builder.ToString(), Encoding.UTF8);
        return playersByNumber.Count;
    }

    private static int SortKey(string number)
    {
        return int.TryParse(number, out var parsed) ? parsed : int.MaxValue;
    }

    private static string NormalizeNumber(string value)
    {
        return value.Trim();
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
