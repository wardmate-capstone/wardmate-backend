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
            // CHAT-QUALITY-01: câu tiếp nối ngắn ("còn phí thì sao", "trường hợp 2") được tìm cùng câu hỏi trước của người dân.
            string? previous = null;
            if (input.ConversationId is Guid sid)
                previous = await db.Messages.AsNoTracking()
                    .Where(x => x.SessionId == sid && x.Role == "User" && db.Sessions.Any(s => s.Id == sid && s.UserId == userId))
                    .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => x.Content).FirstOrDefaultAsync(ct);
            sources = await retriever.Search(RetrievalQuery(previous, question), knowledge.TopK, ct);
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
        AssistantReply reply; ChatCitation[]? citations = null;
        if (sources is null) reply = await model.Answer(history, session.WardCode, ct);
        else if (sources.Length == 0) reply = new(NoSourceAnswer, false, []); // grounded mode: no matching source means no model call
        else
        {
            reply = await model.AnswerWithSources(history, session.WardCode, sources, ct);
            citations = Cite(reply.Answer, sources);
        }
        var assistant = new ChatMessage
        {
            SessionId = session.Id, Role = "Assistant", Content = reply.Answer, NeedsClarification = reply.NeedsClarification,
            Citations = citations is null ? null : JsonSerializer.Serialize(citations, Json),
            SuggestedQuestions = reply.SuggestedQuestions.Length == 0 ? null : JsonSerializer.Serialize(reply.SuggestedQuestions, Json)
        };
        db.Messages.Add(assistant);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var related = (citations ?? []).Where(c => c.ProcedureId is not null).DistinctBy(c => c.ProcedureId)
            .Select(c => (object)new RelatedProcedure(c.ProcedureId!.Value, c.Title)).ToArray();
        return new(session.Id, reply.Answer, related, citations, citations is not null, assistant.Id, reply.NeedsClarification, reply.SuggestedQuestions);
    }

    /// <summary>
    /// CHAT-QUALITY-01: câu hỏi ngắn (dưới 12 từ) thường là câu tiếp nối hoặc câu trả lời cho câu hỏi làm rõ; ghép với câu hỏi
    /// trước (tối đa 300 ký tự) để tìm đúng thủ tục. Câu dài đủ ngữ cảnh thì tìm riêng để không lẫn chủ đề cũ.
    /// </summary>
    public static string RetrievalQuery(string? previous, string question)
    {
        if (string.IsNullOrWhiteSpace(previous)) return question;
        var words = question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (words >= 12) return question;
        var prior = previous.Trim();
        if (prior.Length > 300) prior = prior[..300];
        return prior + "\n" + question;
    }

    // CHAT-CITE-01: keeps only the sources the answer actually cites. No marker means the model did not answer from the
    // sources (typically "chưa đủ thông tin" for an off-topic question), so nothing is cited and the turn is not grounded.
    public static ChatCitation[]? Cite(string answer, IReadOnlyList<KnowledgeHit> sources)
    {
        var cited = CitationMarker().Matches(answer).Select(m => int.Parse(m.Groups[1].Value))
            .Where(i => i >= 1 && i <= sources.Count).Distinct().Order().ToArray();
        if (cited.Length == 0) return null;
        return cited.Select(i =>
        {
            var s = sources[i - 1];
            return new ChatCitation(i, s.DocumentId, s.DocumentTitle, s.SourceType, s.ProcedureId, s.ChunkId, s.Heading, s.Score);
        }).ToArray();
    }

    /// <summary>CHAT-FEEDBACK-01: người dân đánh giá một câu trả lời trong cuộc trò chuyện của mình; đánh giá lại thì ghi đè.</summary>
    public async Task Feedback(Guid userId, Guid messageId, ChatFeedbackInput input, CancellationToken ct)
    {
        Configured();
        var (reason, comment) = ValidateFeedback(input);
        var message = await db.Messages.SingleOrDefaultAsync(x => x.Id == messageId && x.Role == "Assistant"
            && db.Sessions.Any(s => s.Id == x.SessionId && s.UserId == userId), ct)
            ?? throw new AiFailure(404, "chat.message_not_found", "Không tìm thấy câu trả lời trong cuộc trò chuyện của bạn.");
        message.FeedbackHelpful = input.Helpful; message.FeedbackReason = reason; message.FeedbackComment = comment;
        message.FeedbackAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public static (string? Reason, string? Comment) ValidateFeedback(ChatFeedbackInput input)
    {
        var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim().ToUpperInvariant();
        var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        if (reason is not null && !ChatFeedbackReasons.All.Contains(reason))
            throw new AiFailure(400, "chat.invalid_feedback", "reason phải là một trong: " + string.Join(", ", ChatFeedbackReasons.All) + ".");
        if (comment is { Length: > 1000 } || comment?.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')) == true)
            throw new AiFailure(400, "chat.invalid_feedback", "comment tối đa 1000 ký tự, không chứa ký tự điều khiển.");
        // Đánh giá "hữu ích" không cần lý do; góp ý (comment) vẫn được giữ.
        return (input.Helpful ? null : reason, comment);
    }

    /// <summary>Danh sách đánh giá cho người quản lý tri thức: câu hỏi, câu trả lời và lý do, mới nhất trước.</summary>
    public async Task<Page<ChatFeedbackItem>> FeedbackList(bool? helpful, int page, int size, CancellationToken ct)
    {
        Configured();
        if (page is < 1 or > 100000 || size is < 1 or > 100) throw new AiFailure(400, "chat.invalid_input", "pageNumber 1–100000, pageSize 1–100.");
        var q = db.Messages.AsNoTracking().Where(x => x.Role == "Assistant" && x.FeedbackHelpful != null);
        if (helpful is { } h) q = q.Where(x => x.FeedbackHelpful == h);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.FeedbackAt).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new ChatFeedbackItem(x.Id, x.SessionId,
                db.Messages.Where(u => u.SessionId == x.SessionId && u.Role == "User" && u.CreatedAt <= x.CreatedAt)
                    .OrderByDescending(u => u.CreatedAt).Select(u => u.Content).FirstOrDefault(),
                x.Content, x.Citations != null, x.NeedsClarification, x.FeedbackHelpful!.Value, x.FeedbackReason, x.FeedbackComment, x.FeedbackAt!.Value))
            .ToListAsync(ct);
        return new(items, page, size, total);
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
            .Select(x => new { x.Id, x.Role, x.Content, x.CreatedAt, x.Citations, x.NeedsClarification, x.SuggestedQuestions, x.FeedbackHelpful }).ToListAsync(ct);
        return new(rows.Select(x => new MessageDto(x.Id, x.Role, x.Content, x.CreatedAt,
            x.Citations is null ? null : JsonSerializer.Deserialize<ChatCitation[]>(x.Citations, Json), x.NeedsClarification,
            x.SuggestedQuestions is null ? null : JsonSerializer.Deserialize<string[]>(x.SuggestedQuestions, Json), x.FeedbackHelpful)).ToList(), page, size, total);
    }

    [GeneratedRegex(@"\[(\d{1,2})\]")] private static partial Regex CitationMarker();
}
