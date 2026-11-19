using System.Text.RegularExpressions;

namespace Estudarias.Domain.Common;

/// <summary>
/// O formato dos códigos do conteúdo (tipo de erro, exercício e flashcard): começa por letra minúscula e tem letras minúsculas e
/// dígitos separados por um <c>_</c>, com até <see cref="MaxLength"/> caracteres. O código é a chave natural do item na carga do
/// conteúdo, então é estável entre cargas.
/// </summary>
public static partial class ContentCode
{
    public const int MaxLength = 100;

    [GeneratedRegex(@"^[a-z][a-z0-9]*(_[a-z0-9]+)*\z")]
    private static partial Regex Format();

    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length <= MaxLength && Format().IsMatch(code);
}
