namespace Estudarias.Application.Common.Abstractions;

/// <summary>Aluno autenticado na requisição atual.</summary>
public interface ICurrentStudent
{
    Guid Id { get; }
}
