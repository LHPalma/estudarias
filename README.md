# Estudarias

Plataforma adaptativa de estudo de matemática (TCC de ADS, FATEC São Caetano do Sul): trilha em grafo de pré-requisitos, diagnóstico do tipo de erro por alternativa e flashcards com repetição espaçada (SM-2).

## Arquitetura

Clean Architecture em quatro projetos (.NET 10, C#):

| Projeto | Responsabilidade | Depende de |
|---|---|---|
| `Estudarias.Domain` | Entidades, regras de negócio e motor (nota, desbloqueio, SM-2) | nada |
| `Estudarias.Application` | Casos de uso, DTOs e abstrações (repositórios, relógio) | Domain |
| `Estudarias.Infrastructure` | EF Core + PostgreSQL, seed dos YAML, implementações das abstrações | Application |
| `Estudarias.Api` | Controllers REST, links de ação e composição das camadas (DI) | Application, Infrastructure |

O cliente mobile (Expo + TypeScript) consome a API REST. Toda a lógica fica no back-end: o app só exibe e envia respostas.

## Documentação

- [`docs/schema.dbml`](docs/schema.dbml): schema alvo da v1, com dados de exemplo (Frações).

## Rodando

```bash
dotnet build
dotnet run --project src/Estudarias.Api
```

Com a API no ar, `GET http://localhost:5007/health` responde `Healthy`.

## Banco de dados

A API usa PostgreSQL. Em desenvolvimento a connection string vem do
`appsettings.Development.json` (`localhost`, banco `estudarias`, usuário e senha
`estudarias`); nos demais ambientes vem de `ConnectionStrings__Default`.

As migrations não rodam sozinhas na subida. Para criar ou atualizar o banco:

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/Estudarias.Infrastructure
```

Para gerar uma migration nova: `dotnet ef migrations add <Nome> --project src/Estudarias.Infrastructure --output-dir Persistence/Migrations`.
