using Estudarias.Domain.Common.Exceptions;
using Estudarias.Domain.Students;
using Estudarias.Infrastructure.IntegrationTests.Support;

namespace Estudarias.Infrastructure.IntegrationTests.Identity;

/// <summary>Cadastro, de ponta a ponta contra o PostgreSQL real. Os requisitos citados são os do SRS do módulo (#48).</summary>
[Collection(PostgresCollection.Name)]
public class RegisterFlowTests(PostgresFixture postgres) : AuthenticationTests(postgres)
{
    [Fact(DisplayName = "RF-01 / RN-14: o cadastro grava o aluno e a conta ligados, com o nome aparado e o e-mail normalizado")]
    public async Task Register_StoresTheLinkedStudentAndAccount()
    {
        // Act
        var payload = await RegisterAsync("  Ana@Example.COM ", "  Ana  ");

        // Assert
        Assert.Equal(1, await CountAsync("student"));
        Assert.Equal(1, await CountAsync("app_user"));
        Assert.Equal("Ana", await ScalarAsync<string>("SELECT name FROM student"));
        Assert.Equal("ana@example.com", await ScalarAsync<string>("SELECT email FROM app_user"));
        Assert.True(await ScalarAsync<bool>("SELECT u.student_id = s.id FROM app_user u, student s"));
        Assert.Equal(payload.User.Id, await ScalarAsync<Guid>("SELECT id FROM app_user"));
    }

    [Fact(DisplayName = "RF-01: o cadastro já devolve uma sessão aberta, que renova")]
    public async Task Register_ReturnsASessionThatRenews()
    {
        // Act
        var registered = await RegisterAsync();
        var renewed = await RefreshAsync(registered.RefreshToken);

        // Assert
        Assert.StartsWith("access-token-for-", registered.AccessToken, StringComparison.Ordinal);
        Assert.Equal(Clock.GetUtcNow().AddMinutes(15), registered.AccessTokenExpiresAt);
        Assert.NotEqual(registered.RefreshToken, renewed.RefreshToken);
    }

    [Fact(DisplayName = "RF-08 / RN-04: a senha vira um hash argon2id, e nem a senha nem o pepper aparecem no banco")]
    public async Task Register_StoresAnArgon2idHashWithoutThePasswordOrThePepper()
    {
        // Act
        await RegisterAsync();

        // Assert
        var hash = await ScalarAsync<string>("SELECT password_hash FROM app_user");
        Assert.StartsWith("$argon2id$", hash, StringComparison.Ordinal);
        Assert.DoesNotContain(StrongPassword, hash, StringComparison.Ordinal);
        Assert.DoesNotContain(Samples.Pepper1, hash, StringComparison.Ordinal);
        Assert.Equal(1, await ScalarAsync<int>("SELECT password_pepper_version FROM app_user"));
    }

    [Fact(DisplayName = "RF-05 / RN-05: o refresh token é gravado só pelo hash SHA-256, nunca o valor cru")]
    public async Task Register_StoresTheRefreshTokenOnlyByHash()
    {
        // Act
        var payload = await RegisterAsync();

        // Assert
        Assert.Equal(1, await CountAsync("refresh_token"));
        Assert.Equal(HashOf(payload.RefreshToken), await ScalarAsync<string>("SELECT token_hash FROM refresh_token"));
        Assert.Equal(0, await CountAsync("refresh_token", $"token_hash = '{payload.RefreshToken}'"));
        Assert.True(await ScalarAsync<bool>("SELECT expires_at > now() + interval '29 days' FROM refresh_token"));
    }

    [Fact(DisplayName = "FA-01 / RN-02: e-mail já cadastrado é conflito, em qualquer caixa de letras, e nada novo é gravado")]
    public async Task Register_WithTakenEmail_ThrowsConflictAndWritesNothing()
    {
        // Arrange
        await RegisterAsync("ana@example.com");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainConflictException>(() => RegisterAsync("  ANA@example.com ", "Outra Ana"));
        Assert.Equal("identity.email.taken", exception.Code);
        Assert.Equal(1, await CountAsync("student"));
        Assert.Equal(1, await CountAsync("app_user"));
        Assert.Equal(1, await CountAsync("refresh_token"));
    }

    [Fact(DisplayName = "FA-02 / RN-14: cadastros simultâneos com o mesmo e-mail: um vence, os outros recebem conflito, e só uma conta fica gravada")]
    public async Task Register_WithConcurrentRegistrationsOfTheSameEmail_OnlyOneWins()
    {
        // Act
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(index => AttemptAsync(() => RegisterAsync("ana@example.com", $"Ana {index}"))));

