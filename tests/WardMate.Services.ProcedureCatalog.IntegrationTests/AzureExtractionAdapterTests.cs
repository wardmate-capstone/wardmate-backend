using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Infrastructure.Extraction;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class AzureExtractionAdapterTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Extraction:UseAI"] = "true",
        ["DocumentIntelligence:Endpoint"] = "https://ocr.example.test", ["DocumentIntelligence:Key"] = "test-ocr",
        ["AzureOpenAI:Endpoint"] = "https://ai.example.test", ["AzureOpenAI:Key"] = "test-ai", ["AzureOpenAI:Deployment"] = "test-model"
    }).Build();
    [Theory]
    [InlineData("stop", true)]
    [InlineData("length", false)]
    public async Task OcrThenAiPreservesRawUnknownsAndRejectsTruncatedOutput(string finish, bool succeeds)
    {
        using var handler = new StubHandler(finish);
        using var http = new HttpClient(handler);
        var extractor = new AzureProcedureDocumentExtractor(http, Config());
        using var pdf = new MemoryStream("%PDF-1.7 test"u8.ToArray());
        if (!succeeds) { await Assert.ThrowsAsync<InvalidDataException>(() => extractor.Extract(pdf, default)); return; }
        var result = await extractor.Extract(pdf, default);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.Payload.GetProperty("feeSummary").ValueKind);
        Assert.Equal("Nội dung OCR thử nghiệm", result.ExtractedText);
        Assert.Contains(result.Warnings, x => x.Contains("feeSummary"));
        Assert.Equal(3, handler.Calls);
    }
    [Fact]
    public async Task MissingProviderConfigurationMakesNoNetworkCall()
    {
        using var handler = new StubHandler("stop"); using var http = new HttpClient(handler);
        var extractor = new AzureProcedureDocumentExtractor(http, new ConfigurationBuilder().Build());
        using var pdf = new MemoryStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() => extractor.Extract(pdf, default));
        Assert.Equal(0, handler.Calls);
    }
    private sealed class StubHandler(string finish) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (Calls == 1)
            {
                Assert.Equal("ocr.example.test", request.RequestUri!.Host);
                Assert.True(request.Headers.Contains("Ocp-Apim-Subscription-Key"));
                var result = new HttpResponseMessage(HttpStatusCode.Accepted);
                result.Headers.Add("Operation-Location", "https://ocr.example.test/result/1"); return Task.FromResult(result);
            }
            object body = Calls == 2 ? new { status = "succeeded", analyzeResult = new { content = "Nội dung OCR thử nghiệm" } }
                : new { choices = new[] { new { finish_reason = finish, message = new { content = "{\"payload\":{\"feeSummary\":null},\"warnings\":[\"feeSummary: cần đối soát\"]}" } } } };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
        }
    }
}
