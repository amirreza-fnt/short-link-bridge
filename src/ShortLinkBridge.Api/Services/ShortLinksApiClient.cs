using System.Net.Http.Json;
using ShortLinkBridge.Api.Models;

namespace ShortLinkBridge.Api.Services;

public sealed class ShortLinksApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<BatchCreateLinkResult>> CreateBatchAsync(
        IReadOnlyList<BatchCreateLinkItem> items,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
            return Array.Empty<BatchCreateLinkResult>();

        var request = new BatchCreateLinksRequest { Items = items.ToList() };
        using var response = await http.PostAsJsonAsync("/api/links/batch", request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<BatchCreateLinksResponse>(ct);
        return payload?.Results ?? new List<BatchCreateLinkResult>();
    }
}
