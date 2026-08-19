namespace LocalMind.Models;

public class Chat
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Title { get; set; } = "New Chat";
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string ModelDisplayName { get; set; } = "";
    public bool SupportsTools { get; set; }
    public bool IsPinned { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public List<StoredMessage> Messages { get; set; } = [];
}

public class StoredMessage
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}
