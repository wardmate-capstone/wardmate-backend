using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Domain;

namespace WardMate.Services.AIOCR.Infrastructure.Knowledge;

// Gemini embedContent / batchEmbedContents. Disabled unless Knowledge:EmbeddingEnabled=true and Gemini:Key is set.
public sealed partial class GeminiEmbeddingProvider(HttpClient http, IConfiguration config, KnowledgeSettings settings, IAiUsage usage) : IEmbeddingProvider
{
    public const int MaxBatch = 50;   // Gemini accepts up to 100 requests per batch; stay well below.
    private const int MaxChars = 6000; // ~1.5k tokens, above the chunk ceiling of 1,500 characters.

    public bool Enabled => settings.EmbeddingEnabled;
    public string Signature => settings.Signature;

    public async Task<float[][]> EmbedDocuments(IReadOnlyList<EmbeddingInput> inputs, CancellationToken ct)
    {
        if (inputs.Count is 0 or > MaxBatch) throw new ArgumentOutOfRangeException(nameof(inputs));
        var model = Model();
        var body = new
        {
            requests = inputs.Select(x => new
            {
                model = $"models/{model}",
                content = new { parts = new[] { new { text = Clip(x.Text) } } },
                taskType = "RETRIEVAL_DOCUMENT",
                title = string.IsNullOrWhiteSpace(x.Title) ? null : Clip(x.Title),
                outputDimensionality = settings.EmbeddingDimensions
            }).ToArray()
        };
        using var doc = await Post(AiOperations.EmbedDocuments, model, inputs.Count, $"{model}:batchEmbedContents", body, ct);
        if (!doc.RootElement.TryGetProperty("embeddings", out var embeddings) || embeddings.GetArrayLength() != inputs.Count) throw Invalid();
        return embeddings.EnumerateArray().Select(Values).ToArray();
    }

    public async Task<float[]> EmbedQuery(string text, CancellationToken ct)
    {
        var model = Model();
        var body = new
        {
            model = $"models/{model}",
            content = new { parts = new[] { new { text = Clip(text) } } },
            taskType = "RETRIEVAL_QUERY",
            outputDimensionality = settings.EmbeddingDimensions
        };
        using var doc = await Post(AiOperations.EmbedQuery, model, 1, $"{model}:embedContent", body, ct);
        if (!doc.RootElement.TryGetProperty("embedding", out var embedding)) throw Invalid();
        return Values(embedding);
    }

    private float[] Values(JsonElement embedding)
    {
        if (!embedding.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array) throw Invalid();
        var vector = values.EnumerateArray().Select(v => v.GetSingle()).ToArray();
        if (vector.Length != settings.EmbeddingDimensions || vector.Any(x => !float.IsFinite(x))) throw Invalid();
        // Reduced-dimension Gemini embeddings are not unit length; normalise so dot product equals cosine.
        return Vectors.Normalize(vector);
    }

    private string Model()
    {
        if (!settings.EmbeddingEnabled) throw new AiFailure(503, "knowledge.embedding_disabled", "Chưa bật Knowledge:EmbeddingEnabled.");
        var model = settings.EmbeddingModel;
        if (!ModelName().IsMatch(model)) throw new AiFailure(503, "knowledge.embedding_not_configured", "Knowledge:EmbeddingModel không hợp lệ.");
        return model;
    }

    private async Task<JsonDocument> Post(string operation, string model, int items, string path, object body, CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var doc = await Send(path, body, ct);
            await usage.Record(new(operation, model, AiOutcomes.Success, null, (int)watch.ElapsedMilliseconds, ItemCount: items), ct);
            return doc;
        }
        catch (AiFailure failure)
        {
            await usage.Record(new(operation, model, AiOutcomes.Failed, failure.Code, (int)watch.ElapsedMilliseconds, ItemCount: items), ct);
            throw;
        }
    }

    private async Task<JsonDocument> Send(string path, object body, CancellationToken ct)
    {
        var key = config["Gemini:Key"];
        if (string.IsNullOrWhiteSpace(key)) throw new AiFailure(503, "knowledge.embedding_not_configured", "Cần cấu hình Gemini:Key để tạo embedding.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{path}");
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(body, options: new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new AiFailure(503, "knowledge.embedding_unavailable", "Không kết nối được dịch vụ embedding. Thử lại sau."); }
        using (response)
        {
            // Provider body is not echoed: it can contain request details.
            if (!response.IsSuccessStatusCode)
                throw new AiFailure(503, "knowledge.embedding_unavailable", $"Dịch vụ embedding trả HTTP {(int)response.StatusCode} (hạn mức, cấu hình hoặc dịch vụ bận).");
            try { return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); }
            catch (JsonException) { throw Invalid(); }
        }
    }

    private static string Clip(string text) => text.Length <= MaxChars ? text : text[..MaxChars];
    private static AiFailure Invalid() => new(502, "knowledge.embedding_invalid", "Dịch vụ embedding trả kết quả không hợp lệ.");
    [GeneratedRegex("^[a-zA-Z0-9._-]{1,100}$")] private static partial Regex ModelName();
}
