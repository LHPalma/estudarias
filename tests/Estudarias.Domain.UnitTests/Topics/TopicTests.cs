using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Topics;

namespace Estudarias.Domain.UnitTests.Topics;

public class TopicTests
{
    private static Topic NewTopic() => Topic.Create("Frações", "fracoes");

    [Fact(DisplayName = "Um tópico nasce com o nome (sem espaços nas pontas) e o slug informados")]
    public void Create_WithValidData_KeepsTrimmedNameAndSlug()
    {
        // Act
        var topic = Topic.Create("  Equações do 1º grau  ", "equacoes-1-grau");

        // Assert
        Assert.Equal("Equações do 1º grau", topic.Name);
        Assert.Equal("equacoes-1-grau", topic.Slug);
    }

    [Fact(DisplayName = "Um tópico nasce sem nenhum pré-requisito")]
    public void Create_StartsWithoutPrerequisites()
    {
        // Act
        var topic = NewTopic();

        // Assert
        Assert.Empty(topic.Prerequisites);
    }

    [Theory(DisplayName = "O nome do tópico é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithBlankName_ThrowsDomainValidationException(string? name)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Topic.Create(name!, "fracoes"));
        Assert.Equal("topic.name.required", exception.Code);
    }

    [Fact(DisplayName = "O nome do tópico não passa de 100 caracteres")]
    public void Create_WithNameLongerThanLimit_ThrowsDomainValidationException()
    {
        // Arrange
        var name = new string('a', 101);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Topic.Create(name, "fracoes"));
        Assert.Equal("topic.name.tooLong", exception.Code);
    }

    [Fact(DisplayName = "O limite do nome vale depois de remover os espaços das pontas")]
    public void Create_WithNameAtLimitPlusPadding_Succeeds()
    {
        // Arrange
        var name = new string('a', 100);

        // Act
        var topic = Topic.Create($"  {name}  ", "fracoes");

        // Assert
        Assert.Equal(name, topic.Name);
    }

    [Theory(DisplayName = "O slug do tópico é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankSlug_ThrowsDomainValidationException(string? slug)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Topic.Create("Frações", slug!));
        Assert.Equal("topic.slug.required", exception.Code);
    }

    [Theory(DisplayName = "O slug precisa ter só minúsculas, dígitos e hífens entre grupos")]
    [InlineData("Fracoes")]
    [InlineData("equacoes 1 grau")]
    [InlineData(" fracoes")]
    [InlineData("fracoes ")]
    [InlineData("fracoes\n")]
    [InlineData("-fracoes")]
    [InlineData("fracoes-")]
    [InlineData("equacoes--1-grau")]
    [InlineData("equações")]
    [InlineData("equacoes_1_grau")]
    public void Create_WithMalformedSlug_ThrowsDomainValidationException(string slug)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Topic.Create("Frações", slug));
        Assert.Equal("topic.slug.invalid", exception.Code);
    }

    [Fact(DisplayName = "O slug do tópico não passa de 100 caracteres")]
    public void Create_WithSlugLongerThanLimit_ThrowsDomainValidationException()
    {
        // Arrange
        var slug = new string('a', 101);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => Topic.Create("Frações", slug));
        Assert.Equal("topic.slug.invalid", exception.Code);
    }

    [Fact(DisplayName = "Um slug com exatamente 100 caracteres é aceito")]
    public void Create_WithSlugAtLimit_Succeeds()
    {
        // Arrange
        var slug = new string('a', 100);

        // Act
        var topic = Topic.Create("Frações", slug);

        // Assert
        Assert.Equal(slug, topic.Slug);
    }

    [Fact(DisplayName = "Adicionar um pré-requisito o guarda pelo id")]
    public void AddPrerequisite_StoresRequisiteId()
    {
        // Arrange
        var topic = NewTopic();
        var requisiteId = Guid.CreateVersion7();

        // Act
        topic.AddPrerequisite(requisiteId);

        // Assert
        var stored = Assert.Single(topic.Prerequisites);
        Assert.Equal(requisiteId, stored);
    }

    [Fact(DisplayName = "Um tópico aceita vários pré-requisitos")]
    public void AddPrerequisite_AcceptsMany()
    {
        // Arrange
        var topic = NewTopic();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        // Act
        topic.AddPrerequisite(first);
        topic.AddPrerequisite(second);

        // Assert
        Assert.Equal([first, second], topic.Prerequisites);
    }

    [Fact(DisplayName = "Um tópico não é pré-requisito de si mesmo")]
    public void AddPrerequisite_Self_ThrowsDomainValidationException()
    {
        // Arrange
        var topic = NewTopic();

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => topic.AddPrerequisite(topic.Id));
        Assert.Equal("topic.prerequisite.self", exception.Code);
        Assert.Empty(topic.Prerequisites);
    }

    [Fact(DisplayName = "O mesmo pré-requisito não entra duas vezes")]
    public void AddPrerequisite_Duplicated_ThrowsDomainValidationException()
    {
        // Arrange
        var topic = NewTopic();
        var requisiteId = Guid.CreateVersion7();
        topic.AddPrerequisite(requisiteId);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => topic.AddPrerequisite(requisiteId));
        Assert.Equal("topic.prerequisite.duplicated", exception.Code);
        Assert.Single(topic.Prerequisites);
    }
}
