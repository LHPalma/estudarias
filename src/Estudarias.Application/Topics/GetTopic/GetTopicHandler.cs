using Estudarias.Application.Common.Abstractions;
using Estudarias.Application.Students;

namespace Estudarias.Application.Topics.GetTopic;

public sealed class GetTopicHandler(
    ITopicRepository topics,
    IStudentRepository students,
    ICurrentStudent currentStudent)
{
    /// <returns>O tópico com o status do aluno atual, ou <c>null</c> se o tópico ou o aluno não existem.</returns>
    public async Task<TopicDetails?> HandleAsync(Guid topicId, CancellationToken cancellationToken)
    {
        var topic = await topics.GetByIdAsync(topicId, cancellationToken);
        if (topic is null)
        {
            return null;
        }

        var student = await students.GetByIdAsync(currentStudent.Id, cancellationToken);
        if (student is null)
        {
            return null;
        }

        return new TopicDetails(topic.Id, topic.Name, topic.Slug, student.StatusOf(topic.Id));
    }
}
