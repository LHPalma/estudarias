using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class FlashcardConfiguration : IEntityTypeConfiguration<Flashcard>
{
    public void Configure(EntityTypeBuilder<Flashcard> builder)
    {
        // A origem e o tipo de erro andam juntos: o gerado por erro tem tipo de erro, e o manual não. O Domain garante o mesmo por
        // construção.
        builder.ToTable("flashcard", table =>
        {
            table.HasCheckConstraint("ck_flashcard_origin", "origin IN ('manual', 'generated_by_error')");
            table.HasCheckConstraint(
                "ck_flashcard_error_type", "(origin = 'generated_by_error') = (error_type_id IS NOT NULL)");
        });

        builder.HasKey(flashcard => flashcard.Id);
        builder.Property(flashcard => flashcard.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(flashcard => flashcard.TopicId).HasColumnName("topic_id");
        builder.HasOne<Topic>().WithMany().HasForeignKey(flashcard => flashcard.TopicId).OnDelete(DeleteBehavior.Restrict);
        // O código é a chave natural do flashcard na carga do conteúdo, único no tópico. O índice começa pelo topic_id, então também
        // serve à chave estrangeira.
        builder.Property(flashcard => flashcard.Code)
            .HasColumnName("code")
            .HasMaxLength(Flashcard.CodeMaxLength)
            .IsRequired();
        builder.HasIndex(flashcard => new { flashcard.TopicId, flashcard.Code })
            .IsUnique()
            .HasDatabaseName("ux_flashcard_topic_id_code");

        builder.Property(flashcard => flashcard.Question).HasColumnName("question").IsRequired();
        builder.Property(flashcard => flashcard.Answer).HasColumnName("answer").IsRequired();

        builder.Property(flashcard => flashcard.Origin)
            .HasColumnName("origin")
            .HasMaxLength(20)
            .HasConversion(origin => ToText(origin), value => FromText(value))
            .IsRequired();

        builder.Property(flashcard => flashcard.ErrorTypeId).HasColumnName("error_type_id");
        builder.HasOne<ErrorType>().WithMany().HasForeignKey(flashcard => flashcard.ErrorTypeId).OnDelete(DeleteBehavior.Restrict);

        // Um flashcard gerado por tipo de erro, global (não por aluno): é ele que a revisão entrega a quem comete aquele erro. O
        // índice parcial também serve à chave estrangeira, porque o manual nunca tem tipo de erro.
        builder.HasIndex(flashcard => flashcard.ErrorTypeId)
            .IsUnique()
            .HasFilter("origin = 'generated_by_error'")
            .HasDatabaseName("ux_flashcard_error_type_id_generated");
    }

    private static string ToText(FlashcardOrigin origin) =>
        origin switch
        {
            FlashcardOrigin.Manual => "manual",
            FlashcardOrigin.GeneratedByError => "generated_by_error",
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown flashcard origin."),
        };

    private static FlashcardOrigin FromText(string value) =>
        value switch
        {
            "manual" => FlashcardOrigin.Manual,
            "generated_by_error" => FlashcardOrigin.GeneratedByError,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown flashcard origin."),
        };
}
