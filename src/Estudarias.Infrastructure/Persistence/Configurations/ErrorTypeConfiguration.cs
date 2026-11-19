using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class ErrorTypeConfiguration : IEntityTypeConfiguration<ErrorType>
{
    public void Configure(EntityTypeBuilder<ErrorType> builder)
    {
        builder.ToTable("error_type");

        builder.HasKey(errorType => errorType.Id);
        builder.Property(errorType => errorType.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(errorType => errorType.TopicId).HasColumnName("topic_id");
        builder.HasOne<Topic>().WithMany().HasForeignKey(errorType => errorType.TopicId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(errorType => errorType.Code)
            .HasColumnName("code")
            .HasMaxLength(ErrorType.CodeMaxLength)
            .IsRequired();

        // O código é único dentro do tópico, e é a chave natural do tipo de erro na carga do conteúdo. O índice começa pelo
        // topic_id, então também serve à chave estrangeira.
        builder.HasIndex(errorType => new { errorType.TopicId, errorType.Code })
            .IsUnique()
            .HasDatabaseName("ux_error_type_topic_id_code");

        builder.Property(errorType => errorType.Name)
            .HasColumnName("name")
            .HasMaxLength(ErrorType.NameMaxLength)
            .IsRequired();

        builder.Property(errorType => errorType.Description).HasColumnName("description");
        builder.Property(errorType => errorType.Feedback).HasColumnName("feedback").IsRequired();
    }
}
