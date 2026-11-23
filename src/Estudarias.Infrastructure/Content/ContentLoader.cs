using Estudarias.Application.Content;
using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Topics;
using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Content;

/// <summary>
/// A carga do conteúdo (RF-CNT-06). O conteúdo não tem repositório de escrita (RN-CNT-14): esta é a única escrita dele, direto pelo
/// <see cref="AppDbContext"/>. Cada item é achado pela chave natural (RN-CNT-11): se existe, é revisado pelo Domain e mantém o id; se
/// não, é criado. Nada é apagado (RN-CNT-13). Tudo vai num único <c>SaveChanges</c>, então a carga é uma transação (RN-CNT-12).
/// </summary>
internal sealed class ContentLoader(AppDbContext db) : IContentLoader
{
    public async Task<ContentLoadResult> LoadAsync(string folder, CancellationToken cancellationToken)
    {
        var problems = new List<ContentProblem>();
        var files = YamlContentReader.Read(folder, problems);
        if (problems.Count > 0)
        {
            return ContentLoadResult.Failed(problems);
        }

        // Todo o conteúdo do banco, rastreado: é pouco, e a carga precisa dele inteiro para achar cada item pela chave natural.
        var topics = await db.Topics.ToListAsync(cancellationToken);
        var slugById = topics.ToDictionary(topic => topic.Id, topic => topic.Slug);
        var stored = topics.ToDictionary(
            topic => topic.Slug,
            topic => (IReadOnlyCollection<string>)[.. topic.Prerequisites.Select(id => slugById[id])],
            StringComparer.Ordinal);

        problems.AddRange(ContentValidator.Validate(files, stored));
        if (problems.Count > 0)
        {
            return ContentLoadResult.Failed(problems);
        }

        var state = new StoredContent(
            topics.ToDictionary(topic => topic.Slug, StringComparer.Ordinal),
            await db.ErrorTypes.ToListAsync(cancellationToken),
            await db.Lessons.ToListAsync(cancellationToken),
            await db.Exercises.ToListAsync(cancellationToken),
            await db.Flashcards.ToListAsync(cancellationToken));

        var touched = new List<object>();
        foreach (var file in files)
        {
            touched.Add(UpsertTopic(file.Topic, state));
        }

        foreach (var file in files)
        {
            var topic = state.Topics[file.Topic.Slug];
            Apply(file, "pré-requisitos", problems, () =>
                topic.ReplacePrerequisites([.. file.Topic.Prerequisites.Select(slug => state.Topics[slug].Id)]));
            touched.AddRange(UpsertTopicContent(file, topic, state, problems));
        }

        if (problems.Count > 0)
        {
            return ContentLoadResult.Failed(problems);
        }

        var (created, updated, unchanged) = Summarize(touched);
        await db.SaveChangesAsync(cancellationToken);

        return new ContentLoadResult([], created, updated, unchanged);
    }

    private Topic UpsertTopic(TopicDocument document, StoredContent state)
    {
        if (state.Topics.TryGetValue(document.Slug, out var topic))
        {
            topic.Rename(document.Name);
            return topic;
        }

        topic = Topic.Create(document.Name, document.Slug);
        state.Topics[document.Slug] = topic;
        db.Topics.Add(topic);
        return topic;
    }

    private List<object> UpsertTopicContent(ContentFile file, Topic topic, StoredContent state, List<ContentProblem> problems)
    {
        var touched = new List<object>();

        var errorTypeIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var document in file.Topic.ErrorTypes)
        {
            Apply(file, $"tipo de erro '{document.Code}'", problems, () =>
            {
                var errorType = state.ErrorTypes.FirstOrDefault(stored => stored.TopicId == topic.Id && stored.Code == document.Code);
                if (errorType is null)
                {
                    errorType = ErrorType.Create(topic.Id, document.Code, document.Name, document.Description, document.Feedback);
                    db.ErrorTypes.Add(errorType);
                }
                else
                {
                    errorType.Revise(document.Name, document.Description, document.Feedback);
                }

                errorTypeIds[document.Code] = errorType.Id;
                touched.Add(errorType);
            });
        }

        // Os exercícios do tópico, de qualquer lição: o código é único no tópico, e o exercício pode mudar de lição sem perder o id.
        var topicLessonIds = state.Lessons.Where(lesson => lesson.TopicId == topic.Id).Select(lesson => lesson.Id).ToHashSet();
        var topicExercises = state.Exercises.Where(exercise => topicLessonIds.Contains(exercise.LessonId)).ToList();

