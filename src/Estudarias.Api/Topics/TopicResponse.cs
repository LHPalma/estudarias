using Estudarias.Api.Links;
using Estudarias.Domain.Students;

namespace Estudarias.Api.Topics;

public sealed record TopicResponse(Guid Id, string Name, string Slug, TopicStatus Status, IReadOnlyList<Link> Links);
