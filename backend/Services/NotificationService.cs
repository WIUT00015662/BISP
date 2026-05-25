using Bisp.Api.Data;

namespace Bisp.Api.Services;

public sealed class NotificationService
{
    private readonly AppDbContext _dbContext;
    private readonly EmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext dbContext, EmailSender emailSender, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _logger = logger;
    }

    public Task CheckWishlistAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Wishlist check requested at {UtcNow}.", DateTime.UtcNow);
        // Placeholder: query wishlist items and send email when discount threshold is met.
        return Task.CompletedTask;
    }
}
