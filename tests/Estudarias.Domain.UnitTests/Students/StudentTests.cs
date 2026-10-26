using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Students;
using Microsoft.Extensions.Time.Testing;

namespace Estudarias.Domain.UnitTests.Students;

public class StudentTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    private static Student NewStudent() => Student.Create("Ana", ExperimentalGroup.Diagnostic);

    [Fact(DisplayName = "Um aluno nasce com o nome (sem espaços nas pontas) e o grupo informados")]
    public void Create_WithValidData_KeepsTrimmedNameAndGroup()
    {
        // Act
        var student = Student.Create("  Ana  ", ExperimentalGroup.Control);

        // Assert
        Assert.Equal("Ana", student.Name);
        Assert.Equal(ExperimentalGroup.Control, student.Group);
    }

    [Fact(DisplayName = "Um aluno nasce sem nenhum progresso")]
    public void Create_StartsWithoutProgress()
    {
        // Act
        var student = NewStudent();

        // Assert
        Assert.Empty(student.Progress);
    }

    [Theory(DisplayName = "O nome do aluno é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithBlankName_ThrowsDomainValidationException(string? name)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Student.Create(name!, ExperimentalGroup.Control));
        Assert.Equal("student.name.required", exception.Code);
    }

    [Fact(DisplayName = "O nome do aluno não passa de 100 caracteres")]
    public void Create_WithNameLongerThanLimit_ThrowsDomainValidationException()
    {
        // Arrange
        var name = new string('a', 101);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Student.Create(name, ExperimentalGroup.Control));
        Assert.Equal("student.name.tooLong", exception.Code);
    }

    [Fact(DisplayName = "O limite do nome vale depois de remover os espaços das pontas")]
    public void Create_WithNameAtLimitPlusPadding_Succeeds()
    {
        // Arrange
        var name = new string('a', 100);

        // Act
        var student = Student.Create($"  {name}  ", ExperimentalGroup.Control);

        // Assert
        Assert.Equal(name, student.Name);
    }

    [Fact(DisplayName = "O grupo experimental precisa ser um valor conhecido")]
    public void Create_WithUndefinedGroup_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => Student.Create("Ana", (ExperimentalGroup)99));
        Assert.Equal("student.group.invalid", exception.Code);
    }

    [Fact(DisplayName = "Um tópico sem registro de progresso está bloqueado")]
    public void StatusOf_UntouchedTopic_ReturnsLocked()
    {
        // Arrange
        var student = NewStudent();

        // Act
        var status = student.StatusOf(Guid.CreateVersion7());

        // Assert
        Assert.Equal(TopicStatus.Locked, status);
    }

    [Fact(DisplayName = "Desbloquear um tópico o deixa disponível e grava a data")]
    public void Unlock_MakesTopicAvailableAndRecordsDate()
    {
        // Arrange
        var student = NewStudent();
        var topicId = Guid.CreateVersion7();
        var clock = new FakeTimeProvider(Start);

        // Act
        student.Unlock(topicId, clock);

        // Assert
        var progress = Assert.Single(student.Progress);
        Assert.Equal(topicId, progress.TopicId);
        Assert.Equal(TopicStatus.Available, student.StatusOf(topicId));
        Assert.Equal(Start, progress.UnlockedAt);
        Assert.Null(progress.MasteredAt);
    }

    [Fact(DisplayName = "Desbloquear de novo não cria outro registro nem muda a data")]
    public void Unlock_Twice_KeepsSingleRecordAndFirstDate()
    {
        // Arrange
        var student = NewStudent();
        var topicId = Guid.CreateVersion7();
        var clock = new FakeTimeProvider(Start);
        student.Unlock(topicId, clock);
        clock.Advance(TimeSpan.FromDays(1));

        // Act
        student.Unlock(topicId, clock);

        // Assert
        var progress = Assert.Single(student.Progress);
        Assert.Equal(Start, progress.UnlockedAt);
    }

    [Fact(DisplayName = "Desbloquear um tópico não mexe nos outros")]
    public void Unlock_DoesNotAffectOtherTopics()
    {
        // Arrange
        var student = NewStudent();
        var clock = new FakeTimeProvider(Start);

        // Act
        student.Unlock(Guid.CreateVersion7(), clock);

        // Assert
        Assert.Equal(TopicStatus.Locked, student.StatusOf(Guid.CreateVersion7()));
    }

    [Fact(DisplayName = "Dominar um tópico desbloqueado grava a data e mantém a do desbloqueio")]
    public void MarkMastered_UnlockedTopic_RecordsDateAndKeepsUnlockDate()
    {
        // Arrange
        var student = NewStudent();
        var topicId = Guid.CreateVersion7();
        var clock = new FakeTimeProvider(Start);
        student.Unlock(topicId, clock);
        clock.Advance(TimeSpan.FromDays(2));

        // Act
        student.MarkMastered(topicId, clock);

        // Assert
        var progress = Assert.Single(student.Progress);
        Assert.Equal(TopicStatus.Mastered, student.StatusOf(topicId));
        Assert.Equal(Start, progress.UnlockedAt);
        Assert.Equal(Start.AddDays(2), progress.MasteredAt);
    }

    [Fact(DisplayName = "Dominar de novo mantém a primeira data de domínio")]
    public void MarkMastered_Twice_KeepsFirstMasteredAt()
    {
        // Arrange
        var student = NewStudent();
        var topicId = Guid.CreateVersion7();
        var clock = new FakeTimeProvider(Start);
        student.Unlock(topicId, clock);
        clock.Advance(TimeSpan.FromDays(2));
        student.MarkMastered(topicId, clock);
        clock.Advance(TimeSpan.FromDays(3));

        // Act
        student.MarkMastered(topicId, clock);

        // Assert
        var progress = Assert.Single(student.Progress);
        Assert.Equal(Start.AddDays(2), progress.MasteredAt);
    }

    [Fact(DisplayName = "Não se domina um tópico que ainda está bloqueado")]
    public void MarkMastered_LockedTopic_ThrowsDomainValidationException()
    {
        // Arrange
        var student = NewStudent();
        var clock = new FakeTimeProvider(Start);

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => student.MarkMastered(Guid.CreateVersion7(), clock));
        Assert.Equal("student.topic.locked", exception.Code);
        Assert.Empty(student.Progress);
    }

    [Fact(DisplayName = "Desbloquear um tópico já dominado não o faz voltar a disponível")]
    public void Unlock_AfterMastered_KeepsTopicMastered()
    {
        // Arrange
        var student = NewStudent();
        var topicId = Guid.CreateVersion7();
        var clock = new FakeTimeProvider(Start);
        student.Unlock(topicId, clock);
        student.MarkMastered(topicId, clock);

        // Act
        student.Unlock(topicId, clock);

        // Assert
        Assert.Equal(TopicStatus.Mastered, student.StatusOf(topicId));
    }
}
