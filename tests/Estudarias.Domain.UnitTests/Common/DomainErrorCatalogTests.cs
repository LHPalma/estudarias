using System.Reflection;
using System.Text.RegularExpressions;
using Estudarias.Domain.Common;

namespace Estudarias.Domain.UnitTests.Common;

public partial class DomainErrorCatalogTests
{
    // Toda invariante nova quebra o teste até o código ser registrado aqui. É proposital.
    private static readonly string[] RegisteredCodes =
    [
        "student.name.required",
        "student.name.tooLong",
        "student.group.invalid",
        "student.topic.locked",
        "topic.name.required",
        "topic.name.tooLong",
        "topic.slug.required",
        "topic.slug.invalid",
        "topic.prerequisite.self",
        "topic.prerequisite.duplicated",
    ];

    [GeneratedRegex(@"^[a-z][a-zA-Z]*\.[a-z][a-zA-Z]*\.[a-z][a-zA-Z]*$")]
    private static partial Regex CodeFormat();

    private static string[] CollectCodes() =>
        typeof(BaseEntity).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: true, IsSealed: true } && type.Name.EndsWith("Errors"))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

    [Fact(DisplayName = "Nenhum código de erro se repete")]
    public void Codes_AreUnique()
    {
        // Arrange
        var codes = CollectCodes();

        // Act
        var duplicates = codes.GroupBy(code => code).Where(group => group.Count() > 1).Select(group => group.Key);

        // Assert
        Assert.Empty(duplicates);
    }

    [Fact(DisplayName = "Todo código segue o formato <área>.<conceito>.<invariante> em camelCase")]
    public void Codes_FollowNamingFormat()
    {
        // Arrange
        var codes = CollectCodes();

        // Act
        var invalid = codes.Where(code => !CodeFormat().IsMatch(code));

        // Assert
        Assert.Empty(invalid);
    }

    [Fact(DisplayName = "Os códigos existentes são exatamente os registrados no teste")]
    public void Codes_MatchRegisteredList()
    {
        // Arrange
        var codes = CollectCodes();

        // Assert
        Assert.Equal(RegisteredCodes.Order(), codes.Order());
    }
}
