using System.Security.Claims;
using Bisp.Api.Data;
using Bisp.Api.Dtos;
using Bisp.Api.Models;
using Bisp.Api.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Bisp.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly StripeOptions _stripe;
    private readonly IConfiguration _config;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        AppDbContext db,
        IOptions<StripeOptions> stripe,
        IConfiguration config,
        ILogger<SubscriptionsController> logger)
    {
        _db = db;
        _stripe = stripe.Value;
        _config = config;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult<SubscriptionStatusDto>> Status(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var sub = await _db.UserSubscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CurrentPeriodEndUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (sub is null)
            return Ok(new SubscriptionStatusDto(false, null, null));

        return Ok(new SubscriptionStatusDto(sub.Status == "active", sub.Status, sub.CurrentPeriodEndUtc));
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutSessionResponse>> CreateCheckout(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var email = User.FindFirstValue(ClaimTypes.Email)
                    ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);

        var frontendBase = _config["FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";

        // Get or create Stripe customer
        var sub = await _db.UserSubscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CurrentPeriodEndUtc)
            .FirstOrDefaultAsync(cancellationToken);

        string? customerId = sub?.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions { Email = email }, cancellationToken: cancellationToken);
            customerId = customer.Id;

            _db.UserSubscriptions.Add(new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StripeCustomerId = customerId,
                Status = "pending"
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = _stripe.PriceId,
                    Quantity = 1
                }
            ],
            SuccessUrl = $"{frontendBase}/subscription?success=true",
            CancelUrl = $"{frontendBase}/subscription?canceled=true",
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { ["userId"] = userId }
            }
        }, cancellationToken: cancellationToken);

        return Ok(new CheckoutSessionResponse(session.Url));
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();

        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _stripe.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Invalid Stripe webhook signature.");
            return BadRequest();
        }

        switch (stripeEvent.Type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
            {
                var stripeSub = (Stripe.Subscription)stripeEvent.Data.Object;
                await UpsertSubscriptionAsync(stripeSub);
                break;
            }
            case "customer.subscription.deleted":
            {
                var stripeSub = (Stripe.Subscription)stripeEvent.Data.Object;
                await UpsertSubscriptionAsync(stripeSub);
                break;
            }
        }

        return Ok();
    }

    private async Task UpsertSubscriptionAsync(Stripe.Subscription stripeSub)
    {
        // Find userId from metadata or customer lookup
        string? userId = null;
        if (stripeSub.Metadata.TryGetValue("userId", out var uid))
            userId = uid;

        if (string.IsNullOrWhiteSpace(userId))
        {
            // Look up by StripeCustomerId
            var existing = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.StripeCustomerId == stripeSub.CustomerId);
            userId = existing?.UserId;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Webhook: cannot resolve userId for Stripe subscription {SubId}.", stripeSub.Id);
            return;
        }

        var sub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id)
            ?? await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId);

        if (sub is null)
        {
            sub = new UserSubscription { Id = Guid.NewGuid(), UserId = userId };
            _db.UserSubscriptions.Add(sub);
        }

        sub.StripeCustomerId = stripeSub.CustomerId;
        sub.StripeSubscriptionId = stripeSub.Id;
        sub.Status = stripeSub.Status;
        sub.CurrentPeriodEndUtc = stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Subscription {SubId} for user {UserId} updated to {Status}.", stripeSub.Id, userId, stripeSub.Status);
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("User ID claim missing.");
}
