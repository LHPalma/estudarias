using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    /// <summary>O campo do agregado com os pré-requisitos, mapeado como coleção própria (não há propriedade pública dela).</summary>
    internal const string PrerequisitesField = "_prerequisites";

    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topic");

        builder.HasKey(topic => topic.Id);
        builder.Property(topic => topic.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(topic => topic.Name)
            .HasColumnName("name")
            .HasMaxLength(Topic.NameMaxLength)
            .IsRequired();

        // O slug é a chave natural do tópico: é por ele que a carga do conteúdo acha o tópico que já existe.
        builder.Property(topic => topic.Slug)
            .HasColumnName("slug")
            .HasMaxLength(Topic.SlugMaxLength)
            .IsRequired();
        builder.HasIndex(topic => topic.Slug).IsUnique().HasDatabaseName("ux_topic_slug");

        // Os ids públicos (Prerequisites) são uma visão do campo; o que vai para o banco é o campo, uma linha por pré-requisito.
        builder.Ignore(topic => topic.Prerequisites);
        builder.OwnsMany<TopicPrerequisite>(PrerequisitesField, prerequisite =>
        {
            prerequisite.ToTable("topic_prerequisite", table =>
                table.HasCheckConstraint("ck_topic_prerequisite_not_self", "topic_id <> prerequisite_id"));

            prerequisite.WithOwner().HasForeignKey("TopicId");
            prerequisite.Property<Guid>("TopicId").HasColumnName("topic_id");
            prerequisite.Property(value => value.PrerequisiteId).HasColumnName("prerequisite_id");
            prerequisite.HasKey("TopicId", nameof(TopicPrerequisite.PrerequisiteId));

            prerequisite.HasOne<Topic>().WithMany().HasForeignKey(value => value.PrerequisiteId).OnDelete(DeleteBehavior.Restrict);
            prerequisite.HasIndex(value => value.PrerequisiteId).HasDatabaseName("ix_topic_prerequisite_prerequisite_id");

            // A ligação com o dono (topic_id) fica em cascata, o padrão de uma coleção owned: as linhas são parte do agregado. O
            // snapshot das migrations não sabe registrar outro comportamento nessa ligação, e trocá-lo deixaria o modelo sempre
            // "com mudanças pendentes". Um tópico exigido por outro continua protegido pela FK de prerequisite_id, que é RESTRICT.
        });
    }
}
