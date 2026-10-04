using System.Text.Json;

namespace WardMate.Services.AIOCR.Application.Extraction;

// Wire contract only. AIOCR does not reference ProcedureCatalog assemblies or databases.
public sealed record ProcedureExtraction(JsonElement Payload, string ExtractedText, string[] Warnings);
public interface IProcedureDocumentExtractor
{
    Task<ProcedureExtraction> Extract(Stream pdf, CancellationToken ct);
}
