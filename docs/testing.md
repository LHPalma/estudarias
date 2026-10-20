# Convenções de testes

## Projetos

Um projeto de testes por camada, cada um em um passo próprio do CI, para o
vermelho apontar a camada.

| Projeto | Testa | Dependências |
|---|---|---|
| `Estudarias.Domain.UnitTests` | entidades, invariantes, cálculo do SM-2 | nenhuma: sem banco, sem I/O, sem relógio real |
| `Estudarias.Application.UnitTests` | handlers de commands e queries | NSubstitute para repositórios e serviços |
| `Estudarias.Infrastructure.IntegrationTests` | mapeamento EF Core, consultas, seed YAML | PostgreSQL real via Testcontainers |
| `Estudarias.Api.FunctionalTests` | endpoints REST de ponta a ponta | `WebApplicationFactory` + Testcontainers |

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
- `DomainErrorCatalogTests` fixa, por reflexão, a lista completa de códigos,
  a unicidade e o formato `<área>.<conceito>.<invariante>` em minúsculas.
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

## Execução

    dotnet test --project tests/Estudarias.Domain.UnitTests

O `global.json` liga o `dotnet test` ao Microsoft.Testing.Platform, exigido
pelo xUnit v3 no SDK do .NET 10.
