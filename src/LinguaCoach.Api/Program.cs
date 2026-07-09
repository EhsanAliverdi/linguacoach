using System.Text;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LinguaCoach.Api.Middleware;
using LinguaCoach.Api.Quartz;
using LinguaCoach.Infrastructure;
using LinguaCoach.Infrastructure.Diagnostics;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using LinguaCoach.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Log level from environment variables ────────────────────────────────────
// Override via: LOG_LEVEL=Debug, LOG_LEVEL_MICROSOFT=Warning, LOG_LEVEL_EFCORE=Warning
var logLevel = builder.Configuration["LOG_LEVEL"] ?? "Information";
var logLevelMicrosoft = builder.Configuration["LOG_LEVEL_MICROSOFT"] ?? "Warning";
var logLevelEfCore = builder.Configuration["LOG_LEVEL_EFCORE"] ?? "Warning";

builder.Logging.SetMinimumLevel(Enum.Parse<LogLevel>(logLevel, ignoreCase: true));
builder.Logging.AddFilter("Microsoft", Enum.Parse<LogLevel>(logLevelMicrosoft, ignoreCase: true));
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", Enum.Parse<LogLevel>(logLevelEfCore, ignoreCase: true));

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
    // Password policy — enterprise baseline
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
    // Admin creates accounts; email confirmation is set programmatically.
    options.SignIn.RequireConfirmedEmail = false;
    // Lockout — brute-force protection
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
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

var writingAiPermitLimit = builder.Configuration.GetValue<int?>("WritingAi:RateLimit:PermitLimit") ?? 20;
var writingAiWindowMinutes = builder.Configuration.GetValue<int?>("WritingAi:RateLimit:WindowMinutes") ?? 10;
if (writingAiPermitLimit < 1)
    throw new InvalidOperationException("WritingAi:RateLimit:PermitLimit must be at least 1.");
