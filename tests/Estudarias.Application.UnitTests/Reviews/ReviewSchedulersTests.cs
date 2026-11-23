using Estudarias.Application.Reviews;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using NSubstitute;

namespace Estudarias.Application.UnitTests.Reviews;

public class ReviewSchedulersTests
{
    private static IReviewScheduler Scheduler(string algorithm)
    {
        var scheduler = Substitute.For<IReviewScheduler>();
        scheduler.Algorithm.Returns(algorithm);
        return scheduler;
    }

    [Fact(DisplayName = "Acha o agendador pelo algoritmo")]
    public void For_FindsTheSchedulerByAlgorithm()
    {
        // Arrange
        var sm2 = Scheduler("sm2");
        var fsrs = Scheduler("fsrs");
        var schedulers = new ReviewSchedulers([sm2, fsrs]);

        // Act & Assert
        Assert.Same(sm2, schedulers.For("sm2"));
        Assert.Same(fsrs, schedulers.For("fsrs"));
    }

    [Fact(DisplayName = "RN-REV-13: um algoritmo sem agendador registrado é erro de configuração")]
    public void For_AnUnregisteredAlgorithm_Throws()
    {
        // Arrange
        var schedulers = new ReviewSchedulers([Scheduler("sm2")]);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => schedulers.For("fsrs"));
    }

    [Fact(DisplayName = "Dois agendadores do mesmo algoritmo são erro de configuração")]
    public void Constructor_WithTwoSchedulersForTheSameAlgorithm_Throws()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ReviewSchedulers([Scheduler("sm2"), Scheduler("sm2")]));
    }

    [Fact(DisplayName = "O Sm2Scheduler real é o agendador do algoritmo sm2")]
    public void For_Sm2_IsTheRealAdapter()
    {
        // Arrange
        var schedulers = new ReviewSchedulers([new Sm2Scheduler()]);

        // Act & Assert
        Assert.IsType<Sm2Scheduler>(schedulers.For(Sm2Scheduler.AlgorithmName));
    }
}
