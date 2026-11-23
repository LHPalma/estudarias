using Estudarias.Domain.Attempts;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("attempt", table =>
            table.HasCheckConstraint("ck_attempt_response_seconds", "response_seconds IS NULL OR response_seconds >= 0"));

        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(attempt => attempt.StudentId).HasColumnName("student_id");
        builder.HasOne<Student>().WithMany().HasForeignKey(attempt => attempt.StudentId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(attempt => attempt.ExerciseId).HasColumnName("exercise_id");
        builder.HasOne<Exercise>().WithMany().HasForeignKey(attempt => attempt.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(attempt => attempt.ExerciseId).HasDatabaseName("ix_attempt_exercise_id");

        // A chave estrangeira para alternative fica na migration (SQL), e não aqui: a alternativa é parte do agregado Exercise
        // (owned), e o EF Core não deixa outra entidade apontar para uma entidade owned. O índice serve a essa chave.
        builder.Property(attempt => attempt.AlternativeId).HasColumnName("alternative_id");
        builder.HasIndex(attempt => attempt.AlternativeId).HasDatabaseName("ix_attempt_alternative_id");

        builder.Property(attempt => attempt.ResponseSeconds).HasColumnName("response_seconds");
        builder.Property(attempt => attempt.AnsweredAt).HasColumnName("answered_at");

        // A nota lê as últimas tentativas do aluno nos exercícios do tópico; o histórico e o estado da lição também. Sem unicidade:
        // o aluno responde o mesmo exercício quantas vezes quiser. O índice começa pelo student_id, então serve à ligação com o aluno.
        builder.HasIndex(attempt => new { attempt.StudentId, attempt.ExerciseId })
            .HasDatabaseName("ix_attempt_student_id_exercise_id");
    }
}
