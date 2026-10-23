using System.Text.Json;
using RabbitMQ.Client;

namespace WardMate.EventBus;

public sealed class RabbitTransport(RabbitOptions options)
{
    public const string Exchange = "wardmate.workflow.v1";
    public const string Queue = "wardmate.notifications.workflow.v1";
    public const string DeadQueue = Queue + ".dead";

    public async Task<IConnection> Connect(CancellationToken ct) => await new ConnectionFactory
    {
        HostName = options.Host, Port = options.Port, UserName = options.User, Password = options.Password,
        VirtualHost = options.VirtualHost, AutomaticRecoveryEnabled = false,
        RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
        Ssl = new SslOption { Enabled = options.UseTls, ServerName = options.Host }
    }.CreateConnectionAsync(ct);

    public static async Task<IChannel> Open(IConnection connection, CancellationToken ct)
    {
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), ct);
        try
        {
            await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
            await channel.QueueDeclareAsync(DeadQueue, true, false, false, cancellationToken: ct);
            await channel.QueueDeclareAsync(Queue, true, false, false,
                new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "", ["x-dead-letter-routing-key"] = DeadQueue }, cancellationToken: ct);
            await channel.QueueBindAsync(Queue, Exchange, "workflow.changed", cancellationToken: ct);
            return channel;
        }
        catch { await channel.DisposeAsync(); throw; }
    }

    public static async Task Publish(IChannel channel, WorkflowEvent message, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await channel.BasicPublishAsync(Exchange, "workflow.changed", true,
            new BasicProperties { Persistent = true, ContentType = "application/json", MessageId = message.EventId.ToString(), Type = $"workflow.v{message.Version}" },
            JsonSerializer.SerializeToUtf8Bytes(message), deadline.Token);
    }
}
