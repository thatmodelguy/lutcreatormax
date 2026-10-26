using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using LutCreatorMax.Core;

namespace LutCreatorMax.Services;

public sealed class MaxCatalogInfo
{
    public required string LutFolder { get; init; }
    public int NameCount { get; init; }
    public int NextFilterIndex { get; init; }
    public IReadOnlyList<string> ExistingPngNames { get; init; } = Array.Empty<string>();
}

public sealed class ExportResult
{
    public required string LutPngPath { get; init; }
    public required string ThumbnailPath { get; init; }
    public string? NamesSnippetPath { get; init; }
    public string? Message { get; init; }
    public bool InstalledToMax { get; init; }
}

public static class MaxExportService
{
    public const string DefaultMaxLutPath =
        @"C:\Program Files (x86)\Steam\steamapps\common\GameGuru MAX\Files\editors\lut";

    public static MaxCatalogInfo ReadCatalog(string lutFolder)
    {
        string namesPath = Path.Combine(lutFolder, "filternames.txt");
        int nameCount = File.Exists(namesPath) ? File.ReadAllLines(namesPath).Length : 0;

        var pngs = Directory.Exists(lutFolder)
            ? Directory.GetFiles(lutFolder, "*.png")
                .Select(Path.GetFileName)
                .Where(n => n != null)
                .Cast<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        return new MaxCatalogInfo
        {
            LutFolder = lutFolder,
            NameCount = nameCount,
            NextFilterIndex = nameCount,
            ExistingPngNames = pngs
        };
    }

    public static string SanitizeFileName(string name)
    {
        name = name.Trim().ToLowerInvariant();
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Replace(' ', '_');
        if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            name += ".png";
        return name;
    }

    public static bool WouldInsertMidList(string lutFolder, string fileName)
    {
        var info = ReadCatalog(lutFolder);
        if (info.ExistingPngNames.Count == 0)
            return false;
        string last = info.ExistingPngNames[^1];
        return string.Compare(fileName, last, StringComparison.OrdinalIgnoreCase) < 0;
    }

    public static ExportResult ExportPackage(
        Lut3D lut,
        Bitmap gradedPreview,
        string outputFolder,
        string fileName,
        string displayName)
    {
        Directory.CreateDirectory(outputFolder);
        string safeName = SanitizeFileName(fileName);
        string pngPath = Path.Combine(outputFolder, safeName);
        LutStrip.SavePng(lut, pngPath);

        string thumbPath = Path.Combine(outputFolder, "thumbnail.jpg");
        SaveThumbnail(gradedPreview, thumbPath);

        string snippetPath = Path.Combine(outputFolder, "filternames_append.txt");
        File.WriteAllText(snippetPath, displayName.Trim() + Environment.NewLine);

        using (var check = new Bitmap(pngPath))
        {
            if (check.Width != LutStrip.Width || check.Height != LutStrip.Height)
                throw new InvalidOperationException("Exported LUT strip failed size validation.");
        }

        return new ExportResult
        {
            LutPngPath = pngPath,
            ThumbnailPath = thumbPath,
            NamesSnippetPath = snippetPath,
            Message = $"Exported package to {outputFolder}",
            InstalledToMax = false
        };
    }

    public static ExportResult InstallToMax(
        Lut3D lut,
        Bitmap gradedPreview,
        string lutFolder,
        string fileName,
        string displayName)
    {
        if (!Directory.Exists(lutFolder))
            throw new DirectoryNotFoundException($"Max LUT folder not found: {lutFolder}");

        string safeName = SanitizeFileName(fileName);
        string pngPath = Path.Combine(lutFolder, safeName);
        if (File.Exists(pngPath))
            throw new IOException($"A LUT named '{safeName}' already exists in Max.");

        string thumbDir = Path.Combine(lutFolder, "thumbnails");
        Directory.CreateDirectory(thumbDir);

        var catalog = ReadCatalog(lutFolder);
        bool midList = WouldInsertMidList(lutFolder, safeName);
        int filterIndex = catalog.NameCount;
        string thumbPath = Path.Combine(thumbDir, $"Filter{filterIndex:D2}.jpg");

        try
        {
            LutStrip.SavePng(lut, pngPath);
            SaveThumbnail(gradedPreview, thumbPath);

            string namesPath = Path.Combine(lutFolder, "filternames.txt");
            string existing = File.Exists(namesPath) ? File.ReadAllText(namesPath) : "None\r\n";
            if (!existing.EndsWith('\n') && existing.Length > 0)
                existing += Environment.NewLine;
            File.WriteAllText(namesPath, existing + displayName.Trim() + Environment.NewLine);
        }
        catch (UnauthorizedAccessException)
        {
            try { if (File.Exists(pngPath)) File.Delete(pngPath); } catch { /* ignore */ }
            try { if (File.Exists(thumbPath)) File.Delete(thumbPath); } catch { /* ignore */ }
            throw;
        }

        string warn = midList
            ? " Warning: filename sorts before the last existing LUT and may shift Max's Filter index mapping for later entries. Prefer a name like z_mylut.png."
            : "";

        return new ExportResult
        {
            LutPngPath = pngPath,
            ThumbnailPath = thumbPath,
            Message = $"Installed as Filter{filterIndex:D2} ({displayName}).{warn}",
            InstalledToMax = true
        };
    }

    private static void SaveThumbnail(Bitmap gradedPreview, string path)
    {
        using var thumb = ImageBridge.ResizeCrop(gradedPreview, 128, 128);
        thumb.Save(path, ImageFormat.Jpeg);
    }
}
