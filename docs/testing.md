# Convenções de testes

## Projetos

Um projeto de testes por camada, cada um em um passo próprio do CI, para o
vermelho apontar a camada.

| Projeto | Testa | Dependências |
|---|---|---|
| `Estudarias.Domain.UnitTests` | entidades, invariantes, cálculo do SM-2 | nenhuma: sem banco, sem I/O, sem relógio real |
| `Estudarias.Infrastructure.UnitTests` | hash de senha, validação das opções | nenhuma: sem banco, sem Docker |
| `Estudarias.Application.UnitTests` | handlers de commands, e a fronteira da Application (testes de arquitetura) | NSubstitute para repositórios e serviços |
| `Estudarias.Infrastructure.IntegrationTests` | mapeamento EF Core, consultas de leitura (`*Reads`), seed YAML | PostgreSQL real via Testcontainers |
| `Estudarias.Api.UnitTests` | emissor do token, validação das opções, usuário corrente | nenhuma: sem servidor, sem banco |
| `Estudarias.Api.FunctionalTests` | pipeline de autenticação e endpoints REST de ponta a ponta | `WebApplicationFactory`; Testcontainers quando o teste toca o banco |

Os projetos entram conforme a camada correspondente nasce. A pasta de cada
teste espelha a pasta da classe testada (`Domain/Common/BaseEntity.cs` →
`Domain.UnitTests/Common/BaseEntityTests.cs`).

## Nomes

- Classe: `<ClasseTestada>Tests`.
- Método: `Metodo_Cenario_Resultado`, em inglês
  (`Create_WithBlankName_ThrowsDomainValidationException`). O cenário é
  omitido quando não há variação (`Constructor_GeneratesVersion7Id`).
- Todo `[Fact]` e `[Theory]` tem `DisplayName` em pt-BR, escrito como a regra
  que o teste garante, não como o passo que ele executa
  ("Toda entidade nasce com um Guid v7").

## Estrutura

Arrange / Act / Assert, marcados por comentário. Seções vazias são omitidas,
e `// Act & Assert` junta as duas quando a ação é a própria asserção
(`Assert.Throws`).

## Erros de domínio

- O código é comparado **como literal**, nunca pela constante do catálogo:
  `Assert.Equal("topic.name.required", exception.Code)`. Comparar com
  `TopicErrors.NameRequired` compararia o símbolo com ele mesmo e deixaria um
  erro de digitação passar.
- Cada agregado declara seus códigos em uma classe estática
  `<Agregado>Errors`, na sua pasta, com constantes `public const string`
  (`StudentErrors.NameRequired = "student.name.required"`).
- `DomainErrorCatalogTests` varre o assembly do Domain atrás dessas classes e
  fixa, por reflexão, a lista completa de códigos, a unicidade e o formato
  `<área>.<conceito>.<invariante>`: três segmentos separados por ponto, cada
  um começando com letra minúscula e só com letras, em camelCase quando há mais
  de uma palavra (`student.name.tooLong`). Sem hífen, underscore nem número.
  Toda invariante nova quebra esse teste até o código ser registrado nele.
  É proposital.

## Tipos base

Tipos abstratos são testados uma vez, por uma implementação mínima privada
na própria classe de teste (`private sealed class FakeEntity : BaseEntity;`).
Garantias da base, como o Id v7, não são repetidas nos testes de cada
entidade.

## Tempo

Nenhum teste depende do relógio real. Regras com data (próxima revisão do
SM-2, auditoria) recebem um `TimeProvider`, e o teste usa `FakeTimeProvider`
para que a asserção seja exata.

## Testes funcionais da API

`Estudarias.Api.FunctionalTests` sobe a API inteira (`WebApplicationFactory<Program>`) e chama por HTTP, com a composição e o
pipeline reais.

- **Pipeline de autenticação** (`Security/`): usa `ApiFactory`, que só troca a chave do JWT e não toca o banco, então não precisa de contêiner.
  Os endpoints que esses testes chamam moram no próprio projeto (`TestEndpointsController`) e **não declaram `[Authorize]`**: quem os protege é
  a política padrão da API, e é isso que o teste prova. Tokens inválidos (outro algoritmo, sem assinatura, papel que o emissor nunca emitiria) são
  montados à mão em `CraftedTokens`, fora do emissor da API.
