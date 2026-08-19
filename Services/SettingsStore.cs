using System.Text.Json;
using LocalMind.Models;

namespace LocalMind.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalMind", "settings.json");

    public SettingsStore() => Directory.CreateDirectory(Path.GetDirectoryName(_path));

    public AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
        => File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
}
