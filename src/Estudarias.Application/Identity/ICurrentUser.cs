namespace Estudarias.Application.Identity;

/// <summary>Quem está fazendo a requisição, sem dizer como essa identidade foi estabelecida.</summary>
public interface ICurrentUser
{
    /// <summary>O id da conta da requisição, ou nulo quando não há requisição ou a identidade não foi estabelecida.</summary>
    Guid? UserId { get; }
}
