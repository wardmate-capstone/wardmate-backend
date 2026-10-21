using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Domain;

namespace WardMate.Services.AIOCR.Infrastructure;

public sealed class ChatService(ChatDbContext db, IAssistantModel model, IConfiguration config) : IChatService
{
    private void Configured()
    {
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("AiOcrDatabase")))
            throw new AiFailure(503, "chat.not_configured", "Chưa cấu hình database chat AIOCR.");
    }
    public async Task<ChatResult> Complete(Guid userId, ChatInput input, CancellationToken ct)
    {
        Configured();
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
        history.Add(new("User", input.Message.Trim()));
        var userMessage = new ChatMessage { SessionId = session.Id, Role = "User", Content = input.Message.Trim() };
        db.Messages.Add(userMessage);
        await db.SaveChangesAsync(ct);
        var answer = await model.Answer(history, session.WardCode, ct);
        db.Messages.Add(new() { SessionId = session.Id, Role = "Assistant", Content = answer });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(session.Id, answer, []);
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
        return new(await q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new MessageDto(x.Id, x.Role, x.Content, x.CreatedAt)).ToListAsync(ct), page, size, total);
    }
}
