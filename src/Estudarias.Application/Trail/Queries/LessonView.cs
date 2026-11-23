using Estudarias.Domain.Exercises;

namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// A lição com o conteúdo e os exercícios, <b>sem o gabarito</b> (RN-TRL-11): nenhum tipo desta leitura tem um campo que diga qual
/// alternativa é a correta, nem o tipo de erro de cada errada. A correta só é revelada depois de o aluno responder.
/// </summary>
public sealed record LessonView(
    Guid Id,
    Guid TopicId,
    int Order,
    string Title,
    string Content,
    IReadOnlyList<LessonExerciseView> Exercises);

/// <summary>Um exercício da lição, com as alternativas na ordem do conteúdo.</summary>
public sealed record LessonExerciseView(
    Guid Id, string Statement, Difficulty Difficulty, IReadOnlyList<LessonAlternativeView> Alternatives);

/// <summary>Uma alternativa sem o gabarito: só o id e o texto.</summary>
public sealed record LessonAlternativeView(Guid Id, string Text);