        // Assert
        Assert.Single(outcomes, outcome => outcome.Succeeded);
        var failures = outcomes.Where(outcome => !outcome.Succeeded).ToList();
        Assert.Equal(3, failures.Count);
        Assert.All(failures, failure =>
            Assert.Equal("identity.email.taken", Assert.IsType<DomainConflictException>(failure.Error).Code));
        Assert.Equal(1, await CountAsync("student"));
        Assert.Equal(1, await CountAsync("app_user"));
        Assert.Equal(1, await CountAsync("refresh_token"));
    }

    [Theory(DisplayName = "RN-03 / FA-03: dado inválido é recusado e nada é gravado")]
    [InlineData("ana@example.com", "Ana", "curta1!", "identity.password.tooShort")]
    [InlineData("ana@example.com", "Ana", "senha-sem-maiuscula-1!", "identity.password.noUppercase")]
    [InlineData("isto-nao-e-email", "Ana", StrongPassword, "identity.email.invalid")]
    [InlineData("ana@example.com", "   ", StrongPassword, "student.name.required")]
    public async Task Register_WithInvalidData_ThrowsValidationAndWritesNothing(string email, string name, string password, string code)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => RegisterAsync(email, name, password));
        Assert.Equal(code, exception.Code);
        Assert.Equal(0, await CountAsync("student"));
        Assert.Equal(0, await CountAsync("app_user"));
        Assert.Equal(0, await CountAsync("refresh_token"));
    }

    [Theory(DisplayName = "RF-02 / FA-11: com a flag desligada, todo aluno novo cai no grupo de reserva configurado, sem sorteio")]
    [InlineData(ExperimentalGroup.Control, "control", "diagnostic")]
    [InlineData(ExperimentalGroup.Diagnostic, "diagnostic", "control")]
    public async Task Register_WithTheFlagOff_AssignsTheFallbackGroupToEveryone(
        ExperimentalGroup fallback, string expected, string other)
    {
        // Arrange
        using var provider = BuildProvider(settings =>
        {
            settings["ExperimentalGroup:RandomAssignment"] = "false";
            settings["ExperimentalGroup:FallbackGroup"] = fallback.ToString();
        });

        // Act
        for (var index = 0; index < 6; index++)
        {
            await RegisterAsync($"aluno{index}@example.com", provider: provider);
        }

        // Assert
        Assert.Equal(6, await CountAsync("student", $"experimental_group = '{expected}'"));
        Assert.Equal(0, await CountAsync("student", $"experimental_group = '{other}'"));
    }

    [Fact(DisplayName = "RF-02: com a flag ligada, o sorteio distribui os alunos pelos dois grupos")]
    public async Task Register_WithTheFlagOn_UsesBothGroups()
    {
        // Act: com 30 cadastros, a chance de todos caírem no mesmo grupo é de 2 em 2^30.
        for (var index = 0; index < 30; index++)
        {
            await RegisterAsync($"aluno{index}@example.com");
        }

        // Assert
        var control = await CountAsync("student", "experimental_group = 'control'");
        var diagnostic = await CountAsync("student", "experimental_group = 'diagnostic'");
        Assert.True(control > 0, "o sorteio nunca escolheu o grupo de controle");
        Assert.True(diagnostic > 0, "o sorteio nunca escolheu o grupo de diagnóstico");
        Assert.Equal(30, control + diagnostic);
    }

    [Fact(DisplayName = "RN-10: virar a flag não muda o grupo de quem já se cadastrou")]
    public async Task Register_WhenTheFlagIsTurnedOff_DoesNotChangeExistingStudents()
    {
        // Arrange
        await RegisterAsync("antes@example.com");
        var before = await ScalarAsync<string>("SELECT experimental_group FROM student");
        var fixedGroup = before == "control" ? ExperimentalGroup.Diagnostic : ExperimentalGroup.Control;
        using var provider = BuildProvider(settings =>
        {
            settings["ExperimentalGroup:RandomAssignment"] = "false";
            settings["ExperimentalGroup:FallbackGroup"] = fixedGroup.ToString();
        });

        // Act
        await RegisterAsync("depois@example.com", provider: provider);

        // Assert
        Assert.Equal(before, await ScalarAsync<string>(
            "SELECT s.experimental_group FROM student s JOIN app_user u ON u.student_id = s.id WHERE u.email = 'antes@example.com'"));
    }
}
