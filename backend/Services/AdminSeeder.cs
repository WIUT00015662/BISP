using Bisp.Api.Data;
using Bisp.Api.Models;
using Bisp.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bisp.Api.Services;

public sealed class AdminSeeder
{
    private const string AdminRole = "Admin";
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AdminOptions _options;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<AdminOptions> options,
        ILogger<AdminSeeder> logger)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedStoresAsync();
        await SeedAdminAsync();
    }

    private async Task SeedStoresAsync()
    {
        var stores = new[]
        {
            new Store { Code = "steam", Name = "Steam" },
            new Store { Code = "gog", Name = "GOG" },
            new Store { Code = "epic", Name = "Epic Games Store" }
        };

        foreach (var store in stores)
        {
            if (!await _db.Stores.AnyAsync(s => s.Code == store.Code))
            {
                _db.Stores.Add(store);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning("Admin seed skipped: missing admin credentials in configuration.");
            return;
        }

        if (!await _roleManager.RoleExistsAsync(AdminRole))
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole(AdminRole));
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("Failed to create admin role: {Errors}", roleResult.Errors);
                return;
            }
        }

        var user = await _userManager.FindByEmailAsync(_options.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = _options.Email,
                Email = _options.Email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, _options.Password);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning("Failed to create admin user: {Errors}", createResult.Errors);
                return;
            }
        }

        if (!await _userManager.IsInRoleAsync(user, AdminRole))
        {
            var addRoleResult = await _userManager.AddToRoleAsync(user, AdminRole);
            if (!addRoleResult.Succeeded)
            {
                _logger.LogWarning("Failed to assign admin role: {Errors}", addRoleResult.Errors);
            }
        }
    }
}