- **Endpoints de autenticação** (`Endpoints/`): usam `DatabaseApiFactory`, que liga a API a um banco de verdade, um por teste. A fixture é a mesma
  dos testes de integração (`PostgresFixture`, PostgreSQL em contêiner), compartilhada **pelo arquivo** (`Compile Include` no `.csproj`) em vez de
  copiada; por isso o namespace dela é o de origem. Precisam de Docker, ou de `ESTUDARIAS_TEST_POSTGRES`, como os de integração. O relógio é um
  `FakeTimeProvider` que começa no instante real e fica parado: o middleware do JWT confere a validade pelo relógio de verdade, então o teste só
  avança o tempo (para vencer um refresh token) quando não vai mais usar o access token.

Uma armadilha de `WebApplicationFactory`: a configuração que o teste troca precisa chegar **antes** do código do `Program`, porque o
`AddInfrastructure` lê a connection string na hora do registro. `ConfigureAppConfiguration` da fábrica só é aplicado depois, então a API usaria o
banco do `appsettings.Development.json` (o de desenvolvimento). Use `UseSetting`, como `DatabaseApiFactory` faz. Opções lidas depois (como as do JWT)
enxergam as duas formas.

## Execução

    dotnet test --project tests/Estudarias.Domain.UnitTests
    dotnet test --project tests/Estudarias.Application.UnitTests
    dotnet test --project tests/Estudarias.Api.UnitTests
    dotnet test --project tests/Estudarias.Api.FunctionalTests
    dotnet test --project tests/Estudarias.Infrastructure.UnitTests
    dotnet test --project tests/Estudarias.Infrastructure.IntegrationTests

Os testes de integração sobem um PostgreSQL em contêiner (Testcontainers),
então precisam de Docker. Onde não há Docker, `ESTUDARIAS_TEST_POSTGRES` aponta
para um servidor existente (uma connection string com permissão de criar
bancos):

    ESTUDARIAS_TEST_POSTGRES="Host=localhost;Username=estudarias;Password=estudarias;Database=postgres" \
      dotnet test --project tests/Estudarias.Infrastructure.IntegrationTests

### Testes de fluxo

`Identity/*FlowTests` chamam os casos de uso como a API chamaria, ligados por `AddApplication` e `AddInfrastructure` sobre um
PostgreSQL real, e conferem o banco direto, não só o que o caso de uso devolve. Só o emissor do access token é um dublê (o JWT é da
API). O relógio é um `FakeTimeProvider` que o teste avança para vencer tokens sem esperar, e `BuildProvider` liga outro provedor ao
mesmo banco com a configuração trocada, que é como um teste simula uma rotação de pepper ou a flag do sorteio virada. O nome de cada
teste cita o requisito do SRS que ele prova (`docs/SRS/`).

As migrations são aplicadas uma vez em um banco-modelo, e cada teste recebe um
banco novo clonado dele. Regras que dependem do PostgreSQL de verdade (índice
único, chave estrangeira, constraint, token de concorrência) são testadas aqui,
nunca com o provider InMemory, que ignora todas elas.

O `global.json` liga o `dotnet test` ao Microsoft.Testing.Platform, exigido
pelo xUnit v3 no SDK do .NET 10.

### Testes de leitura

A leitura não passa por caso de uso (ADR-002), então a consulta (`*Reads`) é testada onde está o banco: `Students/Queries/ProfileReadsTests`, em
`Infrastructure.IntegrationTests`, chama a porta de leitura pela composição real (`NewScope().Get<IProfileReads>()`) sobre um PostgreSQL de
verdade. Além do resultado, o teste prova o que a consulta **não** faz: um contexto próprio com `LogTo` captura o SQL, e o teste confere que ele
é uma consulta só e que não toca em `password_hash`. O endpoint que usa a leitura tem o teste funcional dele (`Endpoints/MeEndpointTests`).

### Testes de arquitetura

`Application.UnitTests/Architecture/ApplicationBoundaryTests` usa o NetArchTest para guardar a fronteira da Application: fora das classes `*Reads`,
nenhum tipo depende do `Microsoft.EntityFrameworkCore` nem do `IApplicationDbContext`. Um teste de controle prova que a análise enxerga essas
dependências nas classes `*Reads`; sem ele, as regras passariam no vazio. Uma regra nova de dependência entra nesse arquivo.
