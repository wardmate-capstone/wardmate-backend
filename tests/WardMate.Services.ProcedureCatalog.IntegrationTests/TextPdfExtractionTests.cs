using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using WardMate.Services.AIOCR.Infrastructure.Extraction;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class TextPdfExtractionTests
{
    private static IConfiguration Config(bool ai = false) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Extraction:UseAI"] = ai.ToString(), ["AzureOpenAI:Endpoint"] = "https://ai.example.test",
        ["AzureOpenAI:Key"] = "test-only", ["AzureOpenAI:Deployment"] = "test-model"
    }).Build();
    internal static byte[] Pdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            if (text.Length > 0) page.AddText(text, 12, new PdfPoint(30, 700), font);
        }
        return builder.Build();
    }
    private const string Text = "Procedure registration: required papers and processing time from the original document.";

    [Fact]
    public async Task NativePdfNeedsNoOcrOrAiCredentialsOrNetwork()
    {
        using var handler = new AiHandler(); using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().Build();
        var extractor = new TextFirstProcedureExtractor(new AzureProcedureDocumentExtractor(http, config), config);
        using var pdf = new MemoryStream(Pdf(Text, "Second page contains additional steps for this procedure."));
        var result = await extractor.Extract(pdf, default);
        Assert.Contains(Text, result.ExtractedText); Assert.Contains("Trang 2", result.ExtractedText);
        Assert.Contains(result.Warnings, x => x.Contains("Chưa bật AI"));
        Assert.Equal("", result.Payload.GetProperty("feeSummary").GetString());
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyOrMixedPdfWarnsAndDoesNotMapPartialTextWithAi(bool mixed)
    {
        using var handler = new AiHandler(); using var http = new HttpClient(handler); var config = Config(true);
        var extractor = new TextFirstProcedureExtractor(new AzureProcedureDocumentExtractor(http, config), config);
        using var pdf = new MemoryStream(mixed ? Pdf(Text, "") : Pdf(""));
        var result = await extractor.Extract(pdf, default);
        Assert.Contains(result.Warnings, x => x.Contains("không có đủ văn bản"));
        Assert.Equal(0, handler.Calls);
        if (mixed) Assert.Contains(Text, result.ExtractedText);
    }

    [Fact]
    public async Task NativePdfWithAiCallsOnlyMappingEndpoint()
    {
        using var handler = new AiHandler(); using var http = new HttpClient(handler); var config = Config(true);
        var extractor = new TextFirstProcedureExtractor(new AzureProcedureDocumentExtractor(http, config), config);
        using var pdf = new MemoryStream(Pdf(Text));
        var result = await extractor.Extract(pdf, default);
        Assert.Equal(1, handler.Calls); Assert.Equal("Mapped title", result.Payload.GetProperty("title").GetString());
        Assert.Contains(Text, result.ExtractedText);
    }

    [Fact]
    public async Task AiFailurePreservesTextForManualReview()
    {
        using var handler = new AiHandler(fail: true); using var http = new HttpClient(handler); var config = Config(true);
        var extractor = new TextFirstProcedureExtractor(new AzureProcedureDocumentExtractor(http, config), config);
        using var pdf = new MemoryStream(Pdf(Text));
        var result = await extractor.Extract(pdf, default);
        Assert.Contains(Text, result.ExtractedText);
        Assert.Contains(result.Warnings, x => x.Contains("Không thể chuyển văn bản"));
    }

    [Fact]
    public async Task MalformedPdfReturnsControlledFailure()
    {
        using var handler = new AiHandler(); using var http = new HttpClient(handler); var config = Config();
        using var pdf = new MemoryStream("%PDF-1.7 broken"u8.ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => new TextFirstProcedureExtractor(new AzureProcedureDocumentExtractor(http, config), config).Extract(pdf, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task PageLimitIsRejectedWithoutSilentlyTruncating()
    {
        using var handler = new AiHandler(); using var http = new HttpClient(handler); var config = Config();
        using var pdf = new MemoryStream(Pdf(Enumerable.Repeat(Text, 101).ToArray()));
        await Assert.ThrowsAsync<InvalidDataException>(() => new TextFirstProcedureExtractor(new AzureProcedureDocumentExtractor(http, config), config).Extract(pdf, default));
        Assert.Equal(0, handler.Calls);
    }

    private sealed class AiHandler(bool fail = false) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal("/openai/v1/chat/completions", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(fail ? HttpStatusCode.BadGateway : HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { choices = new[] { new { finish_reason = "stop", message = new
                { content = "{\"payload\":{\"title\":\"Mapped title\"},\"warnings\":[]}" } } } })
            });
        }
    }
}
