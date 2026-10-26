using System.IO;
using System.Text.Json;

namespace LutCreatorMax.Services;

public sealed class AppSettings
{
    public string MaxLutPath { get; set; } = MaxExportService.DefaultMaxLutPath;
    public string? LastImageFolder { get; set; }
    public string? LastLutImportFolder { get; set; }
    public string? LastExportFolder { get; set; }
    public string DefaultFileNamePrefix { get; set; } = "z_";
    public string PreviewMode { get; set; } = "After";
    public string? LastPresetName { get; set; }
}

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LutCreatorMax");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // fall through to defaults
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
