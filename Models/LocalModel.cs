namespace LocalMind.Models;

public record LocalModel(string ProviderId, string ProviderName, string Id, string DisplayName)
{
    public string Label => $"{ProviderName} · {DisplayName}";
}
