using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Domain;
using WardMate.Services.AIOCR.Infrastructure.Knowledge;

namespace WardMate.Services.AIOCR.Infrastructure;

public sealed partial class ChatService(ChatDbContext db, IAssistantModel model, IConfiguration config,
    IKnowledgeRetriever retriever, KnowledgeSettings knowledge) : IChatService
{
    public const string NoSourceAnswer = "Knowledge Base hiện chưa có thông tin phù hợp để trả lời câu hỏi này. " +
        "Bạn có thể tra cứu danh mục thủ tục trên WardMate hoặc hỏi trực tiếp cán bộ một cửa tại phường.";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private void Configured()
    {
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("AiOcrDatabase")))
            throw new AiFailure(503, "chat.not_configured", "Chưa cấu hình database chat AIOCR.");
    }
    public async Task<ChatResult> Complete(Guid userId, ChatInput input, CancellationToken ct)
    {
        Configured();
        var question = input.Message.Trim();
        // Retrieval (an embedding call) runs before the session lock so the lock only covers the answer itself.
        KnowledgeHit[]? sources = null;
        if (knowledge.RagEnabled)
        {
            if (!config.GetValue<bool>("Assistant:Enabled")) throw new AiFailure(503, "ai.disabled", "Chưa bật chức năng AI công dân.");
            sources = await retriever.Search(question, knowledge.TopK, ct);
        }
        // Serialize turns in one session across replicas. A failed AI turn is rolled back, not a fake answer.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        ChatSession session;
        if (input.ConversationId is Guid id)
        {
            session = await db.Sessions.FromSqlInterpolated($"SELECT * FROM chat_sessions WHERE id={id} AND user_id={userId} FOR UPDATE NOWAIT")
                .SingleOrDefaultAsync(ct) ?? throw new AiFailure(404, "chat.not_found", "Không tìm thấy cuộc trò chuyện của bạn.");
            if (session.WardCode != input.WardCode.Trim()) throw new AiFailure(409, "chat.ward_mismatch", "Phường khác phiên hiện tại; hãy tạo cuộc trò chuyện mới.");
        }
        else
        {
            session = new() { UserId = userId, WardCode = input.WardCode.Trim() };
            db.Sessions.Add(session);
        }
        var recent = await db.Messages.AsNoTracking().Where(x => x.SessionId == session.Id)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(10).ToListAsync(ct);
        recent.Reverse();
        var history = recent.Select(x => new ChatTurn(x.Role, x.Content)).ToList();
        history.Add(new("User", question));
        var userMessage = new ChatMessage { SessionId = session.Id, Role = "User", Content = question };
        db.Messages.Add(userMessage);
        await db.SaveChangesAsync(ct);
        string answer; ChatCitation[]? citations = null;
        if (sources is null) answer = await model.Answer(history, session.WardCode, ct);
        else if (sources.Length == 0) answer = NoSourceAnswer; // grounded mode: no matching source means no model call
        else
        {
            answer = await model.AnswerWithSources(history, session.WardCode, sources, ct);
            citations = Cite(answer, sources);
        }
        db.Messages.Add(new() { SessionId = session.Id, Role = "Assistant", Content = answer,
            Citations = citations is null ? null : JsonSerializer.Serialize(citations, Json) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var related = (citations ?? []).Where(c => c.ProcedureId is not null).DistinctBy(c => c.ProcedureId)
            .Select(c => (object)new RelatedProcedure(c.ProcedureId!.Value, c.Title)).ToArray();
        return new(session.Id, answer, related, citations, citations is not null);
    }

    // Keeps the sources the answer actually cites; if the model cited none, all retrieved sources are returned as context.
    public static ChatCitation[] Cite(string answer, IReadOnlyList<KnowledgeHit> sources)
    {
        var cited = CitationMarker().Matches(answer).Select(m => int.Parse(m.Groups[1].Value))
            .Where(i => i >= 1 && i <= sources.Count).Distinct().Order().ToArray();
        var indexes = cited.Length > 0 ? cited : Enumerable.Range(1, sources.Count).ToArray();
        return indexes.Select(i =>
        {
            var s = sources[i - 1];
            return new ChatCitation(i, s.DocumentId, s.DocumentTitle, s.SourceType, s.ProcedureId, s.ChunkId, s.Heading, s.Score);
        }).ToArray();
    }

    public async Task<Page<SessionDto>> Sessions(Guid userId, int page, int size, CancellationToken ct)
    {
        Configured();
        var q = db.Sessions.AsNoTracking().Where(x => x.UserId == userId);
        var total = await q.CountAsync(ct);
        return new(await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new SessionDto(x.Id, x.WardCode, x.CreatedAt)).ToListAsync(ct), page, size, total);
    }
    public async Task<Page<MessageDto>> Messages(Guid userId, Guid sessionId, int page, int size, CancellationToken ct)
    {
        Configured();
        if (!await db.Sessions.AnyAsync(x => x.Id == sessionId && x.UserId == userId, ct))
            throw new AiFailure(404, "chat.not_found", "Không tìm thấy cuộc trò chuyện của bạn.");
        var q = db.Messages.AsNoTracking().Where(x => x.SessionId == sessionId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Role, x.Content, x.CreatedAt, x.Citations }).ToListAsync(ct);
        return new(rows.Select(x => new MessageDto(x.Id, x.Role, x.Content, x.CreatedAt,
            x.Citations is null ? null : JsonSerializer.Deserialize<ChatCitation[]>(x.Citations, Json))).ToList(), page, size, total);
    }

    [GeneratedRegex(@"\[(\d{1,2})\]")] private static partial Regex CitationMarker();
}
