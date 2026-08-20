using System.Text.Json;
using LocalMind.Models;

namespace LocalMind.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalMind");
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalMind", "settings.json");

    public SettingsStore() => Directory.CreateDirectory(_dir);

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Failed to load settings file '{_path}'.", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to save settings file '{_path}'.", ex);
        }
    }
}
