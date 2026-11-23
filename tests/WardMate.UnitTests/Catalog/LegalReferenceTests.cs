using System.Text.Json;
using WardMate.Services.AIOCR.Infrastructure.Extraction;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.UnitTests.Catalog;

// PROC-LEGAL-01: căn cứ pháp lý dạng 2 cột hoặc 4 cột của Cổng DVC; cột nào không ghi thì để null.
public sealed class LegalReferenceTests
{
    private static readonly ProcedureInputValidator Validator = new();

    private static ProcedureInput Input(params LegalReferenceDetail[] legal) => new()
    {
        CategoryId = 1, ProcedureCode = "P1", Title = "Thủ tục thử",
        ContentPayload = new ProcedureContentPayload { LegalReferences = [.. legal] }
    };

    [Fact]
    public void Two_column_and_four_column_rows_are_valid()
    {
        var twoColumns = new LegalReferenceDetail { DocumentName = "Luật Việc làm", DocumentNumber = "74/2025/QH15", Authority = null };
        var fourColumns = new LegalReferenceDetail
        {
            DocumentNumber = "168/2025/NĐ-CP", DocumentName = "Nghị định về đăng ký doanh nghiệp",
            IssueDate = new DateOnly(2025, 6, 30), Authority = "Chính phủ"
        };
        var numberOnly = new LegalReferenceDetail { DocumentNumber = "59/2020/QH14", DocumentName = null, Authority = null };
        Assert.True(Validator.Validate(Input(twoColumns, fourColumns, numberOnly)).IsValid);
    }

    [Fact]
    public void Row_needs_number_or_name()
    {
        var result = Validator.Validate(Input(new LegalReferenceDetail { DocumentNumber = " ", DocumentName = null, Authority = "Bộ Tài chính" }));
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("số ký hiệu hoặc trích yếu"));
        Assert.False(Validator.Validate(Input(new LegalReferenceDetail { DocumentNumber = new string('a', 256) })).IsValid);
    }

    [Fact]
    public void Blank_columns_are_saved_as_null()
    {
        var procedure = new Procedure();
        Input(new LegalReferenceDetail { DocumentNumber = " 85/2019/TT-BTC ", DocumentName = "  ", Authority = "" }).ApplyTo(procedure);
        var row = Assert.Single(procedure.ContentPayload.LegalReferences);
        Assert.Equal(("85/2019/TT-BTC", (string?)null, (string?)null), (row.DocumentNumber, row.DocumentName, row.Authority));
        Assert.Null(row.IssueDate);
    }

    [Theory]
    [InlineData("29-11-2019", "2019-11-29")]
    [InlineData("17/06/2020", "2020-06-17")]
    [InlineData("2025-06-30", "2025-06-30")]
    [InlineData("1-7-2025", "2025-07-01")]
    [InlineData("tháng 6 năm 2025", null)]
    [InlineData("31-02-2025", null)]
    public void Ai_issue_date_is_normalized_to_iso(string raw, string? expected) => Assert.Equal(expected, ProcedureAiContract.LegalDate(raw));

    [Fact]
    public void Ai_payload_converts_dates_and_does_not_warn_for_missing_date_or_authority()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            contentPayload = new
            {
                legalReferences = new object[]
                {
                    new { documentNumber = "85/2019/TT-BTC", documentName = "Hướng dẫn về phí", issueDate = "29-11-2019", authority = "Bộ Tài chính" },
                    new { documentNumber = "59/2020/QH14", documentName = "Luật 59/2020/QH14", issueDate = (string?)null, authority = (string?)null }
                }
            }
        });
        var warnings = new List<string>();
        var result = ProcedureAiContract.Normalize(payload, warnings);
        var legal = result.GetProperty("contentPayload").GetProperty("legalReferences");
        Assert.Equal("2019-11-29", legal[0].GetProperty("issueDate").GetString());
        Assert.Equal(JsonValueKind.Null, legal[1].GetProperty("authority").ValueKind);
        Assert.DoesNotContain(warnings, w => w.Contains("legalReferences[1].issueDate") || w.Contains("legalReferences[1].authority"));
    }
}
