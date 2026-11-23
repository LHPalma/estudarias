namespace Estudarias.Domain.Lessons;

/// <summary>O estado de uma lição para um aluno, derivado das tentativas dele nos exercícios dela (<see cref="LessonProgress"/>).</summary>
public enum LessonStatus
{
    /// <summary>Nenhuma tentativa em nenhum exercício da lição (e também a lição sem exercícios).</summary>
    NotStarted,

    /// <summary>Pelo menos uma tentativa, mas ainda há exercício que o aluno nunca acertou.</summary>
    InProgress,

    /// <summary>O aluno acertou cada exercício da lição pelo menos uma vez, em qualquer tentativa.</summary>
    Completed
}
