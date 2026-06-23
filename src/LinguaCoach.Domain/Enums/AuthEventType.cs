namespace LinguaCoach.Domain.Enums;

public enum AuthEventType
{
    LoginSucceeded,
    LoginFailed,
    LoginLockedOut,
    PasswordChanged,
    PasswordChangeFailed,
    ForcePasswordChangeCompleted,
    PasswordResetRequested,
    PasswordResetSucceeded,
    PasswordResetFailed,
    StudentAccountCreated,
    RefreshTokenIssued,
    RefreshTokenRotated,
    RefreshTokenRevoked,
    RefreshTokenReuseDetected,
    LogoutSucceeded,
    AllSessionsRevoked,
    ExternalLoginSucceeded,
    ExternalLoginFailed,
    ExternalLoginLinked,
    ExternalLoginRejected,
    ExternalProviderDisabled,
    ExternalEmailUnverified,
    ExternalDomainRejected,
}
