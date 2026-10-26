using System.IO;
using System.Text.Json;
using LutCreatorMax.Core;

namespace LutCreatorMax.Services;

public static class PresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string PresetsDirectory =>
        Path.Combine(AppSettingsStore.SettingsDirectory, "Presets");

    public static IReadOnlyList<LutPreset> LoadAll()
    {
        Directory.CreateDirectory(PresetsDirectory);
        var list = new List<LutPreset>();
        list.AddRange(CreateBuiltIns());

        foreach (string file in Directory.GetFiles(PresetsDirectory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var preset = JsonSerializer.Deserialize<LutPreset>(File.ReadAllText(file));
                if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
                    continue;
                preset.BuiltIn = false;
                // Don't duplicate built-in names from disk
                if (list.Any(p => p.BuiltIn && string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                list.Add(preset);
            }
            catch
            {
                // skip bad files
            }
        }

        return list;
    }

    public static void SaveUserPreset(LutPreset preset)
    {
        Directory.CreateDirectory(PresetsDirectory);
        preset.BuiltIn = false;
        string safe = string.Join("_", preset.Name.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(PresetsDirectory, safe + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));
    }

    public static bool DeleteUserPreset(string name)
    {
        string safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(PresetsDirectory, safe + ".json");
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    public static IEnumerable<LutPreset> CreateBuiltIns()
    {
        yield return new LutPreset
        {
            Name = "Identity",
            BuiltIn = true,
            Grade = new GradeSnapshot()
        };

        yield return new LutPreset
        {
            Name = "Warm Sunset",
            BuiltIn = true,
            Grade = new GradeSnapshot
            {
                Temperature = 0.45f,
                Tint = 0.05f,
                Contrast = 1.15f,
                Saturation = 1.1f,
                Highlights = -0.15f,
                Shadows = 0.1f,
                SCurve = 0.25f,
                HighlightRolloff = 0.35f,
                SplitHighlightHue = 35f,
                SplitHighlightSat = 0.35f,
                SplitShadowHue = 220f,
                SplitShadowSat = 0.15f,
                DisplayName = "Warm Sunset",
                FileName = "z_warmsunset"
            }
        };

        yield return new LutPreset
        {
            Name = "Cold Night",
            BuiltIn = true,
            Grade = new GradeSnapshot
            {
                Temperature = -0.55f,
                Exposure = -0.35f,
                Contrast = 1.2f,
                Saturation = 0.85f,
                Shadows = -0.2f,
                BlackCrush = 0.25f,
                SplitShadowHue = 210f,
                SplitShadowSat = 0.4f,
                SplitHighlightHue = 200f,
                SplitHighlightSat = 0.15f,
                DisplayName = "Cold Night",
                FileName = "z_coldnight"
            }
        };

        yield return new LutPreset
        {
            Name = "Teal Orange",
            BuiltIn = true,
            Grade = new GradeSnapshot
            {
                Contrast = 1.2f,
                Saturation = 1.05f,
                SCurve = 0.3f,
                SplitShadowHue = 185f,
                SplitShadowSat = 0.45f,
                SplitHighlightHue = 30f,
                SplitHighlightSat = 0.4f,
                SplitBalance = 0.1f,
                DisplayName = "Teal Orange",
                FileName = "z_tealorange"
            }
        };

        yield return new LutPreset
        {
            Name = "Bleach Bypass",
            BuiltIn = true,
            Grade = new GradeSnapshot
            {
                Contrast = 1.35f,
                Saturation = 0.45f,
                SCurve = 0.4f,
                BlackCrush = 0.2f,
                HighlightRolloff = 0.2f,
                DisplayName = "Bleach Bypass",
                FileName = "z_bleachbypass"
            }
        };

        yield return new LutPreset
        {
            Name = "Faded Film",
            BuiltIn = true,
            Grade = new GradeSnapshot
            {
                Contrast = 0.9f,
                Saturation = 0.8f,
                Fade = 0.55f,
                HighlightRolloff = 0.4f,
                Temperature = 0.15f,
                SCurve = -0.15f,
                DisplayName = "Faded Film",
                FileName = "z_fadedfilm"
            }
        };

        yield return new LutPreset
        {
            Name = "Dramatic BW",
            BuiltIn = true,
            Grade = new GradeSnapshot
            {
                Saturation = 0f,
                Contrast = 1.4f,
                SCurve = 0.45f,
                BlackCrush = 0.3f,
                Highlights = 0.1f,
                Shadows = -0.15f,
                DisplayName = "Dramatic BW",
                FileName = "z_dramaticbw"
            }
        };
    }
}
