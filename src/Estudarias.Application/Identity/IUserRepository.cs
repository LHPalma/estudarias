using Estudarias.Domain.Identity;

namespace Estudarias.Application.Identity;

public interface IUserRepository
{
    /// <summary>Procura pelo e-mail já normalizado (<see cref="User.NormalizeEmail"/>); nulo quando não existe.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Confere pelo e-mail já normalizado se já existe uma conta.</summary>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Marca a conta para ser gravada; só vale depois de <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(User user);
}
