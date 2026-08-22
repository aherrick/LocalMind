using LocalMind.Models;
using Microsoft.Extensions.AI;

namespace LocalMind.Providers;

public record LocalProviderStatus(bool IsAvailable, IReadOnlyList<LocalModel> Models);

public interface ILocalModelProvider
{
    string Id { get; }
    string DisplayName { get; }

    Task<LocalProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<IChatClient> CreateChatClientAsync(string modelId, CancellationToken cancellationToken = default);
}
