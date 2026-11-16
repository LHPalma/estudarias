using Estudarias.Domain.Exercises;

namespace Estudarias.Domain.Scoring;

/// <summary>Uma resposta já corrigida: a dificuldade do exercício e se o aluno acertou.</summary>
public readonly record struct GradedAnswer(Difficulty Difficulty, bool IsCorrect);
