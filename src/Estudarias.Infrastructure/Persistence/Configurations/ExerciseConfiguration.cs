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
        {
            table.HasCheckConstraint("ck_exercise_difficulty", "difficulty IN ('easy', 'medium', 'hard')");
            table.HasCheckConstraint("ck_exercise_position", "position >= 1");
        });

        builder.HasKey(exercise => exercise.Id);
        builder.Property(exercise => exercise.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(exercise => exercise.LessonId).HasColumnName("lesson_id");
        builder.HasOne<Lesson>().WithMany().HasForeignKey(exercise => exercise.LessonId).OnDelete(DeleteBehavior.Restrict);
        // O código é a chave natural do exercício na carga do conteúdo. A carga exige o código único no tópico inteiro (o exercício
        // pode mudar de lição sem perder o id); o banco garante o que consegue sem a coluna do tópico, o código único na lição. O
        // índice começa pelo lesson_id, então também serve à chave estrangeira.
        builder.Property(exercise => exercise.Code)
            .HasColumnName("code")
            .HasMaxLength(Exercise.CodeMaxLength)
            .IsRequired();
        builder.HasIndex(exercise => new { exercise.LessonId, exercise.Code })
            .IsUnique()
            .HasDatabaseName("ux_exercise_lesson_id_code");

        // A ordem do exercício na lição, como a da lição no tópico: o id não serve de ordem (um Guid v7 não cresce dentro de uma
        // rajada de criação). Sem índice único: a recarga do conteúdo pode trocar duas posições, e uma restrição de unicidade
        // recusaria a troca no meio da gravação.
        builder.Property(exercise => exercise.Position).HasColumnName("position");

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
            {
                table.HasCheckConstraint(
                    "ck_alternative_error_type",
                    "(is_correct AND error_type_id IS NULL) OR (NOT is_correct AND error_type_id IS NOT NULL)");
                table.HasCheckConstraint("ck_alternative_position", "position >= 1");
            });

            alternative.WithOwner().HasForeignKey("ExerciseId");
            alternative.Property<Guid>("ExerciseId").HasColumnName("exercise_id");
            // Dois índices na mesma coluna (este e o parcial da correta), então cada um tem nome no modelo: sem nome, o EF Core
            // trataria os dois como o mesmo índice, e o segundo HasIndex reconfiguraria o primeiro.
            alternative.HasIndex(["ExerciseId"], "ix_alternative_exercise_id");

            alternative.HasKey(value => value.Id);
            alternative.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();

            alternative.Property(value => value.Text).HasColumnName("text").IsRequired();

            // A ordem das alternativas no exercício, escolhida pelo autor do conteúdo (veja Exercise.Position).
            alternative.Property(value => value.Position).HasColumnName("position");
            alternative.Property(value => value.IsCorrect).HasColumnName("is_correct");

            alternative.Property(value => value.ErrorTypeId).HasColumnName("error_type_id");
            alternative.HasOne<ErrorType>().WithMany().HasForeignKey(value => value.ErrorTypeId).OnDelete(DeleteBehavior.Restrict);
            alternative.HasIndex(value => value.ErrorTypeId).HasDatabaseName("ix_alternative_error_type_id");

            // No máximo uma correta por exercício. O Domain já exige exatamente uma; o índice parcial é a segunda barreira.
            alternative.HasIndex(["ExerciseId"], "ux_alternative_exercise_id_correct")
                .IsUnique()
                .HasFilter("is_correct");

            // A ligação com o exercício (exercise_id) fica em cascata, o padrão de uma coleção owned: as alternativas são parte do
            // agregado. O snapshot das migrations não sabe registrar outro comportamento nessa ligação. Quem protege as alternativas
            // de sumir são as FKs que apontam para elas (a tentativa, na #84), que são RESTRICT.
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
