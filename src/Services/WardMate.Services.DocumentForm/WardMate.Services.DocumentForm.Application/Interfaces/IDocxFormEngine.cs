using WardMate.Services.DocumentForm.Application.Interfaces;
namespace WardMate.Services.DocumentForm.Application.Interfaces;

public interface IDocxFormEngine
{
    byte[] Fill(byte[] original, string schemaDefinitionJson, IReadOnlyDictionary<string, string> values);
}
