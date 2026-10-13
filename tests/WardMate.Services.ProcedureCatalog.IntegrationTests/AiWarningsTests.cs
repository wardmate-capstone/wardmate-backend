using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Infrastructure.Extraction;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class AiWarningsTests
{
    [Theory]
    [InlineData("[]", "")]
    [InlineData("[\"Kiểm tra\"]", "Kiểm tra")]
    [InlineData("[{\"field\":\"title\",\"reason\":\"Kiểm tra\"}]", "title: Kiểm tra")]
    [InlineData("[\"Kiểm tra\",{\"field\":\"title\",\"reason\":\"Thiếu\"}]", "title: Thiếu")]
    [InlineData("null", "không đúng định dạng")]
    [InlineData("42", "không đúng định dạng")]
    [InlineData("{}", "không đúng định dạng")]
    [InlineData("[null,42,{},\"\"]", "không đúng định dạng")]
    [InlineData("[{\"field\":42,\"reason\":\"Thiếu\"}]", "không đúng định dạng")]
    [InlineData(null, "không đúng định dạng")]
    public async Task WarningVariantsPreservePayload(string? warnings, string expected)
    {
        var json = "{\"payload\":{\"procedureCode\":\"1.116905\",\"title\":\"Tên thủ tục\"}" +
            (warnings is null ? "" : ",\"warnings\":" + warnings) + "}";
        using var http = new HttpClient(new ResponseHandler(json));
        var result = await Extractor(http).MapText("Văn bản gốc", default);
        Assert.Equal("1.116905", result.Payload.GetProperty("procedureCode").GetString());
        Assert.Equal("Văn bản gốc", result.ExtractedText);
        Assert.Contains(result.Warnings, x => x.Contains(expected));
    }

    [Theory]
    [InlineData("{\"warnings\":[]}")]
    [InlineData("{\"payload\":null,\"warnings\":[]}")]
    [InlineData("{\"payload\":[],\"warnings\":[]}")]
    [InlineData("[]")]
    public async Task InvalidPayloadIsStillRejected(string json)
    {
        using var http = new HttpClient(new ResponseHandler(json));
        await Assert.ThrowsAsync<InvalidDataException>(() => Extractor(http).MapText("Văn bản", default));
    }

    [Fact]
    public async Task BrokenJsonIsNotRepaired()
    {
        using var http = new HttpClient(new ResponseHandler("{\"payload\":"));
        await Assert.ThrowsAnyAsync<JsonException>(() => Extractor(http).MapText("Văn bản", default));
    }

    private static AzureProcedureDocumentExtractor Extractor(HttpClient http) => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Extraction:Provider"] = "Gemini", ["Gemini:Key"] = "test-key", ["Gemini:Model"] = "test-model"
        }).Build());

    private sealed class ResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { choices = new[] { new { finish_reason = "stop", message = new { content = json } } } })
            });
    }
}
