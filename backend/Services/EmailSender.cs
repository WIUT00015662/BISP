namespace Bisp.Api.Services;

public sealed class EmailSender
{
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email queued to {ToAddress} with subject {Subject}.", toAddress, subject);
        return Task.CompletedTask;
    }
}
