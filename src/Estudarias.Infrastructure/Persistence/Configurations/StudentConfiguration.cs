using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("student", table =>
            table.HasCheckConstraint("ck_student_experimental_group", "experimental_group IN ('control', 'diagnostic')"));

        builder.HasKey(student => student.Id);
        builder.Property(student => student.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(student => student.Name)
            .HasColumnName("name")
            .HasMaxLength(Student.NameMaxLength)
            .IsRequired();

        // Gravado em minúsculas, com o nome do enum. A conversão é explícita, então renomear um membro do enum sem querer não muda
        // o texto no banco, e a constraint da tabela recusa qualquer outro valor.
        builder.Property(student => student.Group)
            .HasColumnName("experimental_group")
            .HasMaxLength(20)
            .HasConversion(
                group => group == ExperimentalGroup.Control ? "control" : "diagnostic",
                value => value == "control" ? ExperimentalGroup.Control : ExperimentalGroup.Diagnostic)
            .IsRequired();

        // O progresso por tópico é parte do agregado: carrega e grava junto com o aluno. A linha só existe a partir do desbloqueio
        // (tópico sem linha está bloqueado), e o status não é coluna: deriva de mastered_at.
        builder.OwnsMany(student => student.Progress, progress =>
        {
            progress.ToTable("student_topic", table =>
                table.HasCheckConstraint(
                    "ck_student_topic_mastered_after_unlocked", "mastered_at IS NULL OR mastered_at >= unlocked_at"));

            progress.WithOwner().HasForeignKey("StudentId");
            progress.Property<Guid>("StudentId").HasColumnName("student_id");

            progress.HasKey(value => value.Id);
            progress.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();

            progress.Property(value => value.TopicId).HasColumnName("topic_id");
            progress.HasOne<Topic>().WithMany().HasForeignKey(value => value.TopicId).OnDelete(DeleteBehavior.Restrict);
            progress.HasIndex(value => value.TopicId).HasDatabaseName("ix_student_topic_topic_id");

            // Um progresso por aluno e tópico. Desbloqueios simultâneos do mesmo tópico: o segundo é recusado pelo banco. O índice
            // começa pelo student_id, então também serve à ligação com o aluno.
            progress.HasIndex("StudentId", nameof(TopicProgress.TopicId))
                .IsUnique()
                .HasDatabaseName("ux_student_topic_student_id_topic_id");

            progress.Property(value => value.UnlockedAt).HasColumnName("unlocked_at");
            progress.Property(value => value.MasteredAt).HasColumnName("mastered_at");
            progress.Ignore(value => value.Status);
        });

        builder.Navigation(student => student.Progress).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
