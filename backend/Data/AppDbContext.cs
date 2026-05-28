using Bisp.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bisp.Api.Data;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<Store> Stores { get; set; } = null!;
    public DbSet<GameStorePrice> GameStorePrices { get; set; } = null!;
    public DbSet<ExternalGameId> ExternalGameIds { get; set; } = null!;
    public DbSet<WishlistItem> WishlistItems { get; set; } = null!;
    public DbSet<UserSubscription> UserSubscriptions { get; set; } = null!;
    public DbSet<NotificationLog> NotificationLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Store>()
            .HasIndex(store => store.Code)
            .IsUnique();

        builder.Entity<GameStorePrice>()
            .Property(price => price.CurrentPrice)
            .HasPrecision(10, 2);

        builder.Entity<GameStorePrice>()
            .Property(price => price.RegularPrice)
            .HasPrecision(10, 2);

        builder.Entity<WishlistItem>()
            .Property(item => item.MinDiscountPercent)
            .HasPrecision(5, 2);

        builder.Entity<NotificationLog>()
            .Property(log => log.DiscountPercent)
            .HasPrecision(5, 2);

        builder.Entity<ExternalGameId>()
            .HasIndex(e => new { e.GameId, e.Provider })
            .IsUnique();
    }
}
