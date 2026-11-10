using Estudarias.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estudarias.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    // O hash SHA-256 em hexadecimal minúsculo tem sempre 64 caracteres.
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_token");

        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(token => token.UserId).HasDatabaseName("ix_refresh_token_user_id");

        builder.Property(token => token.FamilyId).HasColumnName("family_id");
        builder.HasIndex(token => token.FamilyId).HasDatabaseName("ix_refresh_token_family_id");

        // Só o hash é guardado, e a busca do refresh é por ele.
        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(TokenHashLength)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("ux_refresh_token_token_hash");

        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");

        // Token de concorrência: de dois usos simultâneos do mesmo token, só o UPDATE do primeiro acha a linha ainda não revogada.
        builder.Property(token => token.RevokedAt).HasColumnName("revoked_at").IsConcurrencyToken();

        builder.Property(token => token.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
    }
}
