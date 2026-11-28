using System.Reflection;
using System.Text.RegularExpressions;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;

namespace WardMate.UnitTests.DocumentForm;

// M-03: câu lỗi DocumentForm hiện thẳng lên giao diện nên phải là tiếng Việt; mã lỗi giữ nguyên.
public sealed partial class DocumentFormErrorTextTests
{
    public static IEnumerable<object[]> Errors()
    {
        var id = Guid.NewGuid();
        foreach (var p in typeof(DocumentFormErrors).GetProperties(BindingFlags.Public | BindingFlags.Static))
            yield return [p.Name, (Error)p.GetValue(null)!];
        foreach (var m in typeof(DocumentFormErrors).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => !m.IsSpecialName))
        {
            var args = m.GetParameters().Select(x => x.ParameterType == typeof(Guid) ? id : x.ParameterType == typeof(int) ? 2
                : x.ParameterType == typeof(long) ? 10L * 1024 * 1024 : (object)"x").ToArray();
            yield return [m.Name, (Error)m.Invoke(null, args)!];
        }
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public void Detail_has_no_english_sentence(string name, Error error)
    {
        Assert.StartsWith("document.", error.Code);
        Assert.False(English().IsMatch(error.Description), $"{name}: {error.Description}");
    }

    [Fact]
    public void Known_codes_are_unchanged_and_size_is_readable()
    {
        Assert.Equal("document.file_not_uploaded", DocumentFormErrors.FileNotUploaded.Code);
        Assert.Equal("Biểu mẫu này chưa có tệp Word (DOCX).", DocumentFormErrors.FileNotUploaded.Description);
        Assert.Contains("10 MB", DocumentFormErrors.FileTooLarge(10 * 1024 * 1024).Description);
        Assert.Contains("1.5 MB", DocumentFormErrors.FileTooLarge(1536 * 1024).Description.Replace(',', '.'));
        Assert.DoesNotContain("storage", DocumentFormErrors.BlobUploadFailed("Azure timeout at storage").Description);
    }

    [GeneratedRegex(@"\b(the|was|has|been|not|found|does|cannot|failed|must|file|uploaded|exceeds)\b", RegexOptions.IgnoreCase)]
    private static partial Regex English();
}
