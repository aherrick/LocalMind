using LocalMind.Models;
using Microsoft.Extensions.AI;

namespace LocalMind.Providers;

public interface ILocalModelProvider
{
    string Id { get; }
    string DisplayName { get; }

    Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken cancellationToken = default);

    Task<IChatClient> CreateChatClientAsync(string modelId, CancellationToken cancellationToken = default);
}
