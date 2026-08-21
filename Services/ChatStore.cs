using System.Text.Json;
using LocalMind.Models;

namespace LocalMind.Services;

public static class ChatStore
{
    private const int ConversationFileFormatVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalMind", "chats");

    static ChatStore() => Directory.CreateDirectory(DirectoryPath);

    public static IReadOnlyList<Chat> Load()
    {
        List<Chat> chats = [];
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.json"))
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

    public static void Save(Chat chat)
    {
        var path = Path.Combine(DirectoryPath, chat.Id + ".json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(chat, Options));
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to save chat file '{path}'.", ex);
        }
    }

    public static void Delete(Chat chat)
    {
        var path = Path.Combine(DirectoryPath, chat.Id + ".json");
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to delete chat file '{path}'.", ex);
        }
    }

    public static Task Export(Chat chat, string path)
    {
        var document = new ConversationFile { Chat = chat };
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(document, Options));
    }

    private sealed class ConversationFile
    {
        public int FormatVersion { get; set; } = ConversationFileFormatVersion;
        public Chat Chat { get; set; }
    }
}
