using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // "user" é palavra reservada do PostgreSQL (SELECT * FROM user devolve o usuário do banco, não a tabela), então a tabela é
        // app_user e nenhum SQL escrito à mão precisa de aspas.
        builder.ToTable("app_user", table =>
            table.HasCheckConstraint("ck_app_user_role", "role IN ('student')"));

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();

        // O e-mail já chega normalizado (aparado e em minúsculas), então o índice único basta para a unicidade.
        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(User.MaxEmailLength)
            .IsRequired();
        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("ux_app_user_email");

        // O hash e a versão do pepper que o derivou viajam juntos, mas ficam em duas colunas.
        builder.ComplexProperty(user => user.PasswordHash, hash =>
        {
            hash.Property(value => value.Value).HasColumnName("password_hash").IsRequired();
            hash.Property(value => value.PepperVersion).HasColumnName("password_pepper_version");
        });

        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .HasConversion(role => "student", value => UserRole.Student)
            .IsRequired();

        builder.Property(user => user.IsActive).HasColumnName("is_active").IsRequired();

        // Uma conta por aluno. Colunas nulas não colidem no índice único, então contas sem aluno (papéis futuros) cabem.
        builder.Property(user => user.StudentId).HasColumnName("student_id");
        builder.HasOne<Student>().WithMany().HasForeignKey(user => user.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(user => user.StudentId).IsUnique().HasDatabaseName("ux_app_user_student_id");
    }
}
