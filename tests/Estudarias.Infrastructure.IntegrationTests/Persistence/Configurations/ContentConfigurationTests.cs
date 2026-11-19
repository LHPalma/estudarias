using Estudarias.Application.Common;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Topics;
using Estudarias.Infrastructure.IntegrationTests.Support;
using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence.Configurations;

/// <summary>
/// O mapeamento do conteúdo (#83) contra um PostgreSQL real: cada agregado vai e volta inteiro, e cada restrição do schema
/// (RN-CNT-10) é recusada pelo próprio banco. O conteúdo não tem repositório de escrita (só a carga do seed grava), então a escrita
/// aqui vai direto pelo AppDbContext, e a leitura pela visão sem rastreamento que a Application usa.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ContentConfigurationTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Operações básicas, e Frações dependendo dela, com um tipo de erro, uma lição, um exercício e os dois flashcards.</summary>
    private sealed record Content(
        Topic Basics, Topic Fractions, ErrorType ErrorType, Lesson Lesson, Exercise Exercise, Flashcard Generated, Flashcard Manual);

    private async Task<Content> SeedContentAsync()
    {
        var basics = Topic.Create("Operações básicas", "operacoes-basicas");
        var fractions = Topic.Create("Frações", "fracoes");
        fractions.AddPrerequisite(basics.Id);

        var errorType = ErrorType.Create(
            fractions.Id,
            "soma_numeradores_e_denominadores",
            "Soma numerador com numerador e denominador com denominador",
            description: null,
            "Só dá para somar frações diretamente quando os denominadores são iguais.");

        var lesson = Lesson.Create(fractions.Id, 1, "Soma de frações", "Para somar {1/2} + {1/3}, encontre um denominador comum.");

        var exercise = Exercise.Create(
            lesson.Id,
            "Quanto é 1/2 + 1/3?",
            Difficulty.Medium,
            [Alternative.Correct("5/6"), Alternative.Wrong("2/5", errorType.Id)]);

        var generated = Flashcard.CreateFromError(fractions.Id, errorType.Id, "Por que 1/2 + 1/3 não é 2/5?", "Falta o denominador comum.");
        var manual = Flashcard.CreateManual(fractions.Id, "O que o denominador indica?", "Em quantas partes o todo foi dividido.");

        await WriteAsync(db =>
        {
            db.Topics.AddRange(basics, fractions);
            db.ErrorTypes.Add(errorType);
            db.Lessons.Add(lesson);
            db.Exercises.Add(exercise);
            db.Flashcards.AddRange(generated, manual);
        });

        return new Content(basics, fractions, errorType, lesson, exercise, generated, manual);
    }

    private async Task WriteAsync(Action<AppDbContext> write)
    {
        using var scope = NewScope();
        var db = scope.Get<AppDbContext>();
        write(db);
        await db.SaveChangesAsync(Cancel);
    }

    private async Task<T> ReadAsync<T>(Func<IApplicationDbContext, Task<T>> read)
    {
        using var scope = NewScope();
        return await read(scope.Get<IApplicationDbContext>());
    }

    private async Task<PostgresException> AssertRejectedAsync(string sql, string sqlState, string constraint)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql));
        Assert.Equal(sqlState, exception.SqlState);
        Assert.Equal(constraint, exception.ConstraintName);
        return exception;
    }

    // ---------- Ida e volta ----------

    [Fact(DisplayName = "O tópico volta do banco com o nome, o slug e os pré-requisitos")]
    public async Task Topic_RoundTrips_WithItsPrerequisites()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        var fractions = await ReadAsync(db => db.Topics.SingleAsync(topic => topic.Id == content.Fractions.Id, Cancel));
        var basics = await ReadAsync(db => db.Topics.SingleAsync(topic => topic.Id == content.Basics.Id, Cancel));

        // Assert
        Assert.Equal(("Frações", "fracoes"), (fractions.Name, fractions.Slug));
        Assert.Equal([content.Basics.Id], fractions.Prerequisites);
        Assert.Empty(basics.Prerequisites);
    }

    [Fact(DisplayName = "Cada pré-requisito é uma linha de topic_prerequisite")]
    public async Task Topic_StoresEachPrerequisiteInItsOwnRow()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        var rows = await ScalarAsync<long>(
            $"SELECT count(*) FROM topic_prerequisite WHERE topic_id = '{content.Fractions.Id}' AND prerequisite_id = '{content.Basics.Id}'");

        // Assert
        Assert.Equal(1, rows);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM topic_prerequisite"));
    }

    [Fact(DisplayName = "Uma consulta lê os pré-requisitos pelo campo mapeado, sem carregar o tópico")]
    public async Task Topic_PrerequisitesCanBeProjectedInAQuery()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        var prerequisites = await ReadAsync(db => db.Topics
            .Where(topic => topic.Id == content.Fractions.Id)
            .SelectMany(topic => EF.Property<IEnumerable<TopicPrerequisite>>(topic, "_prerequisites"))
            .Select(prerequisite => prerequisite.PrerequisiteId)
            .ToListAsync(Cancel));

        // Assert
        Assert.Equal([content.Basics.Id], prerequisites);
    }

    [Fact(DisplayName = "O tipo de erro volta do banco com todos os campos, e a descrição ausente fica nula")]
    public async Task ErrorType_RoundTrips()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        var errorType = await ReadAsync(db => db.ErrorTypes.SingleAsync(value => value.Id == content.ErrorType.Id, Cancel));

        // Assert
        Assert.Equal(content.Fractions.Id, errorType.TopicId);
        Assert.Equal("soma_numeradores_e_denominadores", errorType.Code);
        Assert.Equal(content.ErrorType.Name, errorType.Name);
        Assert.Null(errorType.Description);
        Assert.Equal(content.ErrorType.Feedback, errorType.Feedback);
    }

    [Fact(DisplayName = "A lição volta do banco, e a ordem fica na coluna position")]
    public async Task Lesson_RoundTrips()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        var lesson = await ReadAsync(db => db.Lessons.SingleAsync(value => value.Id == content.Lesson.Id, Cancel));

        // Assert
        Assert.Equal(
            (content.Fractions.Id, 1, "Soma de frações", content.Lesson.Content),
            (lesson.TopicId, lesson.Order, lesson.Title, lesson.Content));
        Assert.Equal(1, await ScalarAsync<int>($"SELECT position FROM lesson WHERE id = '{content.Lesson.Id}'"));
    }

    [Fact(DisplayName = "O exercício volta do banco com as alternativas, sem precisar pedi-las")]
    public async Task Exercise_RoundTrips_WithItsAlternatives()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        var exercise = await ReadAsync(db => db.Exercises.SingleAsync(value => value.Id == content.Exercise.Id, Cancel));

        // Assert
        Assert.Equal((content.Lesson.Id, "Quanto é 1/2 + 1/3?", Difficulty.Medium), (exercise.LessonId, exercise.Statement, exercise.Difficulty));
        Assert.Equal(2, exercise.Alternatives.Count);

        var correct = Assert.Single(exercise.Alternatives, alternative => alternative.IsCorrect);
        Assert.Equal(("5/6", (Guid?)null), (correct.Text, correct.ErrorTypeId));

        var wrong = Assert.Single(exercise.Alternatives, alternative => !alternative.IsCorrect);
        Assert.Equal(("2/5", (Guid?)content.ErrorType.Id), (wrong.Text, wrong.ErrorTypeId));

        Assert.Equal(
            content.Exercise.Alternatives.Select(alternative => alternative.Id).Order(),
            exercise.Alternatives.Select(alternative => alternative.Id).Order());
    }

    [Theory(DisplayName = "A dificuldade é gravada em minúsculas e volta como o mesmo valor")]
    [InlineData(Difficulty.Easy, "easy")]
    [InlineData(Difficulty.Medium, "medium")]
    [InlineData(Difficulty.Hard, "hard")]
    public async Task Exercise_StoresTheDifficultyInLowercase(Difficulty difficulty, string stored)
    {
        // Arrange
        var content = await SeedContentAsync();
        var exercise = Exercise.Create(
            content.Lesson.Id, "Quanto é 1/4 + 1/4?", difficulty, [Alternative.Correct("1/2"), Alternative.Wrong("2/8", content.ErrorType.Id)]);
        await WriteAsync(db => db.Exercises.Add(exercise));

        // Act
        var column = await ScalarAsync<string>($"SELECT difficulty FROM exercise WHERE id = '{exercise.Id}'");
        var read = await ReadAsync(db => db.Exercises.SingleAsync(value => value.Id == exercise.Id, Cancel));

        // Assert
        Assert.Equal(stored, column);
        Assert.Equal(difficulty, read.Difficulty);
    }

    [Fact(DisplayName = "Os flashcards voltam do banco com a origem, e só o gerado por erro tem tipo de erro")]
    public async Task Flashcard_RoundTrips()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        var generated = await ReadAsync(db => db.Flashcards.SingleAsync(value => value.Id == content.Generated.Id, Cancel));
        var manual = await ReadAsync(db => db.Flashcards.SingleAsync(value => value.Id == content.Manual.Id, Cancel));

        // Assert
        Assert.Equal(
            (content.Fractions.Id, content.Generated.Question, content.Generated.Answer, FlashcardOrigin.GeneratedByError, (Guid?)content.ErrorType.Id),
            (generated.TopicId, generated.Question, generated.Answer, generated.Origin, generated.ErrorTypeId));
        Assert.Equal((FlashcardOrigin.Manual, (Guid?)null), (manual.Origin, manual.ErrorTypeId));
        Assert.Equal("generated_by_error", await ScalarAsync<string>($"SELECT origin FROM flashcard WHERE id = '{content.Generated.Id}'"));
        Assert.Equal("manual", await ScalarAsync<string>($"SELECT origin FROM flashcard WHERE id = '{content.Manual.Id}'"));
    }

    [Fact(DisplayName = "A leitura do conteúdo pela Application não rastreia entidades")]
    public async Task Reads_AreNotTracked()
    {
        // Arrange
        var content = await SeedContentAsync();
        using var scope = NewScope();

        // Act
        await scope.Get<IApplicationDbContext>().Exercises.SingleAsync(value => value.Id == content.Exercise.Id, Cancel);
        await scope.Get<IApplicationDbContext>().Topics.ToListAsync(Cancel);

        // Assert
        Assert.Empty(scope.Get<AppDbContext>().ChangeTracker.Entries());
    }

    // ---------- Restrições no banco (RN-CNT-10) ----------

    [Fact(DisplayName = "O banco recusa dois tópicos com o mesmo slug")]
    public async Task Topic_WithADuplicatedSlug_IsRejected()
    {
        // Arrange
        await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO topic (id, name, slug) VALUES ('{Guid.CreateVersion7()}', 'Outro', 'fracoes')",
            PostgresErrorCodes.UniqueViolation,
            "ux_topic_slug");
    }

    [Fact(DisplayName = "O banco recusa um tópico pré-requisito de si mesmo")]
    public async Task TopicPrerequisite_PointingToItself_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO topic_prerequisite (topic_id, prerequisite_id) VALUES ('{content.Basics.Id}', '{content.Basics.Id}')",
            PostgresErrorCodes.CheckViolation,
            "ck_topic_prerequisite_not_self");
    }

    [Fact(DisplayName = "O banco recusa o mesmo pré-requisito duas vezes")]
    public async Task TopicPrerequisite_Duplicated_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO topic_prerequisite (topic_id, prerequisite_id) VALUES ('{content.Fractions.Id}', '{content.Basics.Id}')",
            PostgresErrorCodes.UniqueViolation,
            "PK_topic_prerequisite");
    }

    [Fact(DisplayName = "O banco recusa um pré-requisito que não é tópico")]
    public async Task TopicPrerequisite_ToAnUnknownTopic_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO topic_prerequisite (topic_id, prerequisite_id) VALUES ('{content.Basics.Id}', '{Guid.CreateVersion7()}')",
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_topic_prerequisite_topic_prerequisite_id");
    }

    [Fact(DisplayName = "O banco recusa dois tipos de erro com o mesmo código no mesmo tópico")]
    public async Task ErrorType_WithADuplicatedCodeInTheTopic_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO error_type (id, topic_id, code, name, feedback) VALUES ('{Guid.CreateVersion7()}', '{content.Fractions.Id}', 'soma_numeradores_e_denominadores', 'Outro', 'Outro')",
            PostgresErrorCodes.UniqueViolation,
            "ux_error_type_topic_id_code");
    }

    [Fact(DisplayName = "O mesmo código de tipo de erro é aceito em outro tópico")]
    public async Task ErrorType_WithTheSameCodeInAnotherTopic_IsAccepted()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        await ExecuteAsync(
            $"INSERT INTO error_type (id, topic_id, code, name, feedback) VALUES ('{Guid.CreateVersion7()}', '{content.Basics.Id}', 'soma_numeradores_e_denominadores', 'Outro', 'Outro')");

        // Assert
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM error_type"));
    }

    [Fact(DisplayName = "O banco recusa duas lições na mesma posição do tópico")]
    public async Task Lesson_WithADuplicatedPositionInTheTopic_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO lesson (id, topic_id, position, title, content) VALUES ('{Guid.CreateVersion7()}', '{content.Fractions.Id}', 1, 'Outra', 'Outra')",
            PostgresErrorCodes.UniqueViolation,
            "ux_lesson_topic_id_position");
    }

    [Fact(DisplayName = "O banco recusa uma lição na posição zero")]
    public async Task Lesson_AtPositionZero_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO lesson (id, topic_id, position, title, content) VALUES ('{Guid.CreateVersion7()}', '{content.Fractions.Id}', 0, 'Outra', 'Outra')",
            PostgresErrorCodes.CheckViolation,
            "ck_lesson_position");
    }

    [Fact(DisplayName = "O banco recusa uma dificuldade fora de easy, medium e hard")]
    public async Task Exercise_WithAnUnknownDifficulty_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO exercise (id, lesson_id, statement, difficulty) VALUES ('{Guid.CreateVersion7()}', '{content.Lesson.Id}', 'Outro', 'facil')",
            PostgresErrorCodes.CheckViolation,
            "ck_exercise_difficulty");
    }

    [Fact(DisplayName = "O banco recusa uma segunda alternativa correta no mesmo exercício")]
    public async Task Alternative_SecondCorrectInTheExercise_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO alternative (id, exercise_id, text, is_correct, error_type_id) VALUES ('{Guid.CreateVersion7()}', '{content.Exercise.Id}', '10/12', true, NULL)",
            PostgresErrorCodes.UniqueViolation,
            "ux_alternative_exercise_id_correct");
    }

    [Fact(DisplayName = "O banco aceita várias alternativas erradas no mesmo exercício")]
    public async Task Alternative_SeveralWrongInTheExercise_AreAccepted()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        await ExecuteAsync(
            $"INSERT INTO alternative (id, exercise_id, text, is_correct, error_type_id) VALUES ('{Guid.CreateVersion7()}', '{content.Exercise.Id}', '1/6', false, '{content.ErrorType.Id}')");

        // Assert
        Assert.Equal(2, await ScalarAsync<long>($"SELECT count(*) FROM alternative WHERE exercise_id = '{content.Exercise.Id}' AND NOT is_correct"));
    }

    [Theory(DisplayName = "O banco recusa alternativa correta com tipo de erro e errada sem tipo de erro")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Alternative_WithAnInconsistentErrorType_IsRejected(bool isCorrect, bool withErrorType)
    {
        // Arrange
        var content = await SeedContentAsync();
        var correct = isCorrect ? "true" : "false";
        var errorType = withErrorType ? $"'{content.ErrorType.Id}'" : "NULL";

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO alternative (id, exercise_id, text, is_correct, error_type_id) VALUES ('{Guid.CreateVersion7()}', '{content.Exercise.Id}', 'Outra', {correct}, {errorType})",
            PostgresErrorCodes.CheckViolation,
            "ck_alternative_error_type");
    }

    [Fact(DisplayName = "O banco recusa um segundo flashcard gerado pelo mesmo tipo de erro")]
    public async Task Flashcard_SecondGeneratedForTheErrorType_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO flashcard (id, topic_id, question, answer, origin, error_type_id) VALUES ('{Guid.CreateVersion7()}', '{content.Fractions.Id}', 'Outra', 'Outra', 'generated_by_error', '{content.ErrorType.Id}')",
            PostgresErrorCodes.UniqueViolation,
            "ux_flashcard_error_type_id_generated");
    }

    [Theory(DisplayName = "O banco recusa a origem do flashcard em desacordo com o tipo de erro")]
    [InlineData("manual", true)]
    [InlineData("generated_by_error", false)]
    public async Task Flashcard_WithAnInconsistentErrorType_IsRejected(string origin, bool withErrorType)
    {
        // Arrange
        var content = await SeedContentAsync();
        var errorType = withErrorType ? $"'{content.ErrorType.Id}'" : "NULL";

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO flashcard (id, topic_id, question, answer, origin, error_type_id) VALUES ('{Guid.CreateVersion7()}', '{content.Fractions.Id}', 'Outra', 'Outra', '{origin}', {errorType})",
            PostgresErrorCodes.CheckViolation,
            "ck_flashcard_error_type");
    }

    [Fact(DisplayName = "O banco recusa uma origem de flashcard desconhecida")]
    public async Task Flashcard_WithAnUnknownOrigin_IsRejected()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act & Assert
        await AssertRejectedAsync(
            $"INSERT INTO flashcard (id, topic_id, question, answer, origin, error_type_id) VALUES ('{Guid.CreateVersion7()}', '{content.Fractions.Id}', 'Outra', 'Outra', 'gerado_por_erro', '{content.ErrorType.Id}')",
            PostgresErrorCodes.CheckViolation,
            "ck_flashcard_origin");
    }

    [Theory(DisplayName = "O conteúdo não some em cascata: apagar um item de que outro depende é recusado")]
    [InlineData("topic", "Basics")]
    [InlineData("topic", "Fractions")]
    [InlineData("error_type", "ErrorType")]
    [InlineData("lesson", "Lesson")]
    public async Task Delete_OfReferencedContent_IsRestricted(string table, string item)
    {
        // Arrange
        var content = await SeedContentAsync();
        var id = item switch
        {
            "Basics" => content.Basics.Id,
            "Fractions" => content.Fractions.Id,
            "ErrorType" => content.ErrorType.Id,
            _ => content.Lesson.Id,
        };

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync($"DELETE FROM {table} WHERE id = '{id}'"));

        // Assert
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Equal(1, await ScalarAsync<long>($"SELECT count(*) FROM {table} WHERE id = '{id}'"));
    }

    [Fact(DisplayName = "As alternativas são parte do exercício: saem junto com ele, sem sobrar linha órfã")]
    public async Task Delete_OfAnExercise_TakesItsAlternatives()
    {
        // Arrange
        var content = await SeedContentAsync();

        // Act
        await ExecuteAsync($"DELETE FROM exercise WHERE id = '{content.Exercise.Id}'");

        // Assert
        Assert.Equal(0, await ScalarAsync<long>($"SELECT count(*) FROM alternative WHERE exercise_id = '{content.Exercise.Id}'"));
    }
}
