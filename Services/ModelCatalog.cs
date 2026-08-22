using System.Collections.ObjectModel;
using LocalMind.Models;
using LocalMind.Providers;
using Microsoft.Extensions.AI;
using Microsoft.UI.Dispatching;

namespace LocalMind.Services;

// Owns provider discovery: periodically probes every provider and keeps ReadyModels current so the
// chat picker and Settings reflect models coming online/offline without any polling in the view-models.
public sealed class ModelCatalog
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyList<ILocalModelProvider> _providers;
    private readonly DispatcherQueueTimer _timer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ObservableCollection<LocalModel> ReadyModels { get; } = [];

    // Raised after each scan with the per-provider status snapshot (provider id -> status).
    public event Action<IReadOnlyDictionary<string, LocalProviderStatus>> Refreshed;

    public ModelCatalog(IReadOnlyList<ILocalModelProvider> providers)
    {
        _providers = providers;
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = RefreshInterval;
        _timer.Tick += async (_, _) => await Refresh();
    }

    public void Start() => _timer.Start();

    public async Task Refresh()
    {
        // Skip if a scan is already running so overlapping timer ticks don't stack.
        if (!await _gate.WaitAsync(0))
        {
            return;
        }
        try
        {
            var results = await Task.WhenAll(_providers.Select(p => p.GetStatusAsync()));
            Sync([.. results.SelectMany(status => status.Models)]);
            Refreshed?.Invoke(_providers.Zip(results).ToDictionary(pair => pair.First.Id, pair => pair.Second));
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<IChatClient> CreateChatClient(string providerId, string modelId)
        => _providers.First(p => p.Id == providerId).CreateChatClientAsync(modelId);

    // Incremental update (not Clear+re-add) so an open picker and the current selection are preserved.
    private void Sync(List<LocalModel> incoming)
    {
        for (int i = ReadyModels.Count - 1; i >= 0; i--)
        {
            if (!incoming.Contains(ReadyModels[i]))
            {
                ReadyModels.RemoveAt(i);
            }
        }
        for (int i = 0; i < incoming.Count; i++)
        {
            if (!ReadyModels.Contains(incoming[i]))
            {
                ReadyModels.Insert(Math.Min(i, ReadyModels.Count), incoming[i]);
            }
        }
    }
}
