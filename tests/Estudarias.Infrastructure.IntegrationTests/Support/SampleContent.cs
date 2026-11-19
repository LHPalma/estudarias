using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Topics;

namespace Estudarias.Infrastructure.IntegrationTests.Support;

/// <summary>
/// Operações básicas, e Frações dependendo dela, com um tipo de erro, uma lição, um exercício (uma alternativa correta e uma
/// errada) e os dois flashcards: o gerado pelo tipo de erro e um manual.
/// </summary>
internal sealed record SampleContent(
    Topic Basics, Topic Fractions, ErrorType ErrorType, Lesson Lesson, Exercise Exercise, Flashcard Generated, Flashcard Manual)
{
    public Alternative Correct => Exercise.Alternatives.Single(alternative => alternative.IsCorrect);

    public Alternative Wrong => Exercise.Alternatives.Single(alternative => !alternative.IsCorrect);

    public static SampleContent Create()
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

        return new SampleContent(basics, fractions, errorType, lesson, exercise, generated, manual);
    }
}
