namespace Estudarias.Application.Content;

/// <summary>
/// A carga do conteúdo (RF-CNT-06): lê os YAML de uma pasta, valida tudo e grava numa transação, por chave natural. É tudo ou nada
/// (RN-CNT-12): com qualquer problema, nada é gravado.
/// </summary>
public interface IContentLoader
{
    Task<ContentLoadResult> LoadAsync(string folder, CancellationToken cancellationToken);
}

/// <summary>
/// O resultado da carga. Com problemas, nada foi gravado. Sem, as contagens são dos itens de conteúdo (tópicos, tipos de erro,
/// lições, exercícios e flashcards) criados, alterados e já iguais ao YAML.
/// </summary>
public sealed record ContentLoadResult(IReadOnlyList<ContentProblem> Problems, int Created, int Updated, int Unchanged)
{
    public bool Succeeded => Problems.Count == 0;

    public static ContentLoadResult Failed(IReadOnlyList<ContentProblem> problems) => new(problems, 0, 0, 0);
}
