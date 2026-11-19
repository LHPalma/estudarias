using Estudarias.Application.Content;

namespace Estudarias.Api.Commands;

/// <summary>
/// <c>dotnet run --project src/Estudarias.Api -- seed [pasta]</c>: carrega o conteúdo dos YAML da pasta (padrão: <c>content</c>) e
/// termina, sem subir o servidor. Usa a mesma composição e a mesma <c>ConnectionStrings__Default</c> da API. Sai com 0 quando
/// gravou, e com 1 quando o conteúdo tem problema (e nada foi gravado).
/// </summary>
public static class SeedCommand
{
    public const string Name = "seed";

    private const string DefaultFolder = "content";

    public static bool IsRequested(string[] args) => args is [Name, ..];

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output)
    {
        var folder = Path.GetFullPath(args.Length > 1 ? args[1] : DefaultFolder);

        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IContentLoader>().LoadAsync(folder, CancellationToken.None);

        if (!result.Succeeded)
        {
            await output.WriteLineAsync($"Conteúdo de {folder} não foi carregado: {result.Problems.Count} problema(s). Nada foi gravado.");
            foreach (var problem in result.Problems)
            {
                await output.WriteLineAsync($"  - {problem}");
            }

            return 1;
        }

        await output.WriteLineAsync(
            $"Conteúdo de {folder} carregado: {result.Created} criado(s), {result.Updated} alterado(s), {result.Unchanged} igual(is).");
        return 0;
    }
}
