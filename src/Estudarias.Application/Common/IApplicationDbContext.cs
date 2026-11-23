using Estudarias.Domain.Attempts;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;

namespace Estudarias.Application.Common;

/// <summary>
/// O banco visto só para leitura: cada agregado como <see cref="IQueryable{T}"/>, sem <c>SaveChanges</c>, <c>Add</c> nem rastreamento.
/// As consultas (as classes <c>*Reads</c>) montam o LINQ por cima e materializam o resultado. A escrita não passa por aqui: segue
/// pelos repositórios e pela <see cref="IUnitOfWork"/> (veja o ADR-002).
/// </summary>
public interface IApplicationDbContext
{
    IQueryable<Student> Students { get; }

    IQueryable<User> Users { get; }

    /// <summary>
    /// Os tópicos. Os pré-requisitos não têm propriedade mapeada (<c>Topic.Prerequisites</c> é uma visão em memória); numa consulta,
    /// leia-os com <c>EF.Property&lt;IEnumerable&lt;TopicPrerequisite&gt;&gt;(topic, "_prerequisites")</c>.
    /// </summary>
    IQueryable<Topic> Topics { get; }

    IQueryable<ErrorType> ErrorTypes { get; }

    IQueryable<Lesson> Lessons { get; }

    /// <summary>Os exercícios, com as alternativas (parte do agregado, carregadas junto).</summary>
    IQueryable<Exercise> Exercises { get; }

    IQueryable<Flashcard> Flashcards { get; }

    /// <summary>As tentativas: a fonte da nota, do histórico e do estado da lição.</summary>
    IQueryable<Attempt> Attempts { get; }

    /// <summary>Os cartões de revisão dos alunos: a fonte das revisões do dia.</summary>
    IQueryable<StudentFlashcard> StudentFlashcards { get; }
}
