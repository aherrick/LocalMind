using System.Text.Json;
using LocalMind.Models;

namespace LocalMind.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalMind");
    private static readonly string SettingsPath = Path.Combine(DirectoryPath, "settings.json");

    static SettingsStore() => Directory.CreateDirectory(DirectoryPath);

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Failed to load settings file '{SettingsPath}'.", ex);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to save settings file '{SettingsPath}'.", ex);
        }
    }
}
