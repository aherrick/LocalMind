namespace LocalMind.Models;

public record LocalModel(string ProviderId, string ProviderName, string Id, string DisplayName)
{
    public bool SupportsTools { get; init; }
    public string Label => $"{ProviderName} · {DisplayName}";
}
