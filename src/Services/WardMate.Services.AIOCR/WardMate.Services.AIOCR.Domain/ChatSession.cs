namespace WardMate.Services.AIOCR.Domain;

public sealed class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string WardCode { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Role { get; set; } = "User";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // JSON array of knowledge citations for grounded assistant answers; null for user turns and ungrounded answers.
    public string? Citations { get; set; }
    // CHAT-QUALITY-01: câu trả lời đang hỏi lại người dân để làm rõ; JSON array các câu hỏi gợi ý tiếp theo / lựa chọn trả lời nhanh.
    public bool NeedsClarification { get; set; }
    public string? SuggestedQuestions { get; set; }
    // CHAT-FEEDBACK-01: người dân đánh giá câu trả lời (chỉ tin nhắn Assistant).
    public bool? FeedbackHelpful { get; set; }
    public string? FeedbackReason { get; set; }
    public string? FeedbackComment { get; set; }
    public DateTime? FeedbackAt { get; set; }
}
