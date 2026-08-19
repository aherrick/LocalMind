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
            catch
            {
            }
        }
            chats.Sort((left, right) => right.UpdatedAt.CompareTo(left.UpdatedAt));
            return chats;
    }

    public void Save(Chat chat)
        => File.WriteAllText(Path.Combine(_dir, chat.Id + ".json"), JsonSerializer.Serialize(chat, Options));

    public void Delete(Chat chat)
    {
        var path = Path.Combine(_dir, chat.Id + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
