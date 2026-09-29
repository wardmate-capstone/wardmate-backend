using WardMate.Services.DocumentForm.Domain.Entities;
using Xunit;

namespace WardMate.Services.DocumentForm.Tests;

public sealed class FormTemplateEntityTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesActiveTemplate()
    {
        var template = new FormTemplate("  khai_sinh_01  ", "  Đăng ký khai sinh  ", "https://blob/phoi.docx", "admin");

        Assert.Equal("KHAI_SINH_01", template.Code);
        Assert.Equal("Đăng ký khai sinh", template.Title);
        Assert.Equal("https://blob/phoi.docx", template.FileDocxUrl);
        Assert.True(template.IsActive);
        Assert.Equal("admin", template.CreatedBy);
        Assert.NotEqual(Guid.Empty, template.Id);
        Assert.Empty(template.Versions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyCode_ThrowsArgumentException(string invalidCode)
    {
        Assert.Throws<ArgumentException>(() => new FormTemplate(invalidCode, "Title"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyTitle_ThrowsArgumentException(string invalidTitle)
    {
        Assert.Throws<ArgumentException>(() => new FormTemplate("CODE", invalidTitle));
    }

    [Fact]
    public void AddVersion_IncrementsVersionNumberSequentially()
    {
        var template = new FormTemplate("DK_KET_HON", "Đăng ký kết hôn");

        var v1 = template.AddVersion("{\"title\":\"V1\"}", "author1");
        var v2 = template.AddVersion("{\"title\":\"V2\"}", "author2");

        Assert.Equal(1, v1.VersionNumber);
        Assert.Equal(2, v2.VersionNumber);
        Assert.Equal(2, template.Versions.Count);
        Assert.NotNull(template.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateDocxUrl_UpdatesUrlAndTimestamp()
    {
        var template = new FormTemplate("CODE_01", "Title 01");
        var newUrl = "https://azure.blob/templates/phoi_mau.docx";

        template.UpdateDocxUrl(newUrl, "editor");

        Assert.Equal(newUrl, template.FileDocxUrl);
        Assert.Equal("editor", template.UpdatedBy);
        Assert.NotNull(template.UpdatedAtUtc);
    }

    [Fact]
    public void DeactivateAndActivate_TogglesIsActive()
    {
        var template = new FormTemplate("CODE_02", "Title 02");
        Assert.True(template.IsActive);

        template.Deactivate("admin");
        Assert.False(template.IsActive);
        Assert.Equal("admin", template.UpdatedBy);

        template.Activate("admin2");
        Assert.True(template.IsActive);
        Assert.Equal("admin2", template.UpdatedBy);
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedAndTimestamps()
    {
        var template = new FormTemplate("CODE_03", "Title 03");
        var now = DateTime.UtcNow;

        template.SoftDelete(now);

        Assert.True(template.IsDeleted);
        Assert.Equal(now, template.DeletedAtUtc);
        Assert.Equal(now, template.UpdatedAtUtc);
    }
}
