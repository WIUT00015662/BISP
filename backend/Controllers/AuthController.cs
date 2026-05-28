using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bisp.Api.Dtos;
using Bisp.Api.Models;
using Bisp.Api.Options;
using Bisp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bisp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly EmailSender _emailSender;
    private readonly JwtOptions _jwt;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        EmailSender emailSender,
        IOptions<JwtOptions> jwt,
        IConfiguration config,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _jwt = jwt.Value;
        _config = config;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (req.Password != req.ConfirmPassword)
            return BadRequest(new { error = "Passwords do not match." });

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await SendConfirmationEmailAsync(user);

        return StatusCode(201, new { message = "Registration successful. Please check your email to confirm your account." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null)
            return Unauthorized(new { error = "Invalid credentials." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            if (result.IsNotAllowed)
                return Unauthorized(new { error = "Email not confirmed. Please check your inbox." });
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";
        var token = GenerateJwt(user, role);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        return Ok(new LoginResponse(token, user.Email!, role, expiresAt));
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return BadRequest(new { error = "Invalid confirmation link." });

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return BadRequest(new { error = "Email confirmation failed. The link may have expired." });

        return Ok(new { message = "Email confirmed successfully. You can now log in." });
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        // Always return 200 to avoid user enumeration
        if (user is not null && !user.EmailConfirmed)
            await SendConfirmationEmailAsync(user);

        return Ok(new { message = "If an unconfirmed account with that email exists, a confirmation email has been sent." });
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var frontendBase = _config["FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
        var link = $"{frontendBase}/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

        var body = $"""
            <html><body style="font-family:sans-serif;max-width:600px;margin:auto;padding:20px">
              <h2>Confirm your BISP account</h2>
              <p>Click the button below to confirm your email address and activate your account.</p>
              <a href="{link}" style="display:inline-block;padding:12px 24px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold">
                Confirm Email
              </a>
              <p style="color:#6b7280;font-size:0.85em;margin-top:24px">If you didn't create an account, you can ignore this email.</p>
            </body></html>
            """;

        await _emailSender.SendAsync(user.Email!, "Confirm your BISP account", body);
    }

    private string GenerateJwt(ApplicationUser user, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
