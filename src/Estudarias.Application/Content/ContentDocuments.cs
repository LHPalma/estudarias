namespace Estudarias.Application.Content;

/// <summary>Um arquivo de conteúdo (<c>content/&lt;slug&gt;.yaml</c>) já lido: o nome do arquivo, para as mensagens, e o tópico.</summary>
public sealed record ContentFile(string FileName, TopicDocument Topic);

/// <summary>Um tópico como está no YAML. Os pré-requisitos são slugs; o slug é a chave natural do tópico.</summary>
public sealed record TopicDocument(
    string Slug,
    string Name,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<ErrorTypeDocument> ErrorTypes,
    IReadOnlyList<LessonDocument> Lessons,
    IReadOnlyList<FlashcardDocument> Flashcards);

/// <summary>Um tipo de erro do tópico; o código é a chave natural dentro do tópico.</summary>
public sealed record ErrorTypeDocument(string Code, string Name, string? Description, string Feedback);

/// <summary>Uma lição do tópico; a ordem é a chave natural dentro do tópico.</summary>
public sealed record LessonDocument(int Order, string Title, string Content, IReadOnlyList<ExerciseDocument> Exercises);

/// <summary>Um exercício da lição; o código é a chave natural dentro do tópico (o exercício pode mudar de lição).</summary>
public sealed record ExerciseDocument(string Code, string Statement, string Difficulty, IReadOnlyList<AlternativeDocument> Alternatives);

/// <summary>Uma alternativa: a correta não tem tipo de erro; a errada aponta o código de um tipo de erro do mesmo tópico.</summary>
public sealed record AlternativeDocument(string Text, bool Correct, string? ErrorType);

/// <summary>Um flashcard do tópico; com <c>ErrorType</c> é o gerado por aquele erro, sem é manual. O código é a chave natural no tópico.</summary>
public sealed record FlashcardDocument(string Code, string Question, string Answer, string? ErrorType);

/// <summary>Um problema do conteúdo, com o arquivo e o item, para o autor corrigir sem ler o código (RNF-CNT-03).</summary>
public sealed record ContentProblem(string FileName, string Item, string Message)
{
    public override string ToString() => $"{FileName} — {Item}: {Message}";
}
