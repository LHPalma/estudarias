using Estudarias.Api.Links;
using Estudarias.Application.Trail.Queries;

namespace Estudarias.Api.Contracts.Lessons;

/// <summary>Uma alternativa sem o gabarito: só o id e o texto. Nada aqui diz se ela é a correta (RN-TRL-11).</summary>
public sealed record LessonAlternativeResponse(Guid Id, string Text);

/// <summary>Um exercício da lição, com as alternativas na ordem do conteúdo. <c>Difficulty</c> é <c>easy</c>, <c>medium</c> ou <c>hard</c>.</summary>
public sealed record LessonExerciseResponse(
    Guid Id,
    string Statement,
    string Difficulty,
    IReadOnlyList<LessonAlternativeResponse> Alternatives,
    IReadOnlyList<Link> Links);

/// <summary>
/// A lição do aluno logado (<c>GET /lessons/{id}</c>), com o conteúdo e os exercícios, <b>sem o gabarito</b>. Os <c>links</c> são
/// montados pelo controller, que conhece as rotas (ADR-004).
/// </summary>
public sealed record LessonResponse(
    Guid Id,
    Guid TopicId,
    int Order,
    string Title,
    string Content,
    IReadOnlyList<LessonExerciseResponse> Exercises,
    IReadOnlyList<Link> Links)
{
    public static LessonResponse From(
        LessonView lesson, Func<LessonExerciseView, IReadOnlyList<Link>> exerciseLinks, IReadOnlyList<Link> links) =>
        new(
            lesson.Id,
            lesson.TopicId,
            lesson.Order,
            lesson.Title,
            lesson.Content,
            [.. lesson.Exercises.Select(exercise => new LessonExerciseResponse(
                exercise.Id,
                exercise.Statement,
                DifficultyText.Of(exercise.Difficulty),
                [.. exercise.Alternatives.Select(alternative => new LessonAlternativeResponse(alternative.Id, alternative.Text))],
                exerciseLinks(exercise)))],
            links);
}
