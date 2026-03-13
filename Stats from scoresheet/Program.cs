using System;
using System.IO;
using PdfiumViewer;
using Tesseract;
using OpenCvSharp;

class Program
{
    static void Main(string[] args)
    {
        string pdfPath = "testing pdfs/zapis.pdf";

        using var document = PdfDocument.Load(pdfPath);

        using var image = document.Render(
            0,
            3000,
            4000,
            300,
            300,
            PdfRenderFlags.Annotations
        );

        image.Save("page.png");

        Console.WriteLine("PDF rendered into image");

        using var engine = new TesseractEngine(@"./tessdata", "ces", EngineMode.Default);
        engine.SetVariable("tessedit_char_whitelist", "0123456789");
        engine.DefaultPageSegMode = PageSegMode.SingleWord;

        Console.WriteLine("Running OpenCV line detection...");

        Mat src = Cv2.ImRead("page.png");

        Mat gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        Mat edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);

        LineSegmentPoint[] lines = Cv2.HoughLinesP(
            edges,
            1,
            Math.PI / 180,
            150,
            minLineLength: 1000,
            maxLineGap: 20
        );

        foreach (var line in lines)
        {
            int dx = Math.Abs(line.P1.X - line.P2.X);
            int dy = Math.Abs(line.P1.Y - line.P2.Y);

            // horizontal line
            if (dy < 10)
            {
                Cv2.Line(src, line.P1, line.P2, new Scalar(0, 255, 0), 4);
            }

            // vertical line
            else if (dx < 10)
            {
                Cv2.Line(src, line.P1, line.P2, new Scalar(255, 0, 0), 4);
            }
        }

        Cv2.ImWrite("lines_detected.png", src);

        Console.WriteLine($"Detected {lines.Length} lines.");
        Console.WriteLine("Lines drawn on image saved as lines_detected.png");

        OpenCvSharp.Rect scoreArea = new OpenCvSharp.Rect(1300, 600, 1600, 3000);

        Mat scoreTable = new Mat(src, scoreArea);

        Cv2.ImWrite("score_table.png", scoreTable);

        Console.WriteLine("Score table extracted.");

        Mat table = Cv2.ImRead("score_table.png");

        Mat grayTable = new Mat();
        Cv2.CvtColor(table, grayTable, ColorConversionCodes.BGR2GRAY);

        Mat binary = new Mat();
        Cv2.AdaptiveThreshold(
            grayTable,
            binary,
            255,
            AdaptiveThresholdTypes.MeanC,
            ThresholdTypes.BinaryInv,
            15,
            10
        );

