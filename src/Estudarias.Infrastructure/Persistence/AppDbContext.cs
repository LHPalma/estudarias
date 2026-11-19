using Estudarias.Application.Common;
using Estudarias.Domain.ErrorTypes;
using Estudarias.Domain.Exercises;
using Estudarias.Domain.Flashcards;
using Estudarias.Domain.Identity;
using Estudarias.Domain.Lessons;
using Estudarias.Domain.Students;
using Estudarias.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Student> Students => Set<Student>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // O conteúdo: só a carga do seed grava (#85), e o resto do sistema só lê (RN-CNT-14).
    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<ErrorType> ErrorTypes => Set<ErrorType>();

    public DbSet<Lesson> Lessons => Set<Lesson>();

    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<Flashcard> Flashcards => Set<Flashcard>();

    // A visão de leitura do banco (IApplicationDbContext): sem rastreamento, então uma leitura nunca deixa entidade no contexto.
    IQueryable<Student> IApplicationDbContext.Students => Students.AsNoTracking();

    IQueryable<User> IApplicationDbContext.Users => Users.AsNoTracking();

    IQueryable<Topic> IApplicationDbContext.Topics => Topics.AsNoTracking();

    IQueryable<ErrorType> IApplicationDbContext.ErrorTypes => ErrorTypes.AsNoTracking();

    IQueryable<Lesson> IApplicationDbContext.Lessons => Lessons.AsNoTracking();

    IQueryable<Exercise> IApplicationDbContext.Exercises => Exercises.AsNoTracking();

    IQueryable<Flashcard> IApplicationDbContext.Flashcards => Flashcards.AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
