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

## Senhas

A senha é guardada com Argon2id, e o hash é derivado com um **pepper**: um segredo global, versionado, que fica fora do banco. Um
dump do banco sozinho não permite ataque offline. Em desenvolvimento o pepper vem do `appsettings.Development.json` (exposto de
propósito, com aviso no arquivo); nos demais ambientes a API **não sobe** sem a configuração abaixo.

| Variável | Valor |
|---|---|
| `PasswordHashing__CurrentPepperVersion` | versão com que os hashes novos são derivados (`1`) |
| `PasswordHashing__Peppers__1` | o pepper da versão 1: pelo menos 32 caracteres, por exemplo `openssl rand -base64 32` |

Para rotacionar o pepper, acrescente a versão nova **sem tirar a antiga** (`PasswordHashing__Peppers__2`) e aponte
`CurrentPepperVersion` para ela. Os hashes antigos continuam valendo, e `NeedsRehash` os acusa para o login refazê-los com o pepper
novo (caso de uso do login, #39). Só remova o pepper antigo quando nenhum hash o usar mais:

```sql
SELECT password_pepper_version, count(*) FROM app_user GROUP BY 1;
```

Um hash cuja versão não está mais configurada **não** é tratado como senha errada: a verificação falha com erro de configuração.
