using Estudarias.Domain.Attempts;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Students;

namespace Estudarias.Infrastructure.IntegrationTests.Support;

/// <summary>O que os testes das leituras da trilha semeiam além do <see cref="SampleContent"/>: exercícios, respostas e progresso.</summary>
internal static class LearningSeeds
{
    /// <summary>
    /// Uma lição nova no tópico (na posição seguinte à última) com um exercício de cada dificuldade pedida, cada um com uma alternativa
    /// correta e uma errada. Devolve os exercícios, para o teste responder.
    /// </summary>
    public static async Task<List<Exercise>> SeedExercisesAsync(
        this DatabaseTests tests, SampleContent content, Guid topicId, params Difficulty[] difficulties)
    {
        var position = await tests.ScalarAsync<int>($"SELECT COALESCE(max(position), 0) + 1 FROM lesson WHERE topic_id = '{topicId}'");
        var lesson = Lesson.Create(topicId, position, "Lição de apoio", "Conteúdo.");
        var exercises = difficulties
            .Select((difficulty, index) => Exercise.Create(
                lesson.Id,
                index + 1,
                $"apoio_{lesson.Id:N}_{index}",
                $"Pergunta {index}",
                difficulty,
                [Alternative.Correct("certa"), Alternative.Wrong("errada", content.ErrorType.Id)]))
            .ToList();

        await tests.WriteAsync(db =>
        {
            db.Lessons.Add(lesson);
            db.Exercises.AddRange(exercises);
        });

        return exercises;
    }

    /// <summary>Grava uma resposta do aluno e avança o relógio um minuto: cada resposta tem o próprio instante.</summary>
    public static async Task AnswerAsync(this DatabaseTests tests, Guid studentId, Exercise exercise, bool correct)
    {
        var alternative = exercise.Alternatives.Single(candidate => candidate.IsCorrect == correct);
        using (var scope = tests.NewScope())
        {
            scope.Attempts.Add(Attempt.Create(studentId, exercise.Id, alternative.Id, null, tests.Clock));
            await scope.UnitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        tests.Clock.Advance(TimeSpan.FromMinutes(1));
    }

    /// <summary>Carrega o aluno, aplica a mudança (liberar, dominar) e grava.</summary>
    public static async Task UpdateStudentAsync(this DatabaseTests tests, Guid studentId, Action<Student> change)
    {
        using var scope = tests.NewScope();
        var student = (await scope.Students.FindAsync(studentId, TestContext.Current.CancellationToken))!;
        change(student);
        await scope.UnitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
