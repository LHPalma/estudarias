namespace Estudarias.Domain.Identity;

/// <summary>
/// Uma senha em texto puro que já passou pela política de composição. A porta de hash só aceita este tipo, então um
/// hash nunca é derivado de uma senha que ninguém validou.
/// </summary>
public sealed record Password
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public const string SpecialCharacters = @"!""#$%&'()*+,-./:;<=>?@[\]^_`{|}~";

    private Password(string value) => Value = value;

    public string Value { get; }

    /// <summary>Confere a senha crua contra a política; lança dizendo qual regra falhou.</summary>
    public static Password Create(string? input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < MinimumLength)
        {
            throw IdentityErrors.PasswordShorterThan(MinimumLength);
        }

        if (input.Length > MaximumLength)
        {
            throw IdentityErrors.PasswordLongerThan(MaximumLength);
        }

        if (!input.Any(char.IsUpper))
        {
            throw IdentityErrors.MissingUppercase();
        }

        if (!input.Any(char.IsLower))
        {
            throw IdentityErrors.MissingLowercase();
        }

        if (!input.Any(char.IsDigit))
        {
            throw IdentityErrors.MissingDigit();
        }

        if (!input.Any(SpecialCharacters.Contains))
        {
            throw IdentityErrors.MissingSpecial();
        }

        return new Password(input);
    }

    public override string ToString() => "Password(hidden)";
}
