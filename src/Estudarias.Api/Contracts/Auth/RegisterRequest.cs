namespace Estudarias.Api.Contracts.Auth;

/// <summary>
/// Os campos são anuláveis de propósito: o domínio já valida nulo e branco com o código certo (<c>identity.email.invalid</c>,
/// <c>identity.password.tooShort</c>, <c>student.name.required</c>), então uma requisição incompleta recebe o mesmo formato de erro
/// (com <c>code</c>) que qualquer outra recusa, e não um erro de binding de outro formato.
/// </summary>
public sealed record RegisterRequest(string? Name, string? Email, string? Password);
