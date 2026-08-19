using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmployeeTaskTracker.Api.Notifications;

/// <summary>
/// Drains <see cref="IEmailQueue"/> and delivers each message over SMTP.
///
/// Runs as a hosted service for the lifetime of the application, so the slow
/// part of sending mail happens away from any HTTP request.
///
/// When email is not configured, every message is logged at Information instead
/// of being sent. That is deliberate: somebody cloning this repository to review
/// it has no mailbox set up, and the application must still work for them. The
/// log line shows exactly what would have been sent.
///
/// MailKit is used rather than System.Net.Mail.SmtpClient, which Microsoft
/// documents as not recommended for new development.
/// </summary>
public sealed class EmailDispatcher : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(
        IEmailQueue queue,
        IOptions<EmailOptions> options,
        ILogger<EmailDispatcher> logger)
    {
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.IsConfigured)
        {
            _logger.LogInformation(
                "Email notifications are enabled; sending through {Host}:{Port} as {From}.",
                _options.SmtpHost, _options.SmtpPort, _options.FromAddress);
        }
        else
        {
            _logger.LogInformation(
                "Email notifications are not configured, so they will be written to this log "
                + "instead of sent. See the Email notifications section of the README to enable them.");
        }

        await foreach (var message in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DeliverAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed notification must never take the application down, and
                // there is no request left to report it to, so it is logged and
                // the loop carries on with the next message.
                _logger.LogError(ex,
                    "Failed to send the {Kind} notification to {Recipient}.",
                    message.Kind, message.ToAddress);
            }
        }
    }

    private async Task DeliverAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation(
                "[email not sent - not configured] to={Recipient} kind={Kind} subject=\"{Subject}\"\n{Body}",
                message.ToAddress, message.Kind, message.Subject, message.TextBody);
            return;
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        using var client = new SmtpClient { Timeout = _options.TimeoutSeconds * 1000 };

        var socketOptions = _options.UseImplicitTls
            ? SecureSocketOptions.SslOnConnect      // port 465
            : SecureSocketOptions.StartTls;         // port 587

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.UserName))
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation(
            "Sent the {Kind} notification to {Recipient}.", message.Kind, message.ToAddress);
    }
}
