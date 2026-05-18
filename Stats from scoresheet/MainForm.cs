using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace StatsFromScoresheet;

public sealed class MainForm : Form
{
    private static readonly Color AppBackground = Color.FromArgb(244, 247, 250);
    private static readonly Color PanelBackground = Color.White;
    private static readonly Color BorderColor = Color.FromArgb(214, 222, 232);
    private static readonly Color PrimaryColor = Color.FromArgb(31, 96, 196);
    private static readonly Color AccentColor = Color.FromArgb(218, 86, 46);
    private static readonly Color MutedTextColor = Color.FromArgb(88, 101, 116);

    private readonly ScoreSheetAnalyzer analyzer = new();
    private readonly BindingList<PlayerStats> players = new();
    private readonly TeamRosterStore rosterStore = new();

    private readonly TextBox pdfPathTextBox = new();
    private readonly ComboBox teamSideComboBox = new();
    private readonly Button browseButton = new();
    private readonly Button analyzeButton = new();
    private readonly Button addRowButton = new();
    private readonly Button saveRosterButton = new();
    private readonly Button exportButton = new();
    private readonly PictureBox previewPictureBox = new();
    private readonly DataGridView playersGrid = new();
    private readonly Label statusLabel = new();
    private readonly Label playerCountLabel = new();
    private readonly Label rosterPathLabel = new();

    private ScoreSheetAnalysisResult? currentResult;

