using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Mastery;
using Estudarias.Domain.Scoring;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;
using Microsoft.Extensions.Time.Testing;

namespace Estudarias.Domain.UnitTests.Mastery;

public class TopicMasteryTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static readonly TopicScore Perfect = TopicScore.Compute([new GradedAnswer(Difficulty.Medium, true)]);
    private static readonly TopicScore Failing = TopicScore.Zero;

    // Exatamente 70 não sai de nenhuma sequência de respostas (o decaimento de 0,9 impede), então as bordas usam as
    // notas reais mais próximas de cada lado.
    private static readonly TopicScore JustAboveThreshold = TopicScore.Compute(
    [
        new(Difficulty.Hard, false), new(Difficulty.Medium, true), new(Difficulty.Hard, true),
        new(Difficulty.Medium, true), new(Difficulty.Medium, true),
    ]);

    private static readonly TopicScore JustBelowThreshold = TopicScore.Compute(
    [
        new(Difficulty.Medium, false), new(Difficulty.Medium, true), new(Difficulty.Medium, true),
        new(Difficulty.Hard, true), new(Difficulty.Easy, false), new(Difficulty.Easy, true),
    ]);

    // Trilha da v1: Operações básicas → Frações → (Equações do 1º grau, Porcentagem); Porcentagem também exige Equações
    // neste teste, para ter um dependente com dois pré-requisitos.
    private readonly Topic _basics = Topic.Create("Operações básicas", "operacoes-basicas");
    private readonly Topic _fractions = Topic.Create("Frações", "fracoes");
    private readonly Topic _equations = Topic.Create("Equações do 1º grau", "equacoes-1-grau");
    private readonly Topic _percentage = Topic.Create("Porcentagem", "porcentagem");

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly Student _student = Student.Create("Ana", ExperimentalGroup.Diagnostic);

    public TopicMasteryTests()
    {
        _fractions.AddPrerequisite(_basics.Id);
        _equations.AddPrerequisite(_fractions.Id);
        _percentage.AddPrerequisite(_fractions.Id);
        _percentage.AddPrerequisite(_equations.Id);
    }

    private IReadOnlyCollection<Topic> Trail => [_basics, _fractions, _equations, _percentage];

    [Fact(DisplayName = "As notas de borda dos testes ficam de cada lado de 70")]
    public void BorderScores_AreOnEachSideOfThreshold()
    {
        // Assert
        Assert.InRange(JustAboveThreshold.Value, 70m, 70.001m);
        Assert.InRange(JustBelowThreshold.Value, 69.999m, 69.9999999m);
    }

    [Fact(DisplayName = "Nota logo acima de 70 com 5 tentativas domina o tópico")]
    public void IsMastered_JustAboveThresholdWithMinAttempts_IsTrue()
    {
        // Act & Assert
        Assert.True(TopicMastery.IsMastered(JustAboveThreshold, 5));
    }

    [Fact(DisplayName = "Nota logo abaixo de 70 não domina o tópico, mesmo com muitas tentativas")]
    public void IsMastered_JustBelowThreshold_IsFalse()
    {
        // Act & Assert
        Assert.False(TopicMastery.IsMastered(JustBelowThreshold, 50));
    }

    [Fact(DisplayName = "Com só 4 tentativas o tópico não é dominado, mesmo com nota 100")]
    public void IsMastered_WithFourAttempts_IsFalse()
    {
        // Act & Assert
        Assert.False(TopicMastery.IsMastered(Perfect, 4));
    }

    [Fact(DisplayName = "Uma contagem de tentativas negativa lança ArgumentOutOfRangeException")]
    public void IsMastered_WithNegativeAttempts_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => TopicMastery.IsMastered(Perfect, -1));
    }

    [Fact(DisplayName = "Liberar o início abre só os tópicos sem pré-requisito, com a data do relógio")]
    public void UnlockInitial_UnlocksOnlyRoots()
    {
        // Act
        var unlocked = TopicMastery.UnlockInitial(_student, Trail, _clock);

        // Assert
        Assert.Equal([_basics.Id], unlocked);
        Assert.Equal(TopicStatus.Available, _student.StatusOf(_basics.Id));
        Assert.Equal(TopicStatus.Locked, _student.StatusOf(_fractions.Id));
        Assert.Equal(Start, Assert.Single(_student.Progress).UnlockedAt);
    }

    [Fact(DisplayName = "Liberar o início de novo não libera nada nem muda a data")]
    public void UnlockInitial_Twice_IsIdempotent()
    {
        // Arrange
        TopicMastery.UnlockInitial(_student, Trail, _clock);
        _clock.Advance(TimeSpan.FromDays(1));

        // Act
        var unlocked = TopicMastery.UnlockInitial(_student, Trail, _clock);

        // Assert
        Assert.Empty(unlocked);
        Assert.Equal(Start, Assert.Single(_student.Progress).UnlockedAt);
    }

    [Fact(DisplayName = "Avaliar um tópico bloqueado lança student.topic.locked")]
    public void Evaluate_LockedTopic_ThrowsDomainValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(
            () => TopicMastery.Evaluate(_student, _fractions.Id, Perfect, 10, Trail, _clock));
        Assert.Equal("student.topic.locked", exception.Code);
    }

    [Fact(DisplayName = "Abaixo do limiar, avaliar não marca o domínio nem libera nada")]
    public void Evaluate_BelowThreshold_ChangesNothing()
    {
        // Arrange
        TopicMastery.UnlockInitial(_student, Trail, _clock);

        // Act
        var unlocked = TopicMastery.Evaluate(_student, _basics.Id, JustBelowThreshold, 10, Trail, _clock);

        // Assert
        Assert.Empty(unlocked);
        Assert.Equal(TopicStatus.Available, _student.StatusOf(_basics.Id));
        Assert.Equal(TopicStatus.Locked, _student.StatusOf(_fractions.Id));
    }

    [Fact(DisplayName = "Ao dominar, o tópico ganha a data de domínio e o dependente de um único pré-requisito é liberado")]
    public void Evaluate_WhenMastered_MarksDateAndUnlocksSingleRequisiteDependent()
    {
        // Arrange
        TopicMastery.UnlockInitial(_student, Trail, _clock);
        _clock.Advance(TimeSpan.FromDays(2));

        // Act
        var unlocked = TopicMastery.Evaluate(_student, _basics.Id, JustAboveThreshold, 5, Trail, _clock);

        // Assert
        Assert.Equal([_fractions.Id], unlocked);
        Assert.Equal(TopicStatus.Mastered, _student.StatusOf(_basics.Id));
        Assert.Equal(TopicStatus.Available, _student.StatusOf(_fractions.Id));
        var basics = _student.Progress.Single(progress => progress.TopicId == _basics.Id);
        var fractions = _student.Progress.Single(progress => progress.TopicId == _fractions.Id);
        Assert.Equal(Start.AddDays(2), basics.MasteredAt);
        Assert.Equal(Start.AddDays(2), fractions.UnlockedAt);
    }

    [Fact(DisplayName = "Um dependente com dois pré-requisitos só é liberado quando os dois estão dominados")]
    public void Evaluate_DependentWithTwoRequisites_WaitsForBoth()
    {
        // Arrange
        TopicMastery.UnlockInitial(_student, Trail, _clock);
        TopicMastery.Evaluate(_student, _basics.Id, Perfect, 5, Trail, _clock);

        // Act
        var afterFractions = TopicMastery.Evaluate(_student, _fractions.Id, Perfect, 5, Trail, _clock);
        var afterEquations = TopicMastery.Evaluate(_student, _equations.Id, Perfect, 5, Trail, _clock);

        // Assert
        Assert.Equal([_equations.Id], afterFractions);
        Assert.Equal([_percentage.Id], afterEquations);
        Assert.Equal(TopicStatus.Available, _student.StatusOf(_percentage.Id));
    }

    [Fact(DisplayName = "A nota caindo depois não desfaz o domínio nem a liberação")]
    public void Evaluate_ScoreDropsLater_KeepsMasteryAndUnlocks()
    {
        // Arrange
        TopicMastery.UnlockInitial(_student, Trail, _clock);
        TopicMastery.Evaluate(_student, _basics.Id, Perfect, 5, Trail, _clock);
        var masteredAt = _student.Progress.Single(progress => progress.TopicId == _basics.Id).MasteredAt;
        _clock.Advance(TimeSpan.FromDays(3));

        // Act
        var unlocked = TopicMastery.Evaluate(_student, _basics.Id, Failing, 12, Trail, _clock);

        // Assert
        Assert.Empty(unlocked);
        Assert.Equal(TopicStatus.Mastered, _student.StatusOf(_basics.Id));
        Assert.Equal(masteredAt, _student.Progress.Single(progress => progress.TopicId == _basics.Id).MasteredAt);
        Assert.Equal(TopicStatus.Available, _student.StatusOf(_fractions.Id));
    }

    [Fact(DisplayName = "A lista devolvida tem só os tópicos liberados nesta chamada")]
    public void Evaluate_ReturnsOnlyTopicsUnlockedNow()
    {
        // Arrange
        TopicMastery.UnlockInitial(_student, Trail, _clock);
        TopicMastery.Evaluate(_student, _basics.Id, Perfect, 5, Trail, _clock);

        // Act
        var again = TopicMastery.Evaluate(_student, _basics.Id, Perfect, 6, Trail, _clock);

        // Assert
        Assert.Empty(again);
    }

    [Fact(DisplayName = "Um tópico novo na trilha é liberado na próxima avaliação do pré-requisito já dominado")]
    public void Evaluate_AlreadyMastered_UnlocksDependentAddedLater()
    {
        // Arrange
        TopicMastery.UnlockInitial(_student, Trail, _clock);
        TopicMastery.Evaluate(_student, _basics.Id, Perfect, 5, Trail, _clock);
        var geometry = Topic.Create("Geometria básica", "geometria-basica");
        geometry.AddPrerequisite(_basics.Id);

        // Act
        var unlocked = TopicMastery.Evaluate(_student, _basics.Id, Failing, 6, [.. Trail, geometry], _clock);

        // Assert
        Assert.Equal([geometry.Id], unlocked);
    }

    [Fact(DisplayName = "Uma contagem de tentativas negativa na avaliação lança ArgumentOutOfRangeException")]
    public void Evaluate_WithNegativeAttempts_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        TopicMastery.UnlockInitial(_student, Trail, _clock);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TopicMastery.Evaluate(_student, _basics.Id, Perfect, -1, Trail, _clock));
    }
}
