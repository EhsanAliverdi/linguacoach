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
}
