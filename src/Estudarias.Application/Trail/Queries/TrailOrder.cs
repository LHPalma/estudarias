namespace Estudarias.Application.Trail.Queries;

/// <summary>
/// A ordem em que a trilha lista os tópicos: pela profundidade no grafo de pré-requisitos (a raiz primeiro, depois o que depende só
/// dela, e assim por diante) e, dentro da mesma profundidade, pelo nome. Um tópico nunca vem antes de um pré-requisito dele.
/// </summary>
internal static class TrailOrder
{
    public static IReadOnlyList<T> Sort<T>(
        IReadOnlyCollection<T> topics,
        Func<T, Guid> idOf,
        Func<T, string> nameOf,
        Func<T, IReadOnlyCollection<Guid>> prerequisitesOf)
    {
        var byId = topics.ToDictionary(idOf);
        var depths = new Dictionary<Guid, int>();

        int DepthOf(T topic, HashSet<Guid> path)
        {
            var id = idOf(topic);
            if (depths.TryGetValue(id, out var known))
            {
                return known;
            }

            // Um ciclo não deveria existir (a carga do conteúdo o recusa), mas a leitura não pode travar por causa dele: o tópico
            // que fecha o ciclo conta como raiz, e a ordem sai sem repetir nem perder nenhum tópico.
            if (!path.Add(id))
            {
                return 0;
            }

            var depth = 0;
            foreach (var requisiteId in prerequisitesOf(topic).Where(byId.ContainsKey))
            {
                depth = Math.Max(depth, 1 + DepthOf(byId[requisiteId], path));
            }

            path.Remove(id);
            depths[id] = depth;
            return depth;
        }

        return [.. topics
            .OrderBy(topic => DepthOf(topic, []))
            .ThenBy(nameOf, StringComparer.InvariantCultureIgnoreCase)];
    }
}
