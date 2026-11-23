using Estudarias.Application.Reviews.Queries;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Identity;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.StudentFlashcards.Scheduling;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Estudarias.Infrastructure.IntegrationTests.Reviews;

/// <summary>A leitura das revisões do dia (#89) contra um PostgreSQL real: só os vencidos, na ordem, só do aluno e só as colunas do contrato.</summary>
[Collection(PostgresCollection.Name)]
public class ReviewReadsTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private static readonly Sm2Scheduler Scheduler = new();

    private async Task<DueReviewsView> DueAsync(Guid userId)
    {
        using var scope = NewScope();
        return await scope.Get<IReviewReads>().DueAsync(userId, Cancel);
    }

    /// <summary>Cria o cartão do aluno no instante atual do relógio: a primeira revisão é um dia depois.</summary>
    private async Task<StudentFlashcard> AddCardAsync(Guid studentId, Flashcard flashcard)
    {
        using var scope = NewScope();
        var card = StudentFlashcard.Create(studentId, flashcard.Id, null, Scheduler, Clock);
        scope.StudentFlashcards.Add(card);
        await scope.UnitOfWork.SaveChangesAsync(Cancel);
        return card;
    }

    private async Task<Flashcard> ExtraFlashcardAsync(SampleContent content, string code)
    {
        var flashcard = Flashcard.CreateManual(content.Fractions.Id, code, $"Pergunta {code}?", $"Resposta {code}.");
        await WriteAsync(db => db.Flashcards.Add(flashcard));
        return flashcard;
    }

    [Fact(DisplayName = "Sem nenhum cartão, a lista é vazia e o total é 0")]
    public async Task Due_WithoutCards_IsEmpty()
    {
        // Arrange
        await SeedContentAsync();
        var user = await SeedUserAsync();

        // Act
        var due = await DueAsync(user.Id);

        // Assert
        Assert.Equal(0, due.Count);
        Assert.Empty(due.Items);
    }

    [Fact(DisplayName = "Só os cartões vencidos entram, do mais atrasado ao mais recente, e o total os conta")]
    public async Task Due_ReturnsOnlyTheDueCardsMostOverdueFirst()
    {
        // Arrange: A vence 1 dia depois de criado, B 3 dias, C 5 dias; o relógio para 4,5 dias depois do início
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        var studentId = user.StudentId!.Value;
        var extra = await ExtraFlashcardAsync(content, "terceiro");
        var cardA = await AddCardAsync(studentId, content.Generated);
        Clock.Advance(TimeSpan.FromDays(2));
        var cardB = await AddCardAsync(studentId, content.Manual);
        Clock.Advance(TimeSpan.FromDays(2));
        var cardC = await AddCardAsync(studentId, extra);
        Clock.Advance(TimeSpan.FromHours(12));

        // Act
        var due = await DueAsync(user.Id);

        // Assert
        Assert.Equal(2, due.Count);
        Assert.Equal([cardA.Id, cardB.Id], due.Items.Select(item => item.Id));
        Assert.DoesNotContain(due.Items, item => item.Id == cardC.Id);
        Assert.Equal([cardA.NextReviewAt, cardB.NextReviewAt], due.Items.Select(item => item.NextReviewAt));
    }

    [Fact(DisplayName = "Cada cartão traz a pergunta, a resposta e o tópico do flashcard")]
    public async Task Due_ReturnsTheQuestionTheAnswerAndTheTopic()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await AddCardAsync(user.StudentId!.Value, content.Generated);
        Clock.Advance(TimeSpan.FromDays(2));

        // Act
        var item = Assert.Single((await DueAsync(user.Id)).Items);

        // Assert
        Assert.Equal((content.Generated.Question, content.Generated.Answer), (item.Question, item.Answer));
        Assert.Equal((content.Fractions.Id, "Frações"), (item.TopicId, item.TopicName));
    }

    [Fact(DisplayName = "O cartão que vence neste instante já está vencido")]
    public async Task Due_ACardDueExactlyNow_IsIncluded()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        var card = await AddCardAsync(user.StudentId!.Value, content.Generated);
        Clock.Advance(card.NextReviewAt - Clock.GetUtcNow());

        // Act
        var due = await DueAsync(user.Id);

        // Assert
        Assert.Equal(card.Id, Assert.Single(due.Items).Id);
    }

    [Fact(DisplayName = "Os cartões de outro aluno não aparecem")]
    public async Task Due_IgnoresTheCardsOfAnotherStudent()
    {
        // Arrange
        var content = await SeedContentAsync();
        var ana = await SeedUserAsync("ana@example.com");
        var bia = await SeedUserAsync("bia@example.com");
        var biaCard = await AddCardAsync(bia.StudentId!.Value, content.Generated);
        Clock.Advance(TimeSpan.FromDays(2));

        // Act
        var anaDue = await DueAsync(ana.Id);
        var biaDue = await DueAsync(bia.Id);

        // Assert
        Assert.Empty(anaDue.Items);
        Assert.Equal(biaCard.Id, Assert.Single(biaDue.Items).Id);
    }

    [Fact(DisplayName = "Dois cartões com a mesma data saem sempre na mesma ordem: a do id")]
    public async Task Due_BreaksTiesByTheCardId()
    {
        // Arrange: os dois cartões nascem no mesmo instante, então vencem juntos
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        var first = await AddCardAsync(user.StudentId!.Value, content.Generated);
        var second = await AddCardAsync(user.StudentId!.Value, content.Manual);
        Clock.Advance(TimeSpan.FromDays(2));

        // Act
        var ids = (await DueAsync(user.Id)).Items.Select(item => item.Id).ToList();

        // Assert
        Assert.Equal(first.NextReviewAt, second.NextReviewAt);
        Assert.Equal(ids.Order(), ids);
        Assert.Equal(2, ids.Count);
    }

    [Fact(DisplayName = "A leitura é uma consulta só e não lê o estado de agendamento: ele é do adapter do algoritmo")]
    public async Task Due_RunsOneQueryAndNeverSelectsTheSchedulingState()
    {
        // Arrange
        var content = await SeedContentAsync();
        var user = await SeedUserAsync();
        await AddCardAsync(user.StudentId!.Value, content.Generated);
        Clock.Advance(TimeSpan.FromDays(2));
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .LogTo(commands.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .Options;
        await using var context = new AppDbContext(options);

        // Act
        await new ReviewReads(context, Clock).DueAsync(user.Id, Cancel);

        // Assert: a primeira consulta acha o aluno da conta, e a segunda lê as revisões
        var sql = string.Join('\n', commands);
        Assert.DoesNotContain("scheduling_state", sql);
        Assert.Equal(2, commands.Count(command => command.Contains("Executed DbCommand", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "Uma conta que não existe, está desabilitada ou não tem aluno é sessão inválida")]
    public async Task Due_ForAnInvalidAccount_ThrowsInvalidSession()
    {
        // Arrange
        var user = await SeedUserAsync();
        await ExecuteAsync($"UPDATE app_user SET is_active = false WHERE id = '{user.Id}'");

        // Act & Assert
        foreach (var userId in new[] { Guid.CreateVersion7(), user.Id })
        {
            var exception = await Assert.ThrowsAsync<DomainAuthenticationException>(() => DueAsync(userId));
            Assert.Equal(IdentityErrors.SessionInvalid, exception.Code);
        }
    }
}
