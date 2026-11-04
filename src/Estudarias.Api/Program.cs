using System.Text.Json;
using System.Text.Json.Serialization;
using Estudarias.Api.Authentication;
using Estudarias.Application.Common.Abstractions;
using Estudarias.Application.Topics.GetTopic;
using Estudarias.Infrastructure;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services
    .AddAuthentication(StudentHeaderAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, StudentHeaderAuthenticationHandler>(
        StudentHeaderAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentStudent, HttpContextCurrentStudent>();

builder.Services.AddScoped<GetTopicHandler>();
builder.Services.AddInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
