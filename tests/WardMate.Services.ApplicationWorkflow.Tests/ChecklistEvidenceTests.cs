using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WardMate.Services.ApplicationWorkflow.API;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class ChecklistEvidenceTests
{
    [Theory]
    [InlineData(false, "CERTIFIED_COPY", 2, false)]
    [InlineData(true, "REGULAR_COPY", 2, false)]
    [InlineData(true, "CERTIFIED_COPY", 1, false)]
    [InlineData(true, "CERTIFIED_COPY", 2, true)]
    public async Task EvidenceRequiresExistingBlobMatchingCopyAndQuantity(bool exists, string copy, int quantity, bool valid)
    {
        var id = Guid.NewGuid(); var itemId = Guid.NewGuid();
        var client = Client(new { applicationId = id, submissions = Array.Empty<object>(), supportingDocuments = new[] {
            new { id = Guid.NewGuid(), checklistId = itemId, status = "Uploaded", fileExists = exists,
                submissionType = "NOP", documentCopyType = copy, quantity } } });
        var item = new ChecklistDto(itemId, "ID", "Giấy tờ", true, "COMPLETED", "https://untrusted.test/file", null,
            DateTime.UtcNow, DateTime.UtcNow, SubmissionType: "NOP", DocumentCopyType: "CERTIFIED_COPY", Quantity: 2);
        var result = await client.Validate(id, [item], default);
        if (valid) Assert.Null(result); else Assert.Equal(422, result!.StatusCode);
    }

    [Fact]
    public async Task EvidenceCannotComeFromOtherChecklistOrUnfinishedForm()
    {
        var id = Guid.NewGuid(); var itemId = Guid.NewGuid(); var template = Guid.NewGuid();
        var client = Client(new { applicationId = id, submissions = new[] {
            new { id = Guid.NewGuid(), checklistId = itemId, status = "Draft", templateId = template, fileExists = true } },
            supportingDocuments = new[] { new { id = Guid.NewGuid(), checklistId = Guid.NewGuid(), status = "Uploaded", fileExists = true } } });
        var item = new ChecklistDto(itemId, "FORM", "Tờ khai", true, "COMPLETED", null, null,
            DateTime.UtcNow, DateTime.UtcNow, template);
        Assert.Equal(422, (await client.Validate(id, [item], default))!.StatusCode);
        Assert.Equal(422, (await client.Validate(id, [item with { FormTemplateId = null }], default))!.StatusCode);
    }

    [Fact]
    public void SubmissionSnapshotPreservesRequirementsAndOrder()
    {
        var a = new ApplicationRecord();
        a.Checklists.Add(new() { Code = "Z", Title = "Bản gốc", SortOrder = 0, Quantity = 2,
            CaseCode = "A", SubmissionType = "XUAT_TRINH", DocumentCopyType = "ORIGINAL", ConditionNote = "Đối chiếu tại quầy" });
        a.Checklists.Add(new() { Code = "A", Title = "Bản sao", SortOrder = 1 });
        var snapshot = ApplicationSnapshots.Capture(a, Guid.NewGuid(), DateTime.UtcNow);
        a.Checklists[0].Quantity = 9;
        using var json = System.Text.Json.JsonDocument.Parse(snapshot.SnapshotData);
        var first = json.RootElement.GetProperty("checklists")[0];
        Assert.Equal(2, first.GetProperty("quantity").GetInt32());
        Assert.Equal("XUAT_TRINH", first.GetProperty("submissionType").GetString());
        Assert.Equal("ORIGINAL", first.GetProperty("documentCopyType").GetString());
        Assert.Equal("A", first.GetProperty("caseCode").GetString());
        Assert.Equal("Đối chiếu tại quầy", first.GetProperty("conditionNote").GetString());
        Assert.Equal("Z", ApplicationDto.From(a).Checklists[0].Code);
    }

    private static DocumentContentsClient Client(object response) => new(new HttpClient(new Reply(response)),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Services:DocumentForm:BaseUrl"] = "http://document.test" }).Build(),
        new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
    private sealed class Reply(object response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) });
    }
}
