using Bisp.Api.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bisp.Api.Services;

public sealed class EmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<SmtpOptions> options, ILogger<EmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogWarning("SMTP not configured — skipping email to {ToAddress}.", toAddress);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.User))
                await client.AuthenticateAsync(_options.User, _options.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent to {ToAddress}: {Subject}", toAddress, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToAddress}.", toAddress);
        }
    }
}
