using WardMate.Services.DocumentForm.Domain.Models;
namespace WardMate.Services.DocumentForm.Application.Interfaces;

// Mapping is stored separately. The uploaded source is never rewritten.
public interface IDocxFormEngine
{
    IReadOnlyList<DocxParagraph> Inspect(byte[] original);
    byte[] Fill(byte[] original, IReadOnlyList<DocxFieldMapping> mappings, IReadOnlyDictionary<string, string> values);
}
public sealed record DocxParagraph(int Index, string Text);
