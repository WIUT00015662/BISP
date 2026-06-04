using System.Text;
using Bisp.Api.Data;
using Bisp.Api.Models;
using Bisp.Api.Options;
using Bisp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<IgdbOptions>(builder.Configuration.GetSection(IgdbOptions.SectionName));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<AggregationOptions>(builder.Configuration.GetSection(AggregationOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));

var aggregationOptions = builder.Configuration.GetSection(AggregationOptions.SectionName)
    .Get<AggregationOptions>() ?? new AggregationOptions();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Spa", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// Stripe
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrWhiteSpace(stripeSecretKey))
    StripeConfiguration.ApiKey = stripeSecretKey;

// Named HttpClients for external services
builder.Services.AddHttpClient<IgdbService>(client =>
    client.BaseAddress = new Uri("https://api.igdb.com/v4/"));
builder.Services.AddHttpClient<SteamService>();
builder.Services.AddHttpClient<GogService>();
builder.Services.AddHttpClient<EpicService>();

// Strongly typed options for IgdbService constructor (not IOptions<T>)
var igdbOptions = builder.Configuration.GetSection(IgdbOptions.SectionName).Get<IgdbOptions>() ?? new IgdbOptions();
builder.Services.AddSingleton(igdbOptions);

// Application services
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<AggregationService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<EmailSender>();
builder.Services.AddScoped<AdminSeeder>();

// Background workers
if (aggregationOptions.Enabled)
    builder.Services.AddHostedService<AggregationWorker>();
builder.Services.AddHostedService<NotificationWorker>();

var app = builder.Build();

app.UseExceptionHandler(err => err.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new { error = "Internal server error." });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("Spa");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
    await seeder.SeedAsync();
}

await app.RunAsync();
