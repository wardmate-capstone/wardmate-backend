using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Tests;

/// <summary>
/// Tests cho TASK-10: Thực thể Domain (FormTemplate, FormTemplateVersion, ApplicationForm, SupportingDocument)
/// và logic nghiệp vụ liên quan đến Azure Storage (URL lưu trữ DOCX).
/// </summary>
public sealed class FormTemplateEntityTests
{
    // ──────────────────────────────────────────────────────────────
    // FormTemplate – khởi tạo hợp lệ
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidParams_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var template = new FormTemplate("mau-01", "Mẫu đơn xin việc", "https://blob.azure.com/mau-01.docx", "admin");

        // Assert
        Assert.Equal("MAU-01", template.Code);        // code phải uppercase
        Assert.Equal("Mẫu đơn xin việc", template.Title);
        Assert.Equal("https://blob.azure.com/mau-01.docx", template.FileDocxUrl);
        Assert.True(template.IsActive);               // mặc định Active
        Assert.Equal("admin", template.CreatedBy);
        Assert.NotEqual(Guid.Empty, template.Id);
    }

    [Fact]
    public void Constructor_NullCode_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FormTemplate(null!, "Title"));
    }

    [Fact]
    public void Constructor_EmptyTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new FormTemplate("M01", "   "));
    }

    [Fact]
    public void Constructor_NoDocxUrl_FileDocxUrlIsNull()
    {
        var template = new FormTemplate("M02", "Mẫu không có file");
        Assert.Null(template.FileDocxUrl);
    }

    // ──────────────────────────────────────────────────────────────
    // FormTemplate – cập nhật URL DOCX (Azure Blob Storage upload)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateDocxUrl_ValidUrl_UpdatesFileDocxUrl()
    {
        var template = new FormTemplate("M03", "Mẫu A");
        template.UpdateDocxUrl("https://blob.azure.com/v2/mau-a.docx", "uploader");

        Assert.Equal("https://blob.azure.com/v2/mau-a.docx", template.FileDocxUrl);
    }

    [Fact]
    public void UpdateDocxUrl_EmptyUrl_ThrowsArgumentException()
    {
        var template = new FormTemplate("M03", "Mẫu A");
        Assert.Throws<ArgumentException>(() => template.UpdateDocxUrl("  "));
    }

    // ──────────────────────────────────────────────────────────────
    // FormTemplate – kích hoạt / vô hiệu hóa
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ThenActivate_TogglesIsActive()
    {
        var template = new FormTemplate("M04", "Mẫu B");

        template.Deactivate("admin");
        Assert.False(template.IsActive);

        template.Activate("admin");
        Assert.True(template.IsActive);
    }

    // ──────────────────────────────────────────────────────────────
    // FormTemplate – thêm version
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddVersion_FirstVersion_VersionNumberIsOne()
    {
        var template = new FormTemplate("M05", "Mẫu C");
        var version = template.AddVersion("{\"title\":\"v1\"}", "designer");

        Assert.Equal(1, version.VersionNumber);
        Assert.Single(template.Versions);
    }

    [Fact]
    public void AddVersion_SecondVersion_VersionNumberIncrements()
    {
        var template = new FormTemplate("M06", "Mẫu D");
        template.AddVersion("{\"title\":\"v1\"}");
        var v2 = template.AddVersion("{\"title\":\"v2\"}");

        Assert.Equal(2, v2.VersionNumber);
        Assert.Equal(2, template.Versions.Count);
    }

    [Fact]
    public void AddVersion_EmptySchema_ThrowsArgumentException()
    {
        var template = new FormTemplate("M07", "Mẫu E");
        Assert.Throws<ArgumentException>(() => template.AddVersion("   "));
    }

    // ──────────────────────────────────────────────────────────────
    // FormTemplateVersion – guard clauses
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void FormTemplateVersion_EmptyTemplateId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new FormTemplateVersion(Guid.Empty, 1, "{\"title\":\"v1\"}"));
    }

    [Fact]
    public void FormTemplateVersion_NegativeVersionNumber_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FormTemplateVersion(Guid.NewGuid(), -1, "{\"title\":\"v1\"}"));
    }

    // ──────────────────────────────────────────────────────────────
    // SupportingDocument – tài liệu đính kèm (Azure Blob URL)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void SupportingDocument_ValidParams_SetsPropertiesCorrectly()
    {
        var appId = Guid.NewGuid();
        var doc = new SupportingDocument(
            appId,
            "cmnd_truoc.jpg",
            "https://blob.azure.com/uploads/cmnd_truoc.jpg",
            fileSizeBytes: 512000,
            contentType: "image/jpeg",
            createdBy: "user1");

        Assert.Equal(appId, doc.ApplicationId);
        Assert.Equal("cmnd_truoc.jpg", doc.FileName);
        Assert.Equal("https://blob.azure.com/uploads/cmnd_truoc.jpg", doc.BlobUrl);
        Assert.Equal(512000, doc.FileSizeBytes);
        Assert.Equal("image/jpeg", doc.ContentType);
    }

    [Fact]
    public void SupportingDocument_EmptyApplicationId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportingDocument(Guid.Empty, "file.pdf", "https://blob.azure.com/file.pdf"));
    }

    [Fact]
    public void SupportingDocument_EmptyBlobUrl_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupportingDocument(Guid.NewGuid(), "file.pdf", "   "));
    }

    // ──────────────────────────────────────────────────────────────
    // ApplicationForm – dữ liệu biểu mẫu đã điền
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void ApplicationForm_ValidParams_SetsFormData()
    {
        var appId = Guid.NewGuid();
        var form = new ApplicationForm(appId, "{\"ho_ten\":\"Nguyen Van A\"}", "citizen");

        Assert.Equal(appId, form.ApplicationId);
        Assert.Equal("{\"ho_ten\":\"Nguyen Van A\"}", form.FormData);
    }

    [Fact]
    public void ApplicationForm_UpdateFormData_UpdatesSuccessfully()
    {
        var form = new ApplicationForm(Guid.NewGuid(), "{\"ho_ten\":\"A\"}", "citizen");
        form.UpdateFormData("{\"ho_ten\":\"Nguyen Van B\"}", "admin");

        Assert.Equal("{\"ho_ten\":\"Nguyen Van B\"}", form.FormData);
    }

    [Fact]
    public void ApplicationForm_EmptyApplicationId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new ApplicationForm(Guid.Empty, "{\"ho_ten\":\"A\"}"));
    }
}
