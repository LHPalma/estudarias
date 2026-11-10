using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Identity;
using Estudarias.Application.Common;

namespace Estudarias.Application.Identity;

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<AuthPayload> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var password = command.Password ?? "";

        // Um e-mail malformado falha como qualquer outra credencial errada, e a senha ainda é conferida (contra o hash descartável
        // quando não há usuário), então a resposta não diz qual parte estava errada nem leva um tempo diferente.
        var user = User.TryNormalizeEmail(command.Email, out var email)
            ? await users.FindByEmailAsync(email, cancellationToken)
            : null;

        var passwordMatches = hasher.Verify(user?.PasswordHash, password);

        if (user is null || !passwordMatches || !user.IsActive)
        {
            throw IdentityErrors.InvalidCredentials();
        }

        RehashIfPepperRotated(user, password);
        await refreshTokens.RemoveExpiredAsync(user.Id, now, cancellationToken);

        var (refreshToken, rawRefreshToken) = SessionTokens.IssueRefreshToken(user.Id, Guid.CreateVersion7(), now);
        refreshTokens.Add(refreshToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var access = accessTokens.Issue(user);
        return new AuthPayload(access.Token, access.ExpiresAt, rawRefreshToken, user);
    }

    // O login é o único momento em que a senha em texto puro está à mão, então é a única chance de levar um hash derivado com um
    // pepper antigo para o atual. Uma senha que já não cumpre a política mantém o hash antigo.
    private void RehashIfPepperRotated(User user, string password)
    {
        if (!hasher.NeedsRehash(user.PasswordHash))
        {
            return;
        }

        try
        {
            user.Rehash(hasher.Hash(Password.Create(password)));
        }
        catch (DomainValidationException)
        {
            // A senha entrou quando a política era mais branda: o hash antigo continua valendo.
        }
    }
}
