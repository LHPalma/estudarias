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

## Grupo experimental

Todo aluno que se cadastra entra em um dos dois grupos do experimento (`Control` ou `Diagnostic`). O sorteio fica atrás de uma
flag de configuração, para ligar e desligar sem mudar código:

| Variável | Padrão | O que faz |
|---|---|---|
| `ExperimentalGroup__RandomAssignment` | `true` | ligada, sorteia 50/50 entre controle e diagnóstico; desligada, não sorteia |
| `ExperimentalGroup__FallbackGroup` | `Diagnostic` | o grupo de todo aluno novo enquanto o sorteio está desligado (`Control` ou `Diagnostic`) |

Desligue o sorteio no período em que o experimento ainda não começou ou já terminou. A flag só vale para cadastros novos: quem já
tem grupo não muda. Um valor de `FallbackGroup` que não existe derruba a subida. Os valores do `appsettings.json` também
valem como padrão, e uma configuração em arquivo que recarrega vale na próxima chamada, sem reiniciar.

## Autenticação

Toda a API exige login: uma requisição sem um access token válido recebe `401`, inclusive numa rota que não existe. Ficam abertos só o
`GET /health`, os endpoints de sessão (cadastro, login, renovação e logout) e, em Development, o OpenAPI. Um endpoint novo já nasce
protegido; quem precisa ser aberto declara `[AllowAnonymous]` de propósito.

O access token é um JWT HS256 de 15 minutos, enviado em `Authorization: Bearer <token>`, com as claims `sub` (id da conta), `email` e `role`.
A API só aceita o algoritmo HS256 e confere emissor, audiência, assinatura e validade, com 30 segundos de tolerância de relógio.

| Variável | Padrão | O que faz |
|---|---|---|
| `Jwt__SigningKey` | nenhum | a chave que assina e confere o token: pelo menos 32 caracteres, por exemplo `openssl rand -base64 32` |
| `Jwt__Issuer` | `Estudarias` | o emissor do token |
| `Jwt__Audience` | `Estudarias` | a audiência do token |
| `Jwt__AccessTokenMinutes` | `15` | a vida do access token, em minutos (maior que zero) |

Sem a chave, com uma chave curta, com a vida do token zerada ou com emissor ou audiência em branco, a API **não sobe**. Em desenvolvimento a
chave vem do `appsettings.Development.json`, exposta de propósito e com aviso no arquivo.

A chave é o segredo mais sensível do módulo: com ela se forja o token de qualquer aluno. Trocá-la derruba só os access tokens em circulação
(no máximo 15 minutos de sessão): os refresh tokens ficam no banco e continuam valendo, então o app renova a sessão sozinho e recebe um token
assinado com a chave nova.
