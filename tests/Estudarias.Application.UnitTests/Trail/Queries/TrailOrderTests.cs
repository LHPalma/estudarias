using Estudarias.Application.Trail.Queries;

namespace Estudarias.Application.UnitTests.Trail.Queries;

public class TrailOrderTests
{
    private sealed record Node(Guid Id, string Name, params Guid[] Requisites);

    private static Node Root(string name) => new(Guid.CreateVersion7(), name);

    private static Node After(string name, params Node[] requisites) =>
        new(Guid.CreateVersion7(), name, [.. requisites.Select(requisite => requisite.Id)]);

    private static string[] Sort(params Node[] nodes) =>
        [.. TrailOrder.Sort(nodes, node => node.Id, node => node.Name, node => node.Requisites).Select(node => node.Name)];

    [Fact(DisplayName = "Uma cadeia de pré-requisitos sai da raiz para o fim, qualquer que seja a ordem de entrada")]
    public void Sort_PutsEachTopicAfterItsPrerequisites()
    {
        // Arrange
        var basics = Root("Operações básicas");
        var fractions = After("Frações", basics);
        var equations = After("Equações do 1º grau", fractions);

        // Act
        var order = Sort(equations, basics, fractions);

        // Assert
        Assert.Equal(["Operações básicas", "Frações", "Equações do 1º grau"], order);
    }

    [Fact(DisplayName = "Na mesma profundidade, a ordem é alfabética, sem diferenciar maiúsculas e acentos de caixa")]
    public void Sort_BreaksTiesByName()
    {
        // Arrange
        var basics = Root("Operações básicas");
        var geometry = Root("geometria");
        var algebra = Root("Álgebra");

        // Act
        var order = Sort(geometry, basics, algebra);

        // Assert
        Assert.Equal(["Álgebra", "geometria", "Operações básicas"], order);
    }

    [Fact(DisplayName = "Um tópico com dois pré-requisitos vem depois do mais profundo deles")]
    public void Sort_UsesTheDeepestPrerequisite()
    {
        // Arrange
        var a = Root("A");
        var b = After("B", a);
        var c = After("C", a, b);
        var d = Root("D");

        // Act
        var order = Sort(c, d, b, a);

        // Assert
        Assert.Equal(["A", "D", "B", "C"], order);
    }

    [Fact(DisplayName = "Um pré-requisito que não está na lista é ignorado")]
    public void Sort_IgnoresAnUnknownPrerequisite()
    {
        // Arrange
        var orphan = new Node(Guid.CreateVersion7(), "Órfão", Guid.CreateVersion7());

        // Act
        var order = Sort(orphan);

        // Assert
        Assert.Equal(["Órfão"], order);
    }

    [Fact(DisplayName = "Sem tópicos, a ordem é vazia")]
    public void Sort_WithoutTopics_IsEmpty()
    {
        // Act & Assert
        Assert.Empty(Sort());
    }

    [Fact(DisplayName = "Um ciclo não trava a leitura nem perde tópico")]
    public void Sort_WithACycle_StillReturnsEveryTopic()
    {
        // Arrange
        var firstId = Guid.CreateVersion7();
        var secondId = Guid.CreateVersion7();
        var first = new Node(firstId, "Primeiro", secondId);
        var second = new Node(secondId, "Segundo", firstId);

        // Act
        var order = Sort(first, second);

        // Assert
        Assert.Equal(["Primeiro", "Segundo"], order.Order());
    }
}
