using Estudarias.Infrastructure.Persistence.InMemory;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Estudarias.Api.FunctionalTests.Infrastructure;

/// <summary>Sobe a Api em memória e expõe os repositórios para os testes popularem.</summary>
public sealed class EstudariasApiFactory : WebApplicationFactory<Program>
{
    public InMemoryTopicRepository Topics => Services.GetRequiredService<InMemoryTopicRepository>();
    public InMemoryStudentRepository Students => Services.GetRequiredService<InMemoryStudentRepository>();
}
