using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Lessons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercise", table =>
            table.HasCheckConstraint("ck_exercise_difficulty", "difficulty IN ('easy', 'medium', 'hard')"));

        builder.HasKey(exercise => exercise.Id);
        builder.Property(exercise => exercise.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(exercise => exercise.LessonId).HasColumnName("lesson_id");
        builder.HasOne<Lesson>().WithMany().HasForeignKey(exercise => exercise.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(exercise => exercise.LessonId).HasDatabaseName("ix_exercise_lesson_id");

        builder.Property(exercise => exercise.Statement).HasColumnName("statement").IsRequired();

        // Gravada em minúsculas, com a conversão explícita (como no student): renomear um membro do enum não muda o texto no banco,
        // e a constraint da tabela recusa qualquer outro valor.
        builder.Property(exercise => exercise.Difficulty)
            .HasColumnName("difficulty")
            .HasMaxLength(10)
            .HasConversion(difficulty => ToText(difficulty), value => FromText(value))
            .IsRequired();

        // As alternativas são parte do agregado: carregam e gravam junto com o exercício, na tabela própria.
        builder.OwnsMany(exercise => exercise.Alternatives, alternative =>
        {
            alternative.ToTable("alternative", table =>
                table.HasCheckConstraint(
                    "ck_alternative_error_type",
                    "(is_correct AND error_type_id IS NULL) OR (NOT is_correct AND error_type_id IS NOT NULL)"));

            alternative.WithOwner().HasForeignKey("ExerciseId");
            alternative.Property<Guid>("ExerciseId").HasColumnName("exercise_id");
            // Dois índices na mesma coluna (este e o parcial da correta), então cada um tem nome no modelo: sem nome, o EF Core
            // trataria os dois como o mesmo índice, e o segundo HasIndex reconfiguraria o primeiro.
            alternative.HasIndex(["ExerciseId"], "ix_alternative_exercise_id");

            alternative.HasKey(value => value.Id);
            alternative.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();

            alternative.Property(value => value.Text).HasColumnName("text").IsRequired();
            alternative.Property(value => value.IsCorrect).HasColumnName("is_correct");

            alternative.Property(value => value.ErrorTypeId).HasColumnName("error_type_id");
            alternative.HasOne<ErrorType>().WithMany().HasForeignKey(value => value.ErrorTypeId).OnDelete(DeleteBehavior.Restrict);
            alternative.HasIndex(value => value.ErrorTypeId).HasDatabaseName("ix_alternative_error_type_id");

            // No máximo uma correta por exercício. O Domain já exige exatamente uma; o índice parcial é a segunda barreira.
            alternative.HasIndex(["ExerciseId"], "ux_alternative_exercise_id_correct")
                .IsUnique()
                .HasFilter("is_correct");

            // Conteúdo não some em cascata: tentativas apontam para as alternativas.
            alternative.OwnedEntityType.FindOwnership()!.DeleteBehavior = DeleteBehavior.Restrict;
        });

        // A coleção pública é só leitura; o EF Core preenche o campo do agregado.
        builder.Navigation(exercise => exercise.Alternatives).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static string ToText(Difficulty difficulty) =>
        difficulty switch
        {
            Difficulty.Easy => "easy",
            Difficulty.Medium => "medium",
            Difficulty.Hard => "hard",
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown difficulty."),
        };

    private static Difficulty FromText(string value) =>
        value switch
        {
            "easy" => Difficulty.Easy,
            "medium" => Difficulty.Medium,
            "hard" => Difficulty.Hard,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown difficulty."),
        };
}
