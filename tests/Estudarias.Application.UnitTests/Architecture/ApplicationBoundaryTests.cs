using System.Reflection;
using Estudarias.Application;
using Estudarias.Application.Common;
using NetArchTest.Rules;

namespace Estudarias.Application.UnitTests.Architecture;

/// <summary>
/// A fronteira da Application depois do ADR-002: ela referencia o EF Core só para materializar leituras, então o compilador deixa de
/// impedir que um caso de uso escreva pelo contexto. Estes testes devolvem essa barreira: só as classes <c>*Reads</c> enxergam o
/// <c>IApplicationDbContext</c> e o EF Core. A escrita segue pelos repositórios e pela unidade de trabalho.
/// </summary>
public class ApplicationBoundaryTests
{
    private const string EntityFramework = "Microsoft.EntityFrameworkCore";

    private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

    private static readonly string ReadContract = typeof(IApplicationDbContext).FullName!;

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        $"Tipos que violam a regra: {string.Join(", ", result.FailingTypeNames ?? [])}";

    [Fact(DisplayName = "Só as classes *Reads da Application usam o EF Core")]
    public void Application_OutsideReads_DoesNotDependOnEntityFramework()
    {
        // Arrange
        var rule = Types.InAssembly(ApplicationAssembly)
            .That().DoNotHaveNameEndingWith("Reads")
            .ShouldNot().HaveDependencyOn(EntityFramework);

        // Act
        var result = rule.GetResult();

        // Assert
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact(DisplayName = "Só as classes *Reads da Application usam o IApplicationDbContext")]
    public void Application_OutsideReads_DoesNotDependOnTheReadContext()
    {
        // Arrange
        var rule = Types.InAssembly(ApplicationAssembly)
            .That().DoNotHaveNameEndingWith("Reads").And().DoNotHaveName(nameof(IApplicationDbContext))
            .ShouldNot().HaveDependencyOn(ReadContract);

        // Act
        var result = rule.GetResult();

        // Assert
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact(DisplayName = "As regras enxergam as classes *Reads: elas usam mesmo o contexto de leitura e o EF Core")]
    public void Reads_DoUseTheReadContextAndEntityFramework()
    {
        // Arrange: o controle que impede as duas regras acima de passarem no vazio. Se a análise não visse dependência nenhuma, elas
        // passariam sempre; aqui ela tem de achá-las nas classes que as usam de fato.
        var readClasses = Types.InAssembly(ApplicationAssembly).That().HaveNameEndingWith("Reads").And().AreClasses();

        // Act
        var usingContext = readClasses.Should().HaveDependencyOn(ReadContract).GetResult();
        var usingEntityFramework = readClasses.Should().HaveDependencyOn(EntityFramework).GetResult();

        // Assert
        Assert.True(usingContext.IsSuccessful, Describe(usingContext));
        Assert.True(usingEntityFramework.IsSuccessful, Describe(usingEntityFramework));
    }
}
