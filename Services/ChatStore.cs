using System.Text.Json;
using LocalMind.Models;

namespace LocalMind.Services;

public sealed class ChatStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalMind", "chats");

    public ChatStore() => Directory.CreateDirectory(_dir);

    public IReadOnlyList<Chat> Load()
    {
        List<Chat> chats = [];
        foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
        {
            try
            {
                var chat = JsonSerializer.Deserialize<Chat>(File.ReadAllText(file), Options);
                if (chat is not null)
                {
                    chats.Add(chat);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning($"Failed to load chat file '{file}'.", ex);
            }
        }
        chats.Sort((left, right) => right.UpdatedAt.CompareTo(left.UpdatedAt));
        return chats;
    }

    public void Save(Chat chat)
    {
        var path = Path.Combine(_dir, chat.Id + ".json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(chat, Options));
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to save chat file '{path}'.", ex);
        }
    }

    public void Delete(Chat chat)
    {
        var path = Path.Combine(_dir, chat.Id + ".json");
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to delete chat file '{path}'.", ex);
            }
        }
    }
}
