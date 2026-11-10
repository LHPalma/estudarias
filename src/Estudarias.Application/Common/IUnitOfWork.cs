namespace Estudarias.Application.Common;

/// <summary>
/// Grava de uma vez tudo o que os repositórios marcaram. Um caso de uso que mexe em mais de um agregado (o cadastro cria um
/// aluno e uma conta) chama <see cref="SaveChangesAsync"/> uma só vez, então as duas mudanças entram juntas ou nenhuma entra.
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="ConcurrencyConflictException">Outra requisição alterou uma linha que esta também alterou.</exception>
    /// <exception cref="UniqueConstraintViolationException">A gravação repetiu um valor que precisa ser único.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