        // cleaning out 
        Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);

        //finding cell contours
        Point[][] contours;
        HierarchyIndex[] hierarchy;

        Mat horizontalKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(120, 1));
        Mat horizontal = new Mat();
        Cv2.Erode(binary, horizontal, horizontalKernel);
        Cv2.Dilate(horizontal, horizontal, horizontalKernel);

        Mat verticalKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 120));
        Mat vertical = new Mat();
        Cv2.Erode(binary, vertical, verticalKernel);
        Cv2.Dilate(vertical, vertical, verticalKernel);

        Mat grid = new Mat();
        Cv2.Add(horizontal, vertical, grid);

        Mat cleanKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.Dilate(grid, grid, cleanKernel);

        Cv2.ImWrite("grid.png", grid);

        // CONVERSION TO 1-CHANNEL IMAGE
        Mat gridGray = grid.Clone();

        Console.WriteLine($"Grid channels: {grid.Channels()}");

        // ===== COLUMN DETECTION =====

        var verticalPositions = new List<int>();

        for (int x = 0; x < gridGray.Width; x++)
        {
            int sum = 0;

            for (int y = 0; y < gridGray.Height; y++)
            {
                if (gridGray.At<byte>(y, x) > 0)
                { 
                    sum++; 
                }
            }

            if (sum > gridGray.Height * 0.15)
            { 
                verticalPositions.Add(x); 
            }
        }

        var columns = FilterGridLines(verticalPositions, 60);


        // ===== ROW DETECTION =====

        var horizontalPositions = new List<int>();

        for (int y = 0; y < gridGray.Height; y++)
        {
            int sum = 0;

            for (int x = 0; x < gridGray.Width; x++)
            {
                if (gridGray.At<byte>(y, x) > 0)
                { 
                    sum++; 
                }
            }

            if (sum > gridGray.Width * 0.2)
            { 
                horizontalPositions.Add(y); 
            }
        }

        var rows = FilterGridLines(horizontalPositions, 35);

        Console.WriteLine($"Vertical candidates: {verticalPositions.Count}");
        Console.WriteLine($"Horizontal candidates: {horizontalPositions.Count}");
        Console.WriteLine($"Detected {columns.Count} columns and {rows.Count} rows in the score table.");

        Console.WriteLine("Column spacing:");
        for (int i = 1; i < columns.Count; i++)
        {
            Console.WriteLine(columns[i] - columns[i - 1]);
        }

        Console.WriteLine("Row spacing:");
        for (int i = 1; i < rows.Count; i++)
        {
            Console.WriteLine(rows[i] - rows[i - 1]);
        }

        columns = NormalizeGrid(columns, 20);
        rows = NormalizeGrid(rows, 43);

        // ===== CELL EXTRACTION =====

        int index = 0;

        for (int r = 0; r < rows.Count - 1; r++)
        {
            for (int c = 0; c < columns.Count - 1; c++)
            {
                int x = columns[c];
                int y = rows[r];

                int width = columns[c + 1] - columns[c];
                int height = rows[r + 1] - rows[r];

                if (width < 10 || height < 10)
                    continue;

                var rect = new OpenCvSharp.Rect(x, y, width, height);

                var cell = new Mat(table, rect);

                string filename = $"cell_{r}_{c}.png";

                Cv2.ImWrite(filename, cell);

                index++;
            }
        }

        Console.WriteLine($"Cells extracted: {index}");

        for (int r = 0; r < 43; r++)
        {
            for (int block = 0; block < 4; block++)
            {
                int baseCol = block * 5;

                string aPlayer = $"cell_{r}_{baseCol}.png";
                string aScore = $"cell_{r}_{baseCol + 1}.png";
                string minute = $"cell_{r}_{baseCol + 2}.png";
                string bPlayer = $"cell_{r}_{baseCol + 3}.png";
                string bScore = $"cell_{r}_{baseCol + 4}.png";

                Console.WriteLine($"{aPlayer} {aScore} {minute} {bPlayer} {bScore}");
            }
        }

        Console.WriteLine("Reading table values...");

        for (int r = 0; r < 43; r++)
        {
            for (int block = 0; block < 4; block++)
            {
                int baseCol = block * 5;

                string aPlayer = ReadNumber($"cell_{r}_{baseCol}.png", engine);
                string aScore = ReadNumber($"cell_{r}_{baseCol + 1}.png", engine);
                string minute = ReadNumber($"cell_{r}_{baseCol + 2}.png", engine);
                string bPlayer = ReadNumber($"cell_{r}_{baseCol + 3}.png", engine);
                string bScore = ReadNumber($"cell_{r}_{baseCol + 4}.png", engine);

                Console.WriteLine($"{r} | {aPlayer} {aScore} {minute} {bPlayer} {bScore}");
            }
        }
    }

    static List<int> FilterGridLines(List<int> lines, int minSpacing)
    {
        List<int> filtered = new();

        foreach (var pos in lines)
        {
            if (filtered.Count == 0 || pos - filtered.Last() > minSpacing)
                filtered.Add(pos);
        }

        return filtered;
    }

    static List<int> NormalizeGrid(List<int> lines, int expectedCount)
    {
        int step = (lines.Last() - lines.First()) / expectedCount;

        List<int> normalized = new();

        for (int i = 0; i <= expectedCount; i++)
            normalized.Add(lines.First() + i * step);

        return normalized;
    }

    static string ReadNumber(string file, TesseractEngine engine)
    {
        Mat img = Cv2.ImRead(file);

        Mat resized = new Mat();
        Cv2.Resize(img, resized, new Size(), 2, 2, InterpolationFlags.Cubic);

        string temp = "temp.png";
        Cv2.ImWrite(temp, resized);

        using var pix = Pix.LoadFromFile(temp);
        using var page = engine.Process(pix);

        return page.GetText().Trim();
    }
}