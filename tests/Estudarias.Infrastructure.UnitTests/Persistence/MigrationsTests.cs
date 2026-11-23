using Estudarias.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Estudarias.Infrastructure.UnitTests.Persistence;

public class MigrationsTests
{
    // Compara o modelo das configurações com o snapshot da última migration, sem abrir conexão: a connection string nunca é usada.
    // Sem este teste, um mapeamento sem migration (ou que o snapshot não consegue registrar) só aparece quando os testes de
    // integração tentam migrar o banco, e só onde há Docker.
    [Fact(DisplayName = "O modelo do EF Core bate com a última migration: não há mudança sem migration")]
    public void Model_HasNoPendingChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").Options;
        using var context = new AppDbContext(options);

        // Act
        var pending = context.Database.HasPendingModelChanges();

        // Assert
        Assert.False(pending);
    }
}
