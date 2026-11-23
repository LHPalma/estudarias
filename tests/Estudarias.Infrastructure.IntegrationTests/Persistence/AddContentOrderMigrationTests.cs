using Estudarias.Infrastructure.IntegrationTests.Support;
using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Estudarias.Infrastructure.IntegrationTests.Persistence;

/// <summary>
/// A migration <c>AddContentOrder</c> sobre um banco que já tem conteúdo: as linhas que existiam sem posição passam a ter uma, pela
/// ordem do id, e a gravação nova a informa. Sem o preenchimento, o 0 do padrão violaria as CHECKs da própria migration.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AddContentOrderMigrationTests(PostgresFixture postgres) : DatabaseTests(postgres)
{
    private const string BeforeTheOrder = "20261119204230_AddContentCodes";

    private async Task MigrateAsync(string? target = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;
        await using var context = new AppDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(target, Cancel);
    }

    [Fact(DisplayName = "As linhas que já existiam ganham posição de 1 em diante, pela ordem do id, dentro da lição e do exercício")]
    public async Task Up_NumbersTheExistingRows()
    {
        // Arrange: o banco como estava antes da migration, com um exercício de duas alternativas e outro de três
        await MigrateAsync(BeforeTheOrder);
        var topic = Guid.CreateVersion7();
        var lesson = Guid.CreateVersion7();
        var errorType = Guid.CreateVersion7();
        Guid[] exercises = [Guid.Parse("00000000-0000-7000-8000-000000000001"), Guid.Parse("00000000-0000-7000-8000-000000000002")];
        await ExecuteAsync($"INSERT INTO topic (id, name, slug) VALUES ('{topic}', 'Frações', 'fracoes')");
        await ExecuteAsync($"INSERT INTO lesson (id, topic_id, position, title, content) VALUES ('{lesson}', '{topic}', 1, 'Lição', 'Texto')");
        await ExecuteAsync(
            "INSERT INTO error_type (id, topic_id, code, name, description, feedback) " +
            $"VALUES ('{errorType}', '{topic}', 'soma_direta', 'Soma direta', NULL, 'Falta o denominador comum.')");
        foreach (var (exercise, index) in exercises.Select((id, index) => (id, index)))
        {
            await ExecuteAsync(
                "INSERT INTO exercise (id, lesson_id, code, statement, difficulty) " +
                $"VALUES ('{exercise}', '{lesson}', 'exercicio_{index}', 'Pergunta {index}', 'easy')");
        }

        string[] alternativeIds =
        [
            "00000000-0000-7000-8000-0000000000a1", "00000000-0000-7000-8000-0000000000a2",
            "00000000-0000-7000-8000-0000000000b1", "00000000-0000-7000-8000-0000000000b2", "00000000-0000-7000-8000-0000000000b3",
        ];
        await ExecuteAsync(
            "INSERT INTO alternative (id, exercise_id, text, is_correct, error_type_id) VALUES " +
            $"('{alternativeIds[0]}', '{exercises[0]}', 'certa', true, NULL), ('{alternativeIds[1]}', '{exercises[0]}', 'errada', false, '{errorType}'), " +
            $"('{alternativeIds[2]}', '{exercises[1]}', 'certa', true, NULL), ('{alternativeIds[3]}', '{exercises[1]}', 'errada 1', false, '{errorType}'), " +
            $"('{alternativeIds[4]}', '{exercises[1]}', 'errada 2', false, '{errorType}')");

        // Act
        await MigrateAsync();

        // Assert
        var exercisePositions = await ReadAsync(async db =>
            await db.Exercises.OrderBy(exercise => exercise.Position).Select(exercise => exercise.Position).ToListAsync(Cancel));
        var firstExercise = await ScalarAsync<int[]>($"SELECT array_agg(position ORDER BY id) FROM alternative WHERE exercise_id = '{exercises[0]}'");
        var secondExercise = await ScalarAsync<int[]>($"SELECT array_agg(position ORDER BY id) FROM alternative WHERE exercise_id = '{exercises[1]}'");
        Assert.Equal([1, 2], exercisePositions);
        Assert.Equal([1, 2], firstExercise);
        Assert.Equal([1, 2, 3], secondExercise);
    }

    [Fact(DisplayName = "Depois da migration o banco exige a posição: nenhum padrão esconde uma gravação sem ela")]
    public async Task Up_LeavesNoDefaultOnThePositionColumns()
    {
        // Act
        var defaults = await ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.columns " +
            "WHERE table_name IN ('exercise', 'alternative') AND column_name = 'position' AND column_default IS NOT NULL");

        // Assert
        Assert.Equal(0L, defaults);
    }

    [Fact(DisplayName = "Reverter a migration tira as colunas de posição, e migrar de novo as devolve")]
    public async Task Down_ThenUp_RoundTrips()
    {
        // Act
        await MigrateAsync(BeforeTheOrder);
        var without = await ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.columns WHERE table_name IN ('exercise', 'alternative') AND column_name = 'position'");
        await MigrateAsync();
        var with = await ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.columns WHERE table_name IN ('exercise', 'alternative') AND column_name = 'position'");

        // Assert
        Assert.Equal((0L, 2L), (without, with));
    }
}
