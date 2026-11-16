using Estudarias.Domain.Attempts;
using Estudarias.Domain.Common.Exceptions;
using Microsoft.Extensions.Time.Testing;

namespace Estudarias.Domain.UnitTests.Attempts;

public class AttemptTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid StudentId = Guid.CreateVersion7();
    private static readonly Guid ExerciseId = Guid.CreateVersion7();
    private static readonly Guid AlternativeId = Guid.CreateVersion7();

    [Fact(DisplayName = "Uma tentativa nasce com o aluno, o exercício, a alternativa, o tempo de resposta e a data do relógio")]
    public void Create_WithValidData_KeepsValues()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);

        // Act
        var attempt = Attempt.Create(StudentId, ExerciseId, AlternativeId, 42, clock);

        // Assert
        Assert.Equal(StudentId, attempt.StudentId);
        Assert.Equal(ExerciseId, attempt.ExerciseId);
        Assert.Equal(AlternativeId, attempt.AlternativeId);
        Assert.Equal(42, attempt.ResponseSeconds);
        Assert.Equal(Start, attempt.AnsweredAt);
    }

    [Fact(DisplayName = "A data da tentativa é o instante do relógio no momento da criação")]
    public void Create_TakesAnsweredAtFromClock()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var attempt = Attempt.Create(StudentId, ExerciseId, AlternativeId, 10, clock);

        // Assert
        Assert.Equal(Start.AddMinutes(5), attempt.AnsweredAt);
    }

    [Fact(DisplayName = "O tempo de resposta pode ser omitido")]
    public void Create_WithoutResponseTime_StoresNull()
    {
        // Act
        var attempt = Attempt.Create(StudentId, ExerciseId, AlternativeId, null, new FakeTimeProvider(Start));

        // Assert
        Assert.Null(attempt.ResponseSeconds);
    }

    [Fact(DisplayName = "Um tempo de resposta zero é aceito")]
    public void Create_WithZeroResponseTime_Succeeds()
    {
        // Act
        var attempt = Attempt.Create(StudentId, ExerciseId, AlternativeId, 0, new FakeTimeProvider(Start));

        // Assert
        Assert.Equal(0, attempt.ResponseSeconds);
    }

    [Theory(DisplayName = "O tempo de resposta não pode ser negativo")]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_WithNegativeResponseTime_ThrowsDomainValidationException(int responseSeconds)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Attempt.Create(StudentId, ExerciseId, AlternativeId, responseSeconds, new FakeTimeProvider(Start)));
        Assert.Equal("attempt.responseTime.invalid", exception.Code);
    }

    [Fact(DisplayName = "O aluno da tentativa é obrigatório")]
    public void Create_WithEmptyStudent_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Attempt.Create(Guid.Empty, ExerciseId, AlternativeId, 10, new FakeTimeProvider(Start)));
        Assert.Equal("attempt.student.required", exception.Code);
    }

    [Fact(DisplayName = "O exercício da tentativa é obrigatório")]
    public void Create_WithEmptyExercise_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Attempt.Create(StudentId, Guid.Empty, AlternativeId, 10, new FakeTimeProvider(Start)));
        Assert.Equal("attempt.exercise.required", exception.Code);
    }

    [Fact(DisplayName = "A alternativa da tentativa é obrigatória")]
    public void Create_WithEmptyAlternative_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Attempt.Create(StudentId, ExerciseId, Guid.Empty, 10, new FakeTimeProvider(Start)));
        Assert.Equal("attempt.alternative.required", exception.Code);
    }

    [Fact(DisplayName = "Cada tentativa nasce com um id próprio")]
    public void Create_AssignsDistinctIds()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);

        // Act
        var first = Attempt.Create(StudentId, ExerciseId, AlternativeId, 10, clock);
        var second = Attempt.Create(StudentId, ExerciseId, AlternativeId, 10, clock);

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }
}
