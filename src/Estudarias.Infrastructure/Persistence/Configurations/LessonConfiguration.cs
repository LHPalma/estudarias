using Estudarias.Domain.Lessons;
using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        // "order" é palavra reservada do SQL, então a coluna da ordem se chama position, e nenhum SQL escrito à mão precisa de aspas
        // (o mesmo motivo do app_user).
        builder.ToTable("lesson", table => table.HasCheckConstraint("ck_lesson_position", "position >= 1"));

        builder.HasKey(lesson => lesson.Id);
        builder.Property(lesson => lesson.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(lesson => lesson.TopicId).HasColumnName("topic_id");
        builder.HasOne<Topic>().WithMany().HasForeignKey(lesson => lesson.TopicId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(lesson => lesson.Order).HasColumnName("position");

        // A ordem é única dentro do tópico, e é a chave natural da lição na carga do conteúdo. O índice começa pelo topic_id, então
        // também serve à chave estrangeira.
        builder.HasIndex(lesson => new { lesson.TopicId, lesson.Order })
            .IsUnique()
            .HasDatabaseName("ux_lesson_topic_id_position");

        builder.Property(lesson => lesson.Title)
            .HasColumnName("title")
            .HasMaxLength(Lesson.TitleMaxLength)
            .IsRequired();

        builder.Property(lesson => lesson.Content).HasColumnName("content").IsRequired();
    }
}
