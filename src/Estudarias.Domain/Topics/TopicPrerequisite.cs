namespace Estudarias.Domain.Topics;

/// <summary>
/// Um pré-requisito do tópico: o id do tópico exigido. É um tipo próprio para a persistência gravar cada pré-requisito numa linha
/// de <c>topic_prerequisite</c>, com chave e chave estrangeira; quem usa o <see cref="Topic"/> enxerga só os ids
/// (<see cref="Topic.Prerequisites"/>).
/// </summary>
public sealed class TopicPrerequisite
{
    internal TopicPrerequisite(Guid prerequisiteId) => PrerequisiteId = prerequisiteId;

    public Guid PrerequisiteId { get; }
}
