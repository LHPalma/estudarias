using Estudarias.Domain.Students;
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

        // O progresso por tópico (student_topic) depende da tabela de tópicos e entra com o mapeamento do Topic.
        builder.Ignore(student => student.Progress);
    }
}
