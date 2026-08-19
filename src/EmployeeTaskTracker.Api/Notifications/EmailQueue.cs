using System.Threading.Channels;

namespace EmployeeTaskTracker.Api.Notifications;

public interface IEmailQueue
{
    /// <summary>
    /// Hands a message to the background sender. Returns immediately and never
    /// throws, so a notification cannot fail the request that produced it.
    /// </summary>
    void Enqueue(EmailMessage message);

    IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// An in-memory queue between the controllers and the SMTP sender.
///
/// Talking to an SMTP server takes seconds, and the specification requires API
/// responses inside two seconds, so requests must not wait for delivery. The
/// controller drops the message here and returns; <see cref="EmailDispatcher"/>
/// does the slow part.
///
/// The queue is bounded. If notifications were ever produced faster than they
/// could be sent, an unbounded queue would grow until the process ran out of
/// memory; this drops the oldest message instead and logs that it did.
/// </summary>
public sealed class EmailQueue : IEmailQueue
{
    private const int Capacity = 500;

    private readonly Channel<EmailMessage> _channel;
    private readonly ILogger<EmailQueue> _logger;

    public EmailQueue(ILogger<EmailQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Enqueue(EmailMessage message)
    {
        if (!_channel.Writer.TryWrite(message))
        {
            _logger.LogWarning(
                "Email queue is full; dropped a {Kind} notification for {Recipient}.",
                message.Kind, message.ToAddress);
        }
    }

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
