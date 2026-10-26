using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
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
    public string? AssignedFileName { get; init; }
    public string? Message { get; init; }
    public bool InstalledToMax { get; init; }
    public bool FileNameWasAdjusted { get; init; }
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

    public static string StemFromFileName(string fileName)
    {
        string name = SanitizeFileName(fileName);
        name = Path.GetFileNameWithoutExtension(name);
        // Strip leading sort prefixes like zz_058_
        name = Regex.Replace(name, @"^zz_\d+_", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"^z+", "", RegexOptions.IgnoreCase).TrimStart('_');
        if (string.IsNullOrWhiteSpace(name))
            name = "mylut";
        return name;
    }

    /// <summary>
    /// Picks a PNG name that sorts after every existing Max LUT so Filter indices stay aligned
    /// with filternames.txt append order. Uses zz_NNN_stem.png when needed.
    /// </summary>
    public static string EnsureAppendFriendlyFileName(string lutFolder, string preferredName)
    {
        var catalog = ReadCatalog(lutFolder);
        string preferred = SanitizeFileName(preferredName);
        string stem = StemFromFileName(preferred);

        bool PreferredOk() =>
            !File.Exists(Path.Combine(lutFolder, preferred))
            && (catalog.ExistingPngNames.Count == 0
                || string.Compare(preferred, catalog.ExistingPngNames[^1], StringComparison.OrdinalIgnoreCase) > 0);

        if (PreferredOk())
            return preferred;

        int seq = Math.Max(catalog.NextFilterIndex, catalog.ExistingPngNames.Count);
        // Also continue past any existing zz_NNN_ sequence numbers
        foreach (string existing in catalog.ExistingPngNames)
        {
            var m = Regex.Match(existing, @"^zz_(\d+)_", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
                seq = Math.Max(seq, n + 1);
        }

        for (int attempt = 0; attempt < 1000; attempt++)
        {
            string candidate = SanitizeFileName($"zz_{seq:D3}_{stem}");
            bool sortsLast = catalog.ExistingPngNames.Count == 0
                || string.Compare(candidate, catalog.ExistingPngNames[^1], StringComparison.OrdinalIgnoreCase) > 0;
            bool free = !File.Exists(Path.Combine(lutFolder, candidate));
            if (sortsLast && free)
                return candidate;
            seq++;
        }

        // Last resort: timestamp guarantees uniqueness and late sort under zz_
        return SanitizeFileName($"zz_{DateTime.Now:yyyyMMddHHmmss}_{stem}");
    }

    public static bool WouldInsertMidList(string lutFolder, string fileName)
    {
        var info = ReadCatalog(lutFolder);
        if (info.ExistingPngNames.Count == 0)
            return false;
        string last = info.ExistingPngNames[^1];
        return string.Compare(SanitizeFileName(fileName), last, StringComparison.OrdinalIgnoreCase) < 0;
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
            AssignedFileName = safeName,
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

        string preferred = SanitizeFileName(fileName);
        string safeName = EnsureAppendFriendlyFileName(lutFolder, preferred);
        bool adjusted = !string.Equals(safeName, preferred, StringComparison.OrdinalIgnoreCase);

        string pngPath = Path.Combine(lutFolder, safeName);
        if (File.Exists(pngPath))
            throw new IOException($"A LUT named '{safeName}' already exists in Max.");

        string thumbDir = Path.Combine(lutFolder, "thumbnails");
        Directory.CreateDirectory(thumbDir);

        var catalog = ReadCatalog(lutFolder);
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

        string adjustNote = adjusted
            ? $" Auto-named '{safeName}' so it sorts last and keeps Max filter order."
            : "";

        return new ExportResult
        {
            LutPngPath = pngPath,
            ThumbnailPath = thumbPath,
            AssignedFileName = safeName,
            FileNameWasAdjusted = adjusted,
            Message = $"Installed as Filter{filterIndex:D2} ({displayName}) → {safeName}.{adjustNote}",
            InstalledToMax = true
        };
    }

    private static void SaveThumbnail(Bitmap gradedPreview, string path)
    {
        using var thumb = ImageBridge.ResizeCrop(gradedPreview, 128, 128);
        thumb.Save(path, ImageFormat.Jpeg);
    }
}
