using System.Net;
using System.Text;
using WardMate.Services.ApplicationWorkflow.Domain;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class WorkflowUnitTests
{
    [Theory]
    [InlineData("PENDING")]
    [InlineData("REJECTED")]
    public void DomainCannotSubmitWithIncompleteRequiredItem(string status)
    {
        var a = new ApplicationRecord();
        a.Checklists.Add(new() { IsRequired = true, Status = status });
        Assert.Throws<InvalidOperationException>(() => a.Submit("HS-TEST", Guid.NewGuid(), DateTime.UtcNow));
        Assert.Equal("DRAFT", a.Status);
        Assert.Empty(a.History);
    }

    [Fact]
    public void DomainCannotTransitionBackOrSubmitTwice()
    {
        var a = new ApplicationRecord(); var actor = Guid.NewGuid(); var now = DateTime.UtcNow;
        a.Submit("HS-TEST", actor, now);
        Assert.Throws<InvalidOperationException>(() => a.Submit("HS-SECOND", actor, now));
        Assert.Equal("HS-TEST", a.ApplicationCode);
        Assert.Equal(actor, Assert.Single(a.History).ChangedBy);
    }

    [Theory]
    [InlineData(404, "{}", 404)]
    [InlineData(500, "{}", 503)]
    [InlineData(200, "not json", 502)]
    [InlineData(200, "null", 502)]
    public async Task CatalogAdapterDoesNotTreatFailureAsAnEmptyChecklist(int httpStatus, string body, int expected)
    {
        using var http = new HttpClient(new StubHandler((HttpStatusCode)httpStatus, body)) { BaseAddress = new("http://catalog/") };
        var result = await new ProcedureCatalogClient(http).Get(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(expected, result.Error!.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task CatalogAdapterReadsActualCamelCaseContract()
    {
        var id = Guid.NewGuid();
        var body = $$"""
            {"id":"{{id}}","title":"Thủ tục hợp lệ","isActive":true,
             "contentPayload":{"cases":[{"caseCode":"A","caseName":"Một trường hợp"}]},
             "checklistSchema":[{"checklistId":"ID_COPY","itemName":"Giấy tờ","isMandatory":true,"caseCode":"A"}]}
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, body)) { BaseAddress = new("http://catalog/") };
        var result = await new ProcedureCatalogClient(http).Get(id, CancellationToken.None);
        Assert.Null(result.Error);
        Assert.True(Assert.Single(result.Value!.ChecklistSchema!).IsMandatory);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
