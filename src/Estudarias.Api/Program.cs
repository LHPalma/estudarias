using Estudarias.Api.Security;
using Estudarias.Application;
using Estudarias.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSecurity(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Só existe em Development; aberto de propósito, senão a política padrão (login obrigatório) esconderia a documentação.
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Visível aos testes funcionais, que sobem a API inteira com o WebApplicationFactory.
public partial class Program;
