namespace WardMate.EventBus;

// Versioned integration contract: no tokens, email, phone or form/comment contents on the broker.
public sealed record WorkflowEvent(Guid EventId, int Version, Guid ApplicationId, Guid ApplicantId,
    Guid? AssignedOfficerId, string WardCode, string ApplicationCode, string Kind, string Status, DateTime OccurredAt);

public sealed class RabbitOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string VirtualHost { get; set; } = "/";
    public bool UseTls { get; set; }
}

public static class RetrySchedule
{
    public static DateTime Next(int attempts) => DateTime.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(attempts, 9))));
}
