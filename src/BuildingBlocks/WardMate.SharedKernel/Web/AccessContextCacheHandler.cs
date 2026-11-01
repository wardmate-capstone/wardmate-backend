using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WardMate.SharedKernel.Web;

// Short-lived cache for successful IAM access-context answers (owner decision 30/10/2026).
// Only 200 responses are cached, keyed by a hash of URL + Authorization (the raw token is never stored).
// A revoked session or changed permission takes effect after at most Iam:AccessContextCacheSeconds.
public sealed class AccessContextCacheHandler(IConfiguration config) : DelegatingHandler
{
    public const int DefaultSeconds = 30;
    public const int MaxSeconds = 60;
    private const int MaxBodyBytes = 64 * 1024;
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions { SizeLimit = 10_000 });

    public static int Seconds(IConfiguration config)
        => Math.Clamp(config.GetValue("Iam:AccessContextCacheSeconds", DefaultSeconds), 0, MaxSeconds);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var seconds = Seconds(config);
        var authorization = request.Headers.Authorization?.ToString();
        if (seconds == 0 || request.Method != HttpMethod.Get || string.IsNullOrEmpty(authorization)
            || request.RequestUri?.AbsolutePath.EndsWith("/api/v1/users/access-context", StringComparison.OrdinalIgnoreCase) != true)
            return await base.SendAsync(request, ct);

        var key = Key(request.RequestUri.GetLeftPart(UriPartial.Path), authorization);
        if (Cache.TryGetValue(key, out Entry? hit) && hit is not null) return Respond(hit, request);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > MaxBodyBytes) return response;
        var body = await response.Content.ReadAsByteArrayAsync(ct);
        if (body.Length > MaxBodyBytes) return Respond(new Entry(body, response.Content.Headers.ContentType), request, response);
        var entry = new Entry(body, response.Content.Headers.ContentType);
        Cache.Set(key, entry, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds) });
        return Respond(entry, request, response);
    }

    private static string Key(string url, string authorization)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url + "\n" + authorization)));

    private static HttpResponseMessage Respond(Entry entry, HttpRequestMessage request, HttpResponseMessage? original = null)
    {
        original?.Dispose();
        var content = new ByteArrayContent(entry.Body);
        if (entry.ContentType is not null) content.Headers.ContentType = entry.ContentType;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request };
    }

    private sealed record Entry(byte[] Body, MediaTypeHeaderValue? ContentType);
}

public static class AccessContextCacheRegistration
{
    // Adds the cache to every HttpClient from IHttpClientFactory; it only acts on GET .../api/v1/users/access-context.
    public static IServiceCollection AddAccessContextCache(this IServiceCollection services)
        => services.ConfigureHttpClientDefaults(b => b.AddHttpMessageHandler(sp => new AccessContextCacheHandler(sp.GetRequiredService<IConfiguration>())));
}
