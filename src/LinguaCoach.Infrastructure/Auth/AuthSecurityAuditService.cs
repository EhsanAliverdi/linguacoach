using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Auth;

public sealed class AuthSecurityAuditService : IAuthSecurityAuditService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<AuthSecurityAuditService> _logger;

    public AuthSecurityAuditService(LinguaCoachDbContext db, ILogger<AuthSecurityAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordAsync(AuthSecurityEventRecord record, CancellationToken ct = default)
    {
        try
        {
            var entity = new AuthSecurityEvent(
                eventType: record.EventType,
                outcome: record.Outcome,
                userId: record.UserId,
                emailOrUserName: record.EmailOrUserName,
                failureReasonCode: record.FailureReasonCode,
                ipAddress: record.IpAddress,
                userAgent: record.UserAgent,
                correlationId: record.CorrelationId,
                metadataJson: record.MetadataJson);

            _db.AuthSecurityEvents.Add(entity);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit persistence failure must never abort an auth flow
            _logger.LogError(ex,
                "Failed to persist auth security event EventType={EventType} Outcome={Outcome} UserId={UserId}",
                record.EventType, record.Outcome, record.UserId);
        }
    }
}