if (writingAiWindowMinutes < 1)
    throw new InvalidOperationException("WritingAi:RateLimit:WindowMinutes must be at least 1.");

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("WritingAi", context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = writingAiPermitLimit,
            Window = TimeSpan.FromMinutes(writingAiWindowMinutes),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // Auth login: 10 attempts per IP per 5 minutes — reduces password spray
    options.AddPolicy("AuthLogin", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // Password reset link: 3 attempts per IP per 15 minutes — reduces reset-link abuse
    options.AddPolicy("AuthReset", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"reset:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // Refresh token: 30 requests per IP per 5 minutes — prevents brute-force on refresh endpoint
    options.AddPolicy("AuthRefresh", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"refresh:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // External login: 20 requests per IP per 5 minutes
    options.AddPolicy("AuthExternalLogin", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"extlogin:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // Authenticated change-password: 10 attempts per user per 5 minutes
    options.AddPolicy("AuthChangePassword", context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"chgpwd:{userId}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        var isAuthEndpoint = context.HttpContext.Request.Path.StartsWithSegments("/api/auth");
        var message = isAuthEndpoint
            ? "Too many requests. Please try again later."
            : "Please wait before requesting more writing feedback.";

        await context.HttpContext.Response.WriteAsJsonAsync(new { error = message }, token);
    };
});

// ── CORS ─────────────────────────────────────────────────────────────────────
// Allow Angular dev server in Development. In production, the Angular build is
// served from the same origin (or a proxy), so no CORS needed.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins("http://localhost:4200", "http://localhost:4300")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── HTTP context accessor (used by Infrastructure services for IP/UA extraction) ──
builder.Services.AddHttpContextAccessor();

// ── Correlation ID (scoped per request) ────────────────────────────────────
builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

// ── Diagnostic event buffer ─────────────────────────────────────────────────
var enableDiagEvents = builder.Configuration.GetValue<bool>("Diagnostics:EnableAdminDiagnosticEvents", true);
var diagEventLimit = builder.Configuration.GetValue<int>("Diagnostics:AdminDiagnosticEventLimit", 500);
var diagBuffer = new DiagnosticEventBuffer(diagEventLimit, enableDiagEvents);
builder.Services.AddSingleton(diagBuffer);
if (enableDiagEvents)
    builder.Logging.AddProvider(new DiagnosticLoggerProvider(diagBuffer));

// ── Infrastructure services ─────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Quartz background jobs (lesson buffer / TTS / cleanup) ──────────────────
// Runs inside the API process as IHostedService. Skipped in Testing (SQLite has no Quartz schema).
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddSpeakPathQuartz(builder.Configuration);

// ── API ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

var writingFeedbackProvider = app.Configuration["AI:WritingFeedback:Provider"];
var writingFeedbackModel = app.Configuration["AI:WritingFeedback:Model"];
var openAiApiKey = app.Configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var geminiApiKey = app.Configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
if (string.IsNullOrWhiteSpace(writingFeedbackProvider) || string.IsNullOrWhiteSpace(writingFeedbackModel))
{
    app.Logger.LogWarning(
        "AI writing feedback provider/model is not configured. Set AI__WritingFeedback__Provider and AI__WritingFeedback__Model for production writing feedback.");
}
if (string.IsNullOrWhiteSpace(openAiApiKey) && string.IsNullOrWhiteSpace(geminiApiKey))
{
    app.Logger.LogWarning(
        "No AI provider API key is configured. Set OPENAI_API_KEY or GEMINI_API_KEY for AI-backed features.");
}

// ── Run migrations and seed on startup (skipped in Testing environment) ─────
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
    db.Database.Migrate();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await AdminSeeder.SeedAsync(userManager, app.Configuration, seederLogger);
    await DefaultAiSeeder.SeedAsync(db, seederLogger);
    await WritingScenarioSeeder.SeedAsync(db, seederLogger);
    await LearningActivitySeeder.SeedAsync(db, seederLogger);
    await ExercisePatternSeeder.SeedAsync(db, seederLogger);
    await ExerciseTypeDefinitionSeeder.SeedAsync(db, seederLogger);
    await PlacementItemBankSeeder.SeedAsync(db);
    await LinguaCoach.Persistence.Seed.CurriculumObjectiveSeeder.SeedAsync(db, seederLogger);
    await LinguaCoach.Persistence.Seed.UsageGovernanceSeeder.SeedAsync(db);
    await LinguaCoach.Persistence.Seed.NotificationTemplateSeeder.SeedAsync(db, seederLogger);
    await LinguaCoach.Persistence.Seed.OnboardingTemplateSeeder.SeedAsync(db, seederLogger);
    await LinguaCoach.Persistence.Seed.ActivityTemplateSeeder.SeedAsync(db, seederLogger);
    await LinguaCoach.Persistence.Seed.InternalResourceSeedPackSeeder.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.ResourceImport.IResourceImportService>(),
        scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.ResourceImport.IResourceCandidateValidationService>(),
        scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.ResourceImport.IResourceCandidatePublishService>(),
        seederLogger);
    // Phase E8 — a second, independent internal English depth-expansion pack (grammar/usage/reading),
    // idempotent by its own source name, flowing through the same staging → validation → approval →
    // publish pipeline as the E6/E7 pack above.
    await LinguaCoach.Persistence.Seed.InternalResourceSeedPackE8Seeder.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.ResourceImport.IResourceImportService>(),
        scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.ResourceImport.IResourceCandidateValidationService>(),
        scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.ResourceImport.IResourceCandidatePublishService>(),
        seederLogger);
    // Phase E9 — idempotent, one-time backfill of the lean published bank tables' new selection
    // metadata (context/focus tags, subskill, difficulty band) from the ResourceCandidate that
    // published each row. Safe no-op once every traceable row is backfilled. Metadata repair only —
    // never inserts a bank row.
    await LinguaCoach.Persistence.Seed.PublishedBankMetadataBackfillSeeder.RunAsync(db, seederLogger);

    // Storage + Quartz startup health checks (warn-only — do not block startup).
    var storage = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Storage.IFileStorageService>();
    var storageError = await storage.HealthCheckAsync();
    if (storageError is not null)
        seederLogger.LogWarning("File storage health check failed: {Error}", storageError);

    await QuartzConfiguration.ValidateQuartzSchemaAsync(app.Services, seederLogger);
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseHttpsRedirection();
if (app.Environment.IsDevelopment()) app.UseCors("AngularDev");
app.UseRouting();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && !context.Request.Path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase))
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (Guid.TryParse(userIdValue, out var userId))
        {
            var db = context.RequestServices.GetRequiredService<LinguaCoachDbContext>();
            var mustChangePassword = await db.Users
                .Where(user => user.Id == userId)
                .Select(user => user.MustChangePassword)
                .FirstOrDefaultAsync(context.RequestAborted);

            if (mustChangePassword)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "You must change your temporary password before continuing."
                }, context.RequestAborted);
                return;
            }
        }
    }

    await next();
});

app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program { }
