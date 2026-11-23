using Estudarias.Api.Commands;
using Estudarias.Api.Cors;
using Estudarias.Api.ErrorHandling;
using Estudarias.Api.Security;
using Estudarias.Application;
using Estudarias.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSecurity(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Modo comando: carrega o conteúdo e termina, sem subir o servidor (dotnet run --project src/Estudarias.Api -- seed "$PWD/content").
// O caminho é absoluto porque o dotnet run executa o programa com a pasta do projeto como diretório de trabalho.
if (SeedCommand.IsRequested(args))
{
    Environment.ExitCode = await SeedCommand.RunAsync(app.Services, args, Console.Out);
    return;
}

if (app.Environment.IsDevelopment())
{
    // Só existe em Development; aberto de propósito, senão a política padrão (login obrigatório) esconderia a documentação.
    app.MapOpenApi().AllowAnonymous();
}

// O primeiro do pipeline: traduz as exceções de domínio de qualquer camada de baixo em ProblemDetails.
app.UseExceptionHandler();

app.UseHttpsRedirection();

// Antes da autenticação: o preflight (OPTIONS) do navegador não leva token, e o CORS o responde sem passar pelo login.
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Visível aos testes funcionais, que sobem a API inteira com o WebApplicationFactory.
public partial class Program;
