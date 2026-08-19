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

        if ((int)response.StatusCode == 405)
            return await CreateOneByOneAsync(items, ct);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<BatchCreateLinksResponse>(ct);
        return payload?.Results ?? new List<BatchCreateLinkResult>();
    }

    private async Task<IReadOnlyList<BatchCreateLinkResult>> CreateOneByOneAsync(
        IReadOnlyList<BatchCreateLinkItem> items,
        CancellationToken ct)
    {
        var results = new List<BatchCreateLinkResult>(items.Count);
        foreach (var item in items)
        {
            using var response = await http.PostAsJsonAsync("/api/links", new { url = item.Url, groupName = item.GroupName }, ct);
            if (!response.IsSuccessStatusCode)
            {
                results.Add(new BatchCreateLinkResult
                {
                    Url = item.Url,
                    Error = $"HTTP {(int)response.StatusCode}"
                });
                continue;
            }

            var created = await response.Content.ReadFromJsonAsync<CreatedLink>(ct);
            results.Add(new BatchCreateLinkResult
            {
                Url = item.Url,
                ShortUrl = created?.ShortUrl
            });
        }

        return results;
    }

    private sealed class CreatedLink
    {
        public string? ShortUrl { get; set; }
    }
}
