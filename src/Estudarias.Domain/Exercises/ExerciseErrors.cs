namespace Estudarias.Domain.Exercises;

public static class ExerciseErrors
{
    public const string LessonRequired = "exercise.lesson.required";
    public const string StatementRequired = "exercise.statement.required";
    public const string DifficultyInvalid = "exercise.difficulty.invalid";
    public const string AlternativesTooFew = "exercise.alternative.tooFew";
    public const string AlternativesNoCorrect = "exercise.alternative.noCorrect";
    public const string AlternativesMultipleCorrect = "exercise.alternative.multipleCorrect";
    public const string AlternativeTextRequired = "exercise.alternative.textRequired";
    public const string AlternativeTextDuplicated = "exercise.alternative.textDuplicated";
    public const string AlternativeErrorTypeRequired = "exercise.alternative.errorTypeRequired";
}
