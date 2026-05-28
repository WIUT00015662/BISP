using System.ComponentModel.DataAnnotations;

namespace Bisp.Api.Dtos;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string ConfirmPassword);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record LoginResponse(
    string Token,
    string Email,
    string Role,
    DateTime ExpiresAt);

public sealed record ResendConfirmationRequest(
    [Required, EmailAddress] string Email);
