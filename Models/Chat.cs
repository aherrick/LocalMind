namespace LocalMind.Models;

public class Chat
{
    public Chat()
    {
        Id = Guid.NewGuid().ToString("n");
        Title = "New Chat";
        ProviderId = "";
        ModelId = "";
        ModelDisplayName = "";
        CreatedAt = DateTimeOffset.Now;
        UpdatedAt = DateTimeOffset.Now;
        Messages = [];
    }

    public string Id { get; set; }
    public string Title { get; set; }
    public string ProviderId { get; set; }
    public string ModelId { get; set; }
    public string ModelDisplayName { get; set; }
    public bool IsPinned { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<StoredMessage> Messages { get; set; }
}

public class StoredMessage
{
    public StoredMessage()
    {
        Role = "user";
        Text = "";
        Timestamp = DateTimeOffset.Now;
    }

    public string Role { get; set; }
    public string Text { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
