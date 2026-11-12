using System.Text.Json;
using WardMate.Services.AIOCR.Infrastructure.Extraction;
using WardMate.Services.AIOCR.Infrastructure.Knowledge;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.UnitTests.Aiocr;

// KB-TITLE-01: tên thủ tục bóc từ ô bảng PDF mang \n; đồng bộ kho kiến thức, Catalog và bóc tách AI đều gộp về một dòng.
public sealed class ProcedureTitleOneLineTests
{
    private const string Raw = "  (Đà Nẵng) Đăng ký, cấp Giấy chứng nhận\nđối với thửa đất\r\ncó diện tích\t tăng thêm ";
    private const string Expected = "(Đà Nẵng) Đăng ký, cấp Giấy chứng nhận đối với thửa đất có diện tích tăng thêm";

    [Fact]
    public void Knowledge_sync_title_has_no_control_characters()
    {
        var title = KnowledgeBaseService.OneLine(Raw);
        Assert.Equal(Expected, title);
        Assert.DoesNotContain(title!, char.IsControl);
    }

    [Fact]
    public void Knowledge_one_line_is_idempotent_so_resync_stays_unchanged()
    {
        var once = KnowledgeBaseService.OneLine(Raw);
        Assert.Equal(once, KnowledgeBaseService.OneLine(once));
        Assert.Null(KnowledgeBaseService.OneLine(null));
    }

    [Fact]
    public void Catalog_save_stores_title_on_one_line()
    {
        var procedure = new Procedure();
        new ProcedureInput { CategoryId = 1, ProcedureCode = "P1", Title = Raw }.ApplyTo(procedure);
        Assert.Equal(Expected, procedure.Title);
    }

    [Fact]
    public void Ai_extraction_returns_title_on_one_line()
    {
        var payload = JsonSerializer.SerializeToElement(new { title = Raw });
        var result = ProcedureAiContract.Normalize(payload, new List<string>());
        Assert.Equal(Expected, result.GetProperty("title").GetString());
    }
}
