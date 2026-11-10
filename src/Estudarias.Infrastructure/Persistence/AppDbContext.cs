using Estudarias.Application.Common;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Student> Students => Set<Student>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // A visão de leitura do banco (IApplicationDbContext): sem rastreamento, então uma leitura nunca deixa entidade no contexto.
    IQueryable<Student> IApplicationDbContext.Students => Students.AsNoTracking();

    IQueryable<User> IApplicationDbContext.Users => Users.AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
