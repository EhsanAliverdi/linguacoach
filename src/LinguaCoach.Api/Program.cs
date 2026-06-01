using System.Text;
using LinguaCoach.Infrastructure;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
if (builder.Environment.IsEnvironment("Testing"))
{
    // In tests, WebApplicationFactory overrides the DbContextOptions via
    // ConfigureServices to inject a shared SQLite in-memory connection.
    // We register a no-op placeholder here so Identity's AddEntityFrameworkStores
    // wires up without a real connection string.
    var testDb = builder.Configuration["TestDatabase"] ?? $"DataSource=linguacoach_test;Mode=Memory;Cache=Shared";
    builder.Services.AddDbContext<LinguaCoachDbContext>(options =>
        options.UseSqlite(testDb));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is not configured.");
    builder.Services.AddDbContext<LinguaCoachDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// ── ASP.NET Identity ────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    // Admin creates accounts; email confirmation is set programmatically.
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<LinguaCoachDbContext>()
.AddDefaultTokenProviders();

// ── JWT Authentication ──────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");

// Reject the committed placeholder key in any environment other than Development.
// Real deployments must supply Jwt:Key via environment variable or secrets manager.
const string JwtKeyPlaceholder = "CHANGE_ME_IN_PRODUCTION_USE_A_SECRET_AT_LEAST_32_CHARS";
if (!builder.Environment.IsDevelopment())
{
    if (jwtKey.Length < 32)
        throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
    if (jwtKey == JwtKeyPlaceholder)
        throw new InvalidOperationException(
            "Jwt:Key is set to the development placeholder. " +
            "Set a real secret via the JWT_KEY environment variable or a secrets manager before deploying.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

// ── Infrastructure services ─────────────────────────────────────────────────
builder.Services.AddInfrastructure();

// ── API ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Run migrations on startup (skipped in Testing environment) ──────────────
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// TODO before first real user: enforce MustChangePassword at the API layer so students
// cannot access /onboarding, /dashboard, or /reference until they have changed their
// admin-issued temporary password. Options: middleware that checks the claim, or an
// ActionFilter applied to all [Authorize] controllers. Currently the flag is returned
// in the login response and the frontend is expected to redirect; there is no backend
// enforcement.

app.MapControllers();
app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program { }
