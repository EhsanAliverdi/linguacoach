using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Auth;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class CreateStudentHandler : ICreateStudentHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly LinguaCoachDbContext _db;
    private readonly INotificationService _notifications;
    private readonly INotificationTemplateRenderer _templateRenderer;
    private readonly IAuthSecurityAuditService _audit;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateStudentHandler> _logger;

    public CreateStudentHandler(
        UserManager<ApplicationUser> userManager,
        LinguaCoachDbContext db,
        INotificationService notifications,
        INotificationTemplateRenderer templateRenderer,
        IAuthSecurityAuditService audit,
        IHttpContextAccessor httpContext,
        IConfiguration configuration,
        ILogger<CreateStudentHandler> logger)
    {
        _userManager = userManager;
        _db = db;
        _notifications = notifications;
        _templateRenderer = templateRenderer;
        _audit = audit;
        _httpContext = httpContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CreateStudentResult> HandleAsync(CreateStudentCommand command, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(command.Email);
        if (existing is not null)
            throw new InvalidOperationException($"A user with email '{command.Email}' already exists.");

        var user = new ApplicationUser
        {
            UserName = command.Email,
            Email = command.Email,
            Role = UserRole.Student,
            MustChangePassword = command.MustChangePassword,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, command.TemporaryPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var profile = new StudentProfile(user.Id);

        var hasProfileContext = !string.IsNullOrWhiteSpace(command.FirstName)
            || !string.IsNullOrWhiteSpace(command.LastName)
            || !string.IsNullOrWhiteSpace(command.DisplayName)
            || !string.IsNullOrWhiteSpace(command.CareerContext)
            || !string.IsNullOrWhiteSpace(command.LearningGoal)
            || command.PreferredSessionDurationMinutes.HasValue
            || command.ProfessionalExperienceLevel.HasValue
            || command.RoleFamiliarity.HasValue;

        if (hasProfileContext)
        {
            profile.SetInitialProfile(
                command.FirstName,
                command.LastName,
                command.DisplayName,
                command.CareerContext,
                command.LearningGoal,
                command.PreferredSessionDurationMinutes,
                command.ProfessionalExperienceLevel,
                command.RoleFamiliarity);
        }

        var lifecycle = command.MustChangePassword
            ? StudentLifecycleStage.PasswordChangeRequired
            : hasProfileContext && command.ProfessionalExperienceLevel.HasValue && command.RoleFamiliarity.HasValue
                ? StudentLifecycleStage.PlacementRequired
                : StudentLifecycleStage.OnboardingRequired;

        profile.SetLifecycleStage(lifecycle);

        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        // Record audit event — never include the temporary password.
        var ip = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var correlationId = _httpContext.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();
        await _audit.RecordAsync(new AuthSecurityEventRecord(
            AuthEventType.StudentAccountCreated, AuthEventOutcome.Success,
            UserId: user.Id, EmailOrUserName: user.Email,
            IpAddress: ip, CorrelationId: correlationId), ct);

        // Queue welcome/account-created email. Does not include the temporary password.
        // If queueing fails, log and continue — student creation still succeeds.
        try
        {
            var (emailSubject, emailBody) = await ResolveStudentCreatedEmailContentAsync(
                user.Email!, command.DisplayName, ct);

            await _notifications.QueueEmailAsync(
                recipientUserId: user.Id,
                title: emailSubject,
                body: emailBody,
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Info,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to queue welcome email notification for new student {UserId}. Creation still succeeded.",
                user.Id);
        }

        return new CreateStudentResult(profile.Id, user.Id);
    }

    private async Task<(string Subject, string Body)> ResolveStudentCreatedEmailContentAsync(
        string email, string? displayName, CancellationToken ct)
    {
        var template = await _db.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.TemplateKey == "account.student_created" &&
                t.Channel == NotificationChannel.Email &&
                t.IsActive, ct);

        var appName = _configuration["PublicApp:AppName"] ?? "SpeakPath";
        var baseUrl = _configuration["PublicApp:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
        var loginUrl = $"{baseUrl}/login";

        if (template is null)
        {
            _logger.LogWarning(
                "Active template 'account.student_created'/Email not found. Using fallback content.");
            return (
                "Welcome to SpeakPath",
                "Your SpeakPath account has been created. Please log in with the credentials provided by your administrator. You will be prompted to set a new password on first login."
            );
        }

        var resolvedDisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : email;

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DisplayName"] = resolvedDisplayName,
            ["AppName"] = appName,
            ["LoginUrl"] = loginUrl,
            ["AppUrl"] = baseUrl,
        };

        var rendered = _templateRenderer.Render(template.Subject, template.Title, template.Body, variables);

        if (rendered.MissingVariables.Count > 0)
            _logger.LogWarning(
                "Template 'account.student_created'/Email has missing variables: {Vars}",
                string.Join(", ", rendered.MissingVariables));

        return (rendered.RenderedSubject ?? $"Welcome to {appName}", rendered.RenderedBody);
    }
}
