using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Lessons;

namespace Estudarias.Domain.UnitTests.Lessons;

public class LessonTests
{
    private static readonly Guid TopicId = Guid.CreateVersion7();

    private const string Title = "Soma de frações com denominadores diferentes";
    private const string Content = "Para somar frações, primeiro deixe os denominadores iguais.";

    private static Lesson CreateWithTitle(string title) => Lesson.Create(TopicId, 1, title, Content);

    [Fact(DisplayName = "Uma lição nasce com o tópico, a ordem, o título (sem espaços nas pontas) e o conteúdo informados")]
    public void Create_WithValidData_KeepsValues()
    {
        // Act
        var lesson = Lesson.Create(TopicId, 2, $"  {Title}  ", Content);

        // Assert
        Assert.Equal(TopicId, lesson.TopicId);
        Assert.Equal(2, lesson.Order);
        Assert.Equal(Title, lesson.Title);
        Assert.Equal(Content, lesson.Content);
    }

    [Fact(DisplayName = "O conteúdo é guardado como veio, sem remover os espaços e as quebras das pontas")]
    public void Create_KeepsContentAsGiven()
    {
        // Arrange
        var content = "\n  1/2 + 1/3 = 3/6 + 2/6 = 5/6\n\n";

        // Act
        var lesson = Lesson.Create(TopicId, 1, Title, content);

        // Assert
        Assert.Equal(content, lesson.Content);
    }

    [Fact(DisplayName = "O tópico da lição é obrigatório")]
    public void Create_WithEmptyTopic_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Lesson.Create(Guid.Empty, 1, Title, Content));
        Assert.Equal("lesson.topic.required", exception.Code);
    }

    [Theory(DisplayName = "A ordem da lição começa em 1")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_WithOrderBelowOne_ThrowsDomainValidationException(int order)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Lesson.Create(TopicId, order, Title, Content));
        Assert.Equal("lesson.order.invalid", exception.Code);
    }

    [Fact(DisplayName = "A ordem 1 é aceita")]
    public void Create_WithOrderOne_Succeeds()
    {
        // Act
        var lesson = Lesson.Create(TopicId, 1, Title, Content);

        // Assert
        Assert.Equal(1, lesson.Order);
    }

    [Theory(DisplayName = "O título da lição é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithBlankTitle_ThrowsDomainValidationException(string? title)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => CreateWithTitle(title!));
        Assert.Equal("lesson.title.required", exception.Code);
    }

    [Theory(DisplayName = "Títulos de até 150 caracteres são aceitos")]
    [InlineData(100)]
    [InlineData(150)]
    public void Create_WithTitleWithinLimit_Succeeds(int length)
    {
        // Arrange
        var title = new string('a', length);

        // Act
        var lesson = CreateWithTitle(title);

        // Assert
        Assert.Equal(title, lesson.Title);
    }

    [Fact(DisplayName = "O título da lição não passa de 150 caracteres")]
    public void Create_WithTitleLongerThanLimit_ThrowsDomainValidationException()
    {
        // Arrange
        var title = new string('a', 151);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() => CreateWithTitle(title));
        Assert.Equal("lesson.title.tooLong", exception.Code);
    }

    [Fact(DisplayName = "O limite do título vale depois de remover os espaços das pontas")]
    public void Create_WithTitleAtLimitPlusPadding_Succeeds()
    {
        // Arrange
        var title = new string('a', 150);

        // Act
        var lesson = CreateWithTitle($"  {title}  ");

        // Assert
        Assert.Equal(title, lesson.Title);
    }

    [Theory(DisplayName = "O conteúdo da lição é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void Create_WithBlankContent_ThrowsDomainValidationException(string? content)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Lesson.Create(TopicId, 1, Title, content!));
        Assert.Equal("lesson.content.required", exception.Code);
    }

    [Fact(DisplayName = "Cada lição nasce com um id próprio")]
    public void Create_AssignsDistinctIds()
    {
        // Act
        var first = CreateWithTitle(Title);
        var second = CreateWithTitle(Title);

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact(DisplayName = "Revisar troca o título e o conteúdo, e mantém o tópico e a ordem")]
    public void Revise_ChangesTheContentAndKeepsTheKey()
    {
        // Arrange
        var lesson = Lesson.Create(TopicId, 2, Title, Content);

        // Act
        lesson.Revise("  Título novo  ", "Conteúdo novo");

        // Assert
        Assert.Equal((TopicId, 2, "Título novo", "Conteúdo novo"), (lesson.TopicId, lesson.Order, lesson.Title, lesson.Content));
    }

    [Fact(DisplayName = "Revisar aplica as regras da criação, e nada muda quando falha")]
    public void Revise_WithoutContent_ThrowsAndChangesNothing()
    {
        // Arrange
        var lesson = Lesson.Create(TopicId, 1, Title, Content);

        // Act
        var exception = Assert.Throws<DomainValidationException>(() => lesson.Revise("Outro", " "));

        // Assert
        Assert.Equal("lesson.content.required", exception.Code);
        Assert.Equal(Title, lesson.Title);
    }
}
