using Estudarias.Domain.Attempts;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.StudentFlashcards;
using Estudarias.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class StudentFlashcardConfiguration : IEntityTypeConfiguration<StudentFlashcard>
{
    public const int AlgorithmMaxLength = 20;

    public void Configure(EntityTypeBuilder<StudentFlashcard> builder)
    {
        builder.ToTable("student_flashcard");

        builder.HasKey(card => card.Id);
        builder.Property(card => card.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(card => card.StudentId).HasColumnName("student_id");
        builder.HasOne<Student>().WithMany().HasForeignKey(card => card.StudentId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(card => card.FlashcardId).HasColumnName("flashcard_id");
        builder.HasOne<Flashcard>().WithMany().HasForeignKey(card => card.FlashcardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(card => card.FlashcardId).HasDatabaseName("ix_student_flashcard_flashcard_id");

        // Um cartão por aluno e flashcard (RN-REV-12): o erro repetido não cria outro cartão. O índice começa pelo student_id, então
        // também serve à ligação com o aluno.
        builder.HasIndex(card => new { card.StudentId, card.FlashcardId })
            .IsUnique()
            .HasDatabaseName("ux_student_flashcard_student_id_flashcard_id");

        builder.Property(card => card.OriginAttemptId).HasColumnName("origin_attempt_id");
        builder.HasOne<Attempt>().WithMany().HasForeignKey(card => card.OriginAttemptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(card => card.OriginAttemptId).HasDatabaseName("ix_student_flashcard_origin_attempt_id");

        builder.Property(card => card.Algorithm)
            .HasColumnName("algorithm")
            .HasMaxLength(AlgorithmMaxLength)
            .IsRequired();

        // O estado é opaco para o Domain (ADR-003): só o adapter do algoritmo o escreve e o lê. Em jsonb, o PostgreSQL valida que é
        // JSON e o deixa consultável pela análise do experimento; ele normaliza o texto (ordem das chaves, espaços), então o que volta
        // é o mesmo JSON, não necessariamente o mesmo texto.
        builder.Property(card => card.SchedulingState)
            .HasColumnName("scheduling_state")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(card => card.NextReviewAt).HasColumnName("next_review_at");

        // As revisões do dia: os cartões do aluno com next_review_at vencido, do mais atrasado ao mais recente.
        builder.HasIndex(card => new { card.StudentId, card.NextReviewAt })
            .HasDatabaseName("ix_student_flashcard_student_id_next_review_at");
    }
}