        foreach (var document in file.Topic.Lessons)
        {
            var lesson = state.Lessons.FirstOrDefault(stored => stored.TopicId == topic.Id && stored.Order == document.Order);
            Apply(file, $"lição {document.Order}", problems, () =>
            {
                if (lesson is null)
                {
                    lesson = Lesson.Create(topic.Id, document.Order, document.Title, document.Content);
                    db.Lessons.Add(lesson);
                }
                else
                {
                    lesson.Revise(document.Title, document.Content);
                }

                touched.Add(lesson);
            });

            if (lesson is null)
            {
                continue;
            }

            // A ordem do arquivo é a ordem da lição: a posição do exercício é o lugar dele na lista.
            for (var index = 0; index < document.Exercises.Count; index++)
            {
                var exercise = document.Exercises[index];
                var position = index + 1;
                Apply(file, $"exercício '{exercise.Code}'", problems, () =>
                    touched.Add(UpsertExercise(exercise, lesson.Id, position, topicExercises, errorTypeIds)));
            }
        }

        foreach (var document in file.Topic.Flashcards)
        {
            Apply(file, $"flashcard '{document.Code}'", problems, () =>
            {
                Guid? errorTypeId = document.ErrorType is null ? null : errorTypeIds[document.ErrorType];
                var flashcard = state.Flashcards.FirstOrDefault(stored => stored.TopicId == topic.Id && stored.Code == document.Code);
                if (flashcard is null)
                {
                    flashcard = errorTypeId is { } id
                        ? Flashcard.CreateFromError(topic.Id, document.Code, id, document.Question, document.Answer)
                        : Flashcard.CreateManual(topic.Id, document.Code, document.Question, document.Answer);
                    db.Flashcards.Add(flashcard);
                }
                else
                {
                    flashcard.Revise(document.Question, document.Answer, errorTypeId);
                }

                touched.Add(flashcard);
            });
        }

        return touched;
    }

    private Exercise UpsertExercise(
        ExerciseDocument document,
        Guid lessonId,
        int position,
        List<Exercise> topicExercises,
        Dictionary<string, Guid> errorTypeIds)
    {
        var difficulty = ContentValidator.ParseDifficulty(document.Difficulty)!.Value;
        List<Alternative> alternatives =
        [
            .. document.Alternatives.Select(alternative => alternative.Correct
                ? Alternative.Correct(alternative.Text)
                : Alternative.Wrong(alternative.Text, errorTypeIds[alternative.ErrorType!])),
        ];

        var exercise = topicExercises.FirstOrDefault(stored => stored.Code == document.Code);
        if (exercise is null)
        {
            exercise = Exercise.Create(lessonId, position, document.Code, document.Statement, difficulty, alternatives);
            db.Exercises.Add(exercise);
        }
        else
        {
            exercise.Revise(lessonId, position, document.Statement, difficulty, alternatives);
        }

        return exercise;
    }

    /// <summary>
    /// Uma regra que só aparece com o banco (por exemplo, a alternativa removida de um exercício já carregado) vira um problema com o
    /// arquivo e o item, e a carga segue conferindo o resto para listar tudo de uma vez.
    /// </summary>
    private static void Apply(ContentFile file, string item, List<ContentProblem> problems, Action change)
    {
        try
        {
            change();
        }
        catch (DomainValidationException exception)
        {
            problems.Add(new ContentProblem(file.FileName, item, $"{exception.Message} ({exception.Code})"));
        }
    }

    /// <summary>
    /// Conta os itens pelo que o rastreamento vê: criado (novo), alterado (alguma coluna ou alguma linha da coleção do agregado mudou)
    /// ou igual. Como a revisão com os mesmos valores não muda nada, recarregar o mesmo YAML dá tudo igual (FA-CNT-06).
    /// </summary>
    private (int Created, int Updated, int Unchanged) Summarize(List<object> touched)
    {
        db.ChangeTracker.DetectChanges();

        var ownersWithChanges = db.ChangeTracker.Entries()
            .Where(entry => entry.Metadata.IsOwned() && entry.State != EntityState.Unchanged)
            .Select(entry => entry.Property(entry.Metadata.FindOwnership()!.Properties[0].Name).CurrentValue)
            .ToHashSet();

        int created = 0, updated = 0, unchanged = 0;
        foreach (var entry in touched.Distinct().Select(db.Entry))
        {
            if (entry.State == EntityState.Added)
            {
                created++;
            }
            else if (entry.State == EntityState.Modified || ownersWithChanges.Contains(entry.Property("Id").CurrentValue))
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        return (created, updated, unchanged);
    }

    private sealed record StoredContent(
        Dictionary<string, Topic> Topics,
        List<ErrorType> ErrorTypes,
        List<Lesson> Lessons,
        List<Exercise> Exercises,
        List<Flashcard> Flashcards);
}
