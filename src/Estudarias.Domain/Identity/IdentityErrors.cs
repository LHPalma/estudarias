using Estudarias.Domain.Common.Exceptions;

namespace Estudarias.Domain.Identity;

public static class IdentityErrors
{
    public const string CredentialsInvalid = "identity.credentials.invalid";
    public const string EmailInvalid = "identity.email.invalid";
    public const string EmailTaken = "identity.email.taken";
    public const string PasswordHashInvalid = "identity.passwordHash.invalid";
    public const string PasswordNoDigit = "identity.password.noDigit";
    public const string PasswordNoLowercase = "identity.password.noLowercase";
    public const string PasswordNoSpecial = "identity.password.noSpecial";
    public const string PasswordNoUppercase = "identity.password.noUppercase";
    public const string PasswordTooLong = "identity.password.tooLong";
    public const string PasswordTooShort = "identity.password.tooShort";
    public const string RefreshTokenAlreadyRevoked = "identity.refreshToken.alreadyRevoked";
    public const string RefreshTokenInvalid = "identity.refreshToken.invalid";
    public const string SessionInvalid = "identity.session.invalid";

    public static DomainAuthenticationException InvalidCredentials() =>
        new(CredentialsInvalid, "E-mail or password not valid.");

    public static DomainAuthenticationException InvalidRefreshToken() =>
        new(RefreshTokenInvalid, "Refresh token not valid: it is unknown, expired, already used or revoked.");

    public static DomainAuthenticationException InvalidSession() =>
        new(SessionInvalid, "Session not valid: the account does not exist or is disabled.");

    public static DomainValidationException InvalidEmail() =>
        new(EmailInvalid, "E-mail not valid.");

    public static DomainConflictException EmailInUse() =>
        new(EmailTaken, "There is already a user with this e-mail.");

    public static DomainValidationException InvalidPasswordHash() =>
        new(PasswordHashInvalid, "Password hash not valid: expected a PHC string and a pepper version of 1 or more.");

    public static DomainValidationException MissingDigit() =>
        new(PasswordNoDigit, "Password must contain a digit.");

    public static DomainValidationException MissingLowercase() =>
        new(PasswordNoLowercase, "Password must contain a lowercase letter.");

    public static DomainValidationException MissingSpecial() =>
        new(PasswordNoSpecial, "Password must contain a special character.");

    public static DomainValidationException MissingUppercase() =>
        new(PasswordNoUppercase, "Password must contain an uppercase letter.");

    public static DomainValidationException PasswordLongerThan(int maximum) =>
        new(PasswordTooLong, $"Password cannot exceed {maximum} characters.");

    public static DomainValidationException PasswordShorterThan(int minimum) =>
        new(PasswordTooShort, $"Password must have at least {minimum} characters.");

    public static DomainConflictException RefreshTokenRevoked() =>
        new(RefreshTokenAlreadyRevoked, "Refresh token was already revoked.");
}
