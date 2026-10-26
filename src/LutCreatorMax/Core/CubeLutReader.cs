using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace LutCreatorMax.Core;

public sealed class CubeLutLoadResult
{
    public required Lut3D Lut { get; init; }
    public string? Title { get; init; }
    public int SourceSize { get; init; }
}

/// <summary>Parses Adobe / Photoshop .cube 3D LUTs and resamples to 16^3 for Max.</summary>
public static class CubeLutReader
{
    public static Lut3D Load(string path) => LoadDetailed(path).Lut;

    public static CubeLutLoadResult LoadDetailed(string path)
    {
        var lines = File.ReadAllLines(path);
        int size = 0;
        string? title = null;
        var values = new List<Rgb>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase))
            {
                title = ParseTitle(line);
                continue;
            }
            if (line.StartsWith("LUT_1D_SIZE", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("1D .cube LUTs are not supported; export a 3D Color Lookup (.CUBE) from Photoshop.");
            if (line.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
            {
                string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                size = int.Parse(tokens[1], CultureInfo.InvariantCulture);
                continue;
            }
            if (line.StartsWith("DOMAIN_", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            {
                values.Add(new Rgb(r, g, b));
            }
        }

        if (size <= 1)
            throw new InvalidDataException("Missing or invalid LUT_3D_SIZE in .cube file.");

        int expected = size * size * size;
        if (values.Count < expected)
            throw new InvalidDataException($"Expected {expected} RGB triples, found {values.Count}.");

        Lut3D lut;
        if (size == Lut3D.Size)
        {
            var data = new Rgb[Lut3D.EntryCount];
            for (int i = 0; i < Lut3D.EntryCount; i++)
                data[i] = values[i];
            lut = new Lut3D(data);
        }
        else
        {
            lut = Resample(values, size);
        }

        return new CubeLutLoadResult
        {
            Lut = lut,
            Title = title,
            SourceSize = size
        };
    }

    private static string? ParseTitle(string line)
    {
        // TITLE "Going for a walk"  or  TITLE Going for a walk
        var match = Regex.Match(line, "^TITLE\\s+\"([^\"]*)\"\\s*$", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        int idx = line.IndexOf(' ');
        if (idx < 0) return null;
        return line[(idx + 1)..].Trim().Trim('"');
    }

    private static Lut3D Resample(List<Rgb> source, int size)
    {
        var lut = new Lut3D();
        float scale = 1f / (Lut3D.Size - 1);
        for (int b = 0; b < Lut3D.Size; b++)
        for (int g = 0; g < Lut3D.Size; g++)
        for (int r = 0; r < Lut3D.Size; r++)
        {
            float rf = r * scale * (size - 1);
            float gf = g * scale * (size - 1);
            float bf = b * scale * (size - 1);
            lut[r, g, b] = SampleSource(source, size, rf, gf, bf);
        }
        return lut;
    }

    private static Rgb SampleSource(List<Rgb> source, int size, float rf, float gf, float bf)
    {
        int r0 = (int)MathF.Floor(rf);
        int g0 = (int)MathF.Floor(gf);
        int b0 = (int)MathF.Floor(bf);
        int r1 = Math.Min(r0 + 1, size - 1);
        int g1 = Math.Min(g0 + 1, size - 1);
        int b1 = Math.Min(b0 + 1, size - 1);
        float tr = rf - r0;
        float tg = gf - g0;
        float tb = bf - b0;

        Rgb Get(int ri, int gi, int bi) => source[bi * size * size + gi * size + ri];

        Rgb c00 = Rgb.Lerp(Get(r0, g0, b0), Get(r1, g0, b0), tr);
        Rgb c10 = Rgb.Lerp(Get(r0, g1, b0), Get(r1, g1, b0), tr);
        Rgb c01 = Rgb.Lerp(Get(r0, g0, b1), Get(r1, g0, b1), tr);
        Rgb c11 = Rgb.Lerp(Get(r0, g1, b1), Get(r1, g1, b1), tr);
        Rgb c0 = Rgb.Lerp(c00, c10, tg);
        Rgb c1 = Rgb.Lerp(c01, c11, tg);
        return Rgb.Lerp(c0, c1, tb);
    }
}
