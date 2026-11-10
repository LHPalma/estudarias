namespace Estudarias.Infrastructure.UnitTests.Support;

/// <summary>Peppers só dos testes, com o tamanho mínimo exigido. Não protegem nada: existem para ser usados em teste.</summary>
internal static class TestSecrets
{
    public const string Pepper = "test-pepper-0123456789-abcdefghijklmnop";

    public const string OtherPepper = "other-test-pepper-9876543210-zyxwvutsrq";
}