    public MainForm()
    {
        Text = "Stats from scoresheet";
        MinimumSize = new Size(1180, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppBackground;
        Font = new Font("Segoe UI", 9F);

        rosterStore.Load();
        players.ListChanged += (_, _) => UpdatePlayerCount();
        BuildLayout();
        ConfigureGrid();
        UpdatePlayerCount();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            currentResult?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
            BackColor = AppBackground
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        var header = CreateHeaderPanel();
        root.Controls.Add(header, 0, 0);

        var importPanel = CreatePanel();
        importPanel.Padding = new Padding(16, 12, 16, 12);
        root.Controls.Add(importPanel, 0, 1);

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 2,
            BackColor = PanelBackground
        };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 6));
        importPanel.Controls.Add(toolbar);

        toolbar.Controls.Add(CreateFieldLabel("PDF zapis"), 0, 0);
        toolbar.Controls.Add(CreateFieldLabel("Role tymu"), 1, 0);

        pdfPathTextBox.Dock = DockStyle.Fill;
        pdfPathTextBox.PlaceholderText = "Vyber PDF zapis...";
        pdfPathTextBox.BorderStyle = BorderStyle.FixedSingle;
        toolbar.Controls.Add(pdfPathTextBox, 0, 1);

        teamSideComboBox.Dock = DockStyle.Fill;
        teamSideComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        teamSideComboBox.Items.Add(new ComboBoxItem("Nase: domaci A", TeamSide.HomeA));
        teamSideComboBox.Items.Add(new ComboBoxItem("Nase: hoste B", TeamSide.AwayB));
        teamSideComboBox.SelectedIndex = 0;
        toolbar.Controls.Add(teamSideComboBox, 1, 1);

        browseButton.Text = "Vybrat PDF";
        browseButton.Dock = DockStyle.Fill;
        StyleButton(browseButton, Color.White, Color.FromArgb(43, 54, 67));
        browseButton.Click += BrowseButton_Click;
        toolbar.Controls.Add(browseButton, 2, 1);

        analyzeButton.Text = "Analyzovat";
        analyzeButton.Dock = DockStyle.Fill;
        StyleButton(analyzeButton, PrimaryColor, Color.White);
        analyzeButton.Click += AnalyzeButton_Click;
        toolbar.Controls.Add(analyzeButton, 3, 1);

        addRowButton.Text = "Pridat hrace";
        addRowButton.Dock = DockStyle.Fill;
        StyleButton(addRowButton, Color.White, Color.FromArgb(43, 54, 67));
        addRowButton.Click += (_, _) => players.Add(new PlayerStats());
        toolbar.Controls.Add(addRowButton, 4, 1);

        saveRosterButton.Text = "Ulozit soup.";
        saveRosterButton.Dock = DockStyle.Fill;
        StyleButton(saveRosterButton, Color.White, Color.FromArgb(43, 54, 67));
        saveRosterButton.Click += SaveRosterButton_Click;
        toolbar.Controls.Add(saveRosterButton, 5, 1);

        exportButton.Text = "Export CSV";
        exportButton.Dock = DockStyle.Fill;
        StyleButton(exportButton, AccentColor, Color.White);
        exportButton.Click += ExportButton_Click;
        toolbar.Controls.Add(exportButton, 6, 1);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 560,
            BackColor = AppBackground,
            SplitterWidth = 12
        };
        root.Controls.Add(split, 0, 2);

        var previewPanel = CreateContentPanel("Nahled zapisu", "Oranzovy ramecek ukazuje ctenou soupisku.");
        split.Panel1.Controls.Add(previewPanel);

        var previewHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(232, 237, 243),
            Padding = new Padding(10)
        };
        previewPictureBox.Dock = DockStyle.Fill;
        previewPictureBox.BackColor = Color.White;
        previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        previewHost.Controls.Add(previewPictureBox);
        GetPanelBody(previewPanel).Controls.Add(previewHost);

        var tablePanel = CreateContentPanel("Statistiky hracu", "Cislo uprav rucne, jmeno se doplni ze soupisky.");
        split.Panel2.Controls.Add(tablePanel);
        rosterPathLabel.Dock = DockStyle.Bottom;
        rosterPathLabel.Height = 24;
        rosterPathLabel.ForeColor = MutedTextColor;
        rosterPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        rosterPathLabel.Text = $"Soupiska: {rosterStore.FilePath}";
        GetPanelBody(tablePanel).Controls.Add(rosterPathLabel);
        playersGrid.Dock = DockStyle.Fill;
        GetPanelBody(tablePanel).Controls.Add(playersGrid);

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.AutoEllipsis = true;
        statusLabel.Text = "Pripraveno.";
        statusLabel.ForeColor = MutedTextColor;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(statusLabel, 0, 3);
    }

    private void ConfigureGrid()
    {
        playersGrid.AutoGenerateColumns = false;
        playersGrid.AllowUserToAddRows = true;
        playersGrid.AllowUserToDeleteRows = true;
        playersGrid.DataSource = players;
        playersGrid.RowHeadersWidth = 28;
        playersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        playersGrid.CellEndEdit += PlayersGrid_CellEndEdit;
        playersGrid.BackgroundColor = PanelBackground;
        playersGrid.BorderStyle = BorderStyle.None;
        playersGrid.GridColor = Color.FromArgb(224, 230, 238);
        playersGrid.EnableHeadersVisualStyles = false;
        playersGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 247);
        playersGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(43, 54, 67);
        playersGrid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        playersGrid.ColumnHeadersHeight = 34;
        playersGrid.RowTemplate.Height = 30;
        playersGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
        playersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        playersGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(213, 228, 250);
        playersGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(24, 33, 44);

        playersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PlayerStats.PlayerName),
            HeaderText = "Jmeno",
            FillWeight = 180
        });
        playersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PlayerStats.Number),
            HeaderText = "Cislo",
            FillWeight = 60
        });
        playersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PlayerStats.TwoPointersMade),
            HeaderText = "2b",
            FillWeight = 50
        });
        playersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PlayerStats.ThreePointersMade),
            HeaderText = "3b",
            FillWeight = 50
        });
        playersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PlayerStats.FreeThrowsMade),
            HeaderText = "TH",
            FillWeight = 50
        });
        playersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PlayerStats.Fouls),
            HeaderText = "Fauly",
            FillWeight = 60
        });
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "PDF soubory (*.pdf)|*.pdf|Vsechny soubory (*.*)|*.*",
            Title = "Vyber basketbalovy zapis"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            pdfPathTextBox.Text = dialog.FileName;
        }
    }

    private void AnalyzeButton_Click(object? sender, EventArgs e)
    {
        var pdfPath = pdfPathTextBox.Text.Trim();
        if (!File.Exists(pdfPath))
        {
            MessageBox.Show(this, "Vyber existujici PDF soubor.", "Chybi PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            ToggleAnalysisUi(false);
            statusLabel.Text = "Nacitam PDF...";

            var selectedSide = ((ComboBoxItem)teamSideComboBox.SelectedItem!).Value;
            var result = analyzer.Analyze(pdfPath, selectedSide);

            currentResult?.Dispose();
            currentResult = result;

            previewPictureBox.Image = currentResult.PreviewImage;
            players.Clear();
            foreach (var player in currentResult.Players)
            {
                FillNameFromRoster(player, overwriteExistingName: true);
                players.Add(player);
            }

            statusLabel.Text = currentResult.Message;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Analyza selhala", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Analyza selhala.";
        }
        finally
        {
            ToggleAnalysisUi(true);
        }
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV soubor (*.csv)|*.csv",
            Title = "Exportovat statistiky",
            FileName = "statistiky.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildCsv(), Encoding.UTF8);
        statusLabel.Text = $"Export ulozen: {dialog.FileName}";
    }

    private void SaveRosterButton_Click(object? sender, EventArgs e)
    {
        playersGrid.EndEdit();
        var savedCount = rosterStore.SaveFrom(players);
        rosterStore.Load();
        statusLabel.Text = $"Soupiska ulozena ({savedCount} hracu): {rosterStore.FilePath}";
        rosterPathLabel.Text = $"Soupiska: {rosterStore.FilePath}";
    }

    private void PlayersGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        var numberColumn = playersGrid.Columns
            .Cast<DataGridViewColumn>()
            .FirstOrDefault(column => column.DataPropertyName == nameof(PlayerStats.Number));

        if (numberColumn is null || e.ColumnIndex != numberColumn.Index)
        {
            return;
        }

        if (playersGrid.Rows[e.RowIndex].DataBoundItem is PlayerStats player)
        {
            FillNameFromRoster(player, overwriteExistingName: true);
            players.ResetItem(e.RowIndex);
        }
    }

    private void FillNameFromRoster(PlayerStats player, bool overwriteExistingName)
    {
        if (!rosterStore.TryGetName(player.Number, out var knownName))
        {
            return;
        }

        if (overwriteExistingName || string.IsNullOrWhiteSpace(player.PlayerName))
        {
            player.PlayerName = knownName;
        }
    }

    private string BuildCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Jmeno,Cislo,2b,3b,TH,Fauly");

        foreach (var player in players)
        {
            builder.Append(EscapeCsv(player.PlayerName)).Append(',');
            builder.Append(EscapeCsv(player.Number)).Append(',');
            builder.Append(player.TwoPointersMade).Append(',');
            builder.Append(player.ThreePointersMade).Append(',');
            builder.Append(player.FreeThrowsMade).Append(',');
            builder.Append(player.Fouls).AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private void InitializeComponent()
    {

    }

    private Panel CreateHeaderPanel()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            Padding = new Padding(0, 0, 0, 12)
        };

        var title = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 420,
            Text = "Basketbalovy zapis",
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 38, 50),
            TextAlign = ContentAlignment.MiddleLeft
        };

        playerCountLabel.AutoSize = false;
        playerCountLabel.Dock = DockStyle.Right;
        playerCountLabel.Width = 220;
        playerCountLabel.TextAlign = ContentAlignment.MiddleRight;
        playerCountLabel.ForeColor = MutedTextColor;
        playerCountLabel.Font = new Font("Segoe UI", 10F);

        header.Controls.Add(title);
        header.Controls.Add(playerCountLabel);
        return header;
    }

    private static Panel CreatePanel()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelBackground,
            Margin = new Padding(0, 0, 0, 14)
        };
    }

    private Panel CreateContentPanel(string titleText, string subtitleText)
    {
        var panel = CreatePanel();
        panel.Padding = new Padding(14);

        var body = new Panel
        {
            Name = "Body",
            Dock = DockStyle.Fill,
            BackColor = PanelBackground,
            Padding = new Padding(0, 10, 0, 0)
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = PanelBackground
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = titleText,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 38, 50),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = subtitleText,
            ForeColor = MutedTextColor,
            TextAlign = ContentAlignment.MiddleLeft
        };

        header.Controls.Add(subtitle);
        header.Controls.Add(title);
        panel.Controls.Add(body);
        panel.Controls.Add(header);
        return panel;
    }

    private static Panel GetPanelBody(Panel panel)
    {
        return (Panel)panel.Controls["Body"]!;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            ForeColor = MutedTextColor,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void StyleButton(Button button, Color backColor, Color foreColor)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        button.Margin = new Padding(6, 0, 0, 0);
        button.Cursor = Cursors.Hand;
    }

    private void UpdatePlayerCount()
    {
        playerCountLabel.Text = $"Hracich radku: {players.Count}";
    }

    private void ToggleAnalysisUi(bool enabled)
    {
        analyzeButton.Enabled = enabled;
        browseButton.Enabled = enabled;
        exportButton.Enabled = enabled;
        addRowButton.Enabled = enabled;
        saveRosterButton.Enabled = enabled;
        teamSideComboBox.Enabled = enabled;
    }

    private sealed record ComboBoxItem(string Text, TeamSide Value)
    {
        public override string ToString() => Text;
    }
}
