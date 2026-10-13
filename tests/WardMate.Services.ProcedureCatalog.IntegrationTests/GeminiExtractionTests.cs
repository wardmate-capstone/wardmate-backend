using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Infrastructure.Extraction;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class GeminiExtractionTests
{
    [Theory]
    [InlineData("stop", true)]
    [InlineData("retry", true)]
    [InlineData("length", false)]
    [InlineData("empty", false)]
    public async Task GeminiUsesDedicatedCredentialsAndRejectsIncompleteResults(string finish, bool succeeds)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Extraction:Provider"] = "Gemini", ["Gemini:Key"] = "test-key",
            ["Gemini:Model"] = "test-model"
        }).Build();
        using var http = new HttpClient(new Handler(finish));
        var extractor = new AzureProcedureDocumentExtractor(http, configuration);
        if (!succeeds)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => extractor.MapText("Văn bản thủ tục", default));
            return;
        }
        var result = await extractor.MapText("Văn bản thủ tục", default);
        Assert.Equal("Văn bản thủ tục", result.ExtractedText);
        Assert.Equal("1.234", result.Payload.GetProperty("procedureCode").GetString());
        Assert.NotEmpty(result.Warnings);
    }

    private sealed class Handler(string finish) : HttpMessageHandler
    {
        private int calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (finish == "retry" && ++calls < 3) return new(HttpStatusCode.ServiceUnavailable);
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization.Parameter);
            Assert.False(request.Headers.Contains("api-key"));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("test-model", body.RootElement.GetProperty("model").GetString());
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    choices = finish == "empty" ? [] : new[]
                    {
                        new { finish_reason = finish == "retry" ? "stop" : finish, message = new { content = "{\"payload\":{\"procedureCode\":\"1.234\"},\"warnings\":[]}" } }
                    }
                })
            };
        }
    }
}
