# Estudarias

Plataforma adaptativa de estudo de matemática (TCC de ADS, FATEC São Caetano do Sul): trilha em grafo de pré-requisitos, diagnóstico do tipo de erro por alternativa e flashcards com repetição espaçada (SM-2).

## Arquitetura

Clean Architecture em quatro projetos (.NET 10, C#):

| Projeto | Responsabilidade | Depende de |
|---|---|---|
| `Estudarias.Domain` | Entidades, regras de negócio e motor (nota, desbloqueio, SM-2) | nada |
| `Estudarias.Application` | Casos de uso (escrita), consultas de leitura (`*Reads`), DTOs e abstrações (repositórios, relógio, contexto de leitura) | Domain |
| `Estudarias.Infrastructure` | EF Core + PostgreSQL, seed dos YAML, implementações das abstrações | Application |
| `Estudarias.Api` | Controllers REST, links de ação e composição das camadas (DI) | Application, Infrastructure |

O cliente mobile (Expo + TypeScript) consome a API REST. Toda a lógica fica no back-end: o app só exibe e envia respostas.

## Documentação

- [`CONTRIBUTING.md`](CONTRIBUTING.md): convenção de nome de branch, de commits e de PRs.
- [`docs/SRS/`](docs/SRS/README.md): a especificação de requisitos, um SRS por módulo (Autenticação, Conteúdo, Trilha e Progresso, Resposta e Diagnóstico, Revisão Espaçada), com o índice, as convenções de id e a rastreabilidade até os testes.
- [`docs/api.md`](docs/api.md): o contrato da API REST que o app consome (rotas, corpos, respostas, erros e links).
- [`docs/schema.dbml`](docs/schema.dbml): schema alvo da v1, com dados de exemplo (Frações).
- [`docs/adr/0001-authentication.md`](docs/adr/0001-authentication.md): as decisões da autenticação (conta, senha, sessão e grupo experimental), com as alternativas e as consequências.
- [`docs/adr/0002-reads-in-application.md`](docs/adr/0002-reads-in-application.md): como a leitura se separa da escrita (portas de leitura na Application, sem caso de uso nem repositório).
- [`docs/adr/0003-spaced-repetition-scheduler.md`](docs/adr/0003-spaced-repetition-scheduler.md): a repetição espaçada por um port no Domain, com o estado opaco e o cartão preso ao algoritmo (SM-2 agora, trocável depois).
- [`docs/testing.md`](docs/testing.md): as convenções de teste.

## Rodando

```bash
dotnet build
dotnet run --project src/Estudarias.Api
```

Com a API no ar, `GET http://localhost:5007/health` responde `Healthy`.

## Configuração

Em Development a API sobe sem configuração extra: o `appsettings.Development.json` traz valores de desenvolvimento (a senha do banco, o pepper e a chave do JWT), expostos
de propósito e com aviso no arquivo. **Em qualquer outro ambiente a API não sobe sem as variáveis obrigatórias.**

| Variável | Obrigatória | Padrão | Para quê (seção) |
|---|---|---|---|
| `ConnectionStrings__Default` | sim | só em Development | o PostgreSQL ([Banco de dados](#banco-de-dados)) |
| `PasswordHashing__CurrentPepperVersion` | sim | só em Development | a versão com que os hashes novos são derivados ([Senhas](#senhas)) |
| `PasswordHashing__Peppers__N` | sim | só em Development | o pepper da versão N, com 32 caracteres ou mais ([Senhas](#senhas)) |
| `Jwt__SigningKey` | sim | só em Development | a chave que assina o access token, com 32 caracteres ou mais ([Autenticação](#autenticação)) |
| `Jwt__Issuer`, `Jwt__Audience` | não | `Estudarias` | o emissor e a audiência do token ([Autenticação](#autenticação)) |
| `Jwt__AccessTokenMinutes` | não | `15` | a vida do access token ([Autenticação](#autenticação)) |
| `ExperimentalGroup__RandomAssignment` | não | `true` | liga o sorteio do grupo do aluno novo ([Grupo experimental](#grupo-experimental)) |
| `ExperimentalGroup__FallbackGroup` | não | `Diagnostic` | o grupo de todo aluno novo com o sorteio desligado ([Grupo experimental](#grupo-experimental)) |
| `ESTUDARIAS_CONNECTION_STRING` | não | `localhost` | só para o `dotnet ef`, que não passa pela API ([Banco de dados](#banco-de-dados)) |
| `ESTUDARIAS_TEST_POSTGRES` | não | contêiner | um PostgreSQL existente para os testes, onde não há Docker (`docs/testing.md`) |

Para rodar como em produção (troque os valores; **o pepper e a chave são segredos, e o pepper precisa ser o mesmo a cada subida**, senão as senhas já gravadas deixam de conferir):

```bash
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5007 \
ConnectionStrings__Default="Host=localhost;Database=estudarias;Username=estudarias;Password=<senha-do-banco>" \
PasswordHashing__CurrentPepperVersion=1 \
PasswordHashing__Peppers__1="<pepper: 32 ou mais caracteres>" \
Jwt__SigningKey="<chave: 32 ou mais caracteres>" \
dotnet run --project src/Estudarias.Api --no-launch-profile
```

Gere cada segredo uma vez (`openssl rand -base64 32`) e guarde-o fora do repositório. Sem alguma das obrigatórias, com um segredo curto ou com um grupo de reserva que não existe, a
subida falha dizendo qual é.

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

## Conteúdo

O que o aluno estuda (tópicos, tipos de erro, lições, exercícios e flashcards) fica em YAML na pasta [`content/`](content/), um
arquivo por tópico (`content/<slug>.yaml`), revisado por pull request como código. Nada de conteúdo é editado pela API. Para
carregar no banco, com as migrations já aplicadas:

```bash
dotnet run --project src/Estudarias.Api -- seed "$PWD/content"
```

Passe o caminho **absoluto** da pasta (o `"$PWD/content"` acima serve no bash, no zsh e no PowerShell, rodando na raiz do repositório). O
`dotnet run` executa o programa com a pasta do projeto (`src/Estudarias.Api`) como diretório de trabalho, então um `content/` relativo
seria procurado em `src/Estudarias.Api/content/`, que não existe, e o comando sairia com código 1 sem gravar nada.

O comando valida todos os arquivos antes de gravar e grava tudo numa transação. Se algum arquivo tiver problema, ele lista cada
problema com o arquivo e o item, sai com código 1 e não grava nada. Cada item é achado pela chave natural (o `slug` do tópico, o
`code` do tipo de erro, do exercício e do flashcard, e o `order` da lição), então recarregar o mesmo arquivo não duplica nada e
mantém os ids. Um item que sai do YAML continua no banco, e uma alternativa não pode ser removida de um exercício já carregado. As
regras completas estão no [SRS de Conteúdo](docs/SRS/SRS%20-%20M%C3%B3dulo%20de%20Conte%C3%BAdo.md).

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

## CORS

O app aberto no navegador (o Expo na web) só chama a API se ela liberar a origem dele. Em Development qualquer origem é aceita, porque a
porta do Expo web varia. Nos demais ambientes, só as origens da configuração; sem nenhuma, nenhuma origem é liberada. O app nativo não usa CORS.

| Variável | Padrão | O que faz |
|---|---|---|
| `Cors__AllowedOrigins__0`, `__1`... | nenhuma | as origens liberadas fora de Development, cada uma como o navegador a manda: esquema, host e porta, sem caminho nem barra no fim (`https://app.estudarias.com.br`) |

Uma origem mal escrita (o curinga `*`, uma barra no fim, um caminho) derruba a subida: abrir a API para qualquer site tem de ser uma
decisão visível, e uma barra a mais nunca bateria com o cabeçalho `Origin`. Não há credenciais em CORS: o app manda o token no `Authorization`.

## API de autenticação

Os quatro endpoints são os únicos abertos da API (fora o `/health`). Todos recebem e devolvem JSON. O arquivo `src/Estudarias.Api/Estudarias.Api.http` tem uma chamada de cada.

| Endpoint | Corpo | Resposta |
|---|---|---|
| `POST /auth/register` | `name`, `email`, `password` | `201` com a sessão (o aluno já entra logado) e o `Location` do `/me` |
| `POST /auth/login` | `email`, `password` | `200` com a sessão |
| `POST /auth/refresh` | `refreshToken` | `200` com a sessão nova; o token usado deixa de valer |
| `POST /auth/logout` | `refreshToken` | `204`, também para um token desconhecido ou já revogado |

A sessão (cadastro, login e renovação) tem sempre a mesma forma:

```json
{
  "accessToken": "eyJhbGciOi...",
  "accessTokenExpiresAt": "2026-11-09T12:15:00+00:00",
  "refreshToken": "9f2c...64 caracteres hexadecimais",
  "user": { "id": "0192f0c4-...", "email": "ana@example.com", "role": "Student", "studentId": "0192f0c4-..." }
}
```

O app guarda o `refreshToken` (ele só aparece nessa resposta; o servidor guarda só o hash), manda o `accessToken` em `Authorization: Bearer` e, perto de `accessTokenExpiresAt`
(ou ao receber `401`), chama `/auth/refresh`.

As recusas são `application/problem+json` (RFC 9457) com o código do erro em `code`. É pelo `code` que o app decide o que mostrar, não pela mensagem:

| Status | Quando | `code` |
|---|---|---|
| `400` | e-mail, senha ou nome inválidos | `identity.email.invalid`, `identity.password.tooShort`, `identity.password.noUppercase`, `noLowercase`, `noDigit`, `noSpecial`, `tooLong`, `student.name.required`, `student.name.tooLong` |
| `401` | credenciais inválidas (senha errada, e-mail desconhecido ou conta desabilitada, sem distinção) | `identity.credentials.invalid` |
| `401` | refresh token desconhecido, vencido, já usado ou de conta desabilitada | `identity.refreshToken.invalid` |
| `409` | e-mail já cadastrado | `identity.email.taken` |
| `500` | erro inesperado (por exemplo, de configuração) | sem `code`, sem detalhe técnico |

Um JSON malformado é `400` e um corpo que não é JSON é `415`, ambos sem `code`. Campos ausentes ou nulos recebem o mesmo erro de qualquer valor inválido, com `code`.

## API do perfil

`GET /me` devolve o perfil de quem está logado. Exige o `accessToken` em `Authorization: Bearer`, como todo endpoint fora os de autenticação. É uma leitura: não grava nada e não passa por caso de uso (veja o [ADR-002](docs/adr/0002-reads-in-application.md)).

```json
{
  "user": { "id": "0192f0c4-...", "email": "ana@example.com", "role": "Student" },
  "student": { "id": "0192f0c4-...", "name": "Ana Souza", "experimentalGroup": "Diagnostic" },
  "links": [
    { "href": "http://localhost:5007/me", "rel": "self", "method": "GET" },
    { "href": "http://localhost:5007/trail", "rel": "trail", "method": "GET" }
  ]
}
```

- `links` são as ações a partir do recurso, com URL absoluta (veja o [ADR-004](docs/adr/0004-action-links.md)): o `self` e o `trail`, que leva à trilha.
- `user` é a conta de acesso, e `student` é o perfil de estudo ligado a ela, separados como no domínio. `student.id` é o `studentId` que o login devolve.
- `experimentalGroup` é `Control` ou `Diagnostic` e diz ao app qual versão da experiência mostrar. O grupo é sorteado no cadastro e não muda.
- O progresso por tópico não faz parte do perfil: sai na trilha (`GET /trail`).

| Status | Quando | `code` |
|---|---|---|
| `401` | sem token, token vencido ou inválido | sem `code` (o desafio `Bearer` vem no cabeçalho `WWW-Authenticate`) |
| `401` | token válido de uma conta que não existe mais ou foi desabilitada, sem distinção | `identity.session.invalid` |

Ao receber `401` o app tenta `/auth/refresh`; se a renovação também falhar, volta para o login.

## API da trilha

`GET /trail` devolve a trilha do aluno logado: os tópicos em ordem de pré-requisito, com o status e a nota de cada um, e os totais que a tela
mostra no topo. O contrato completo está em [`docs/api.md`](docs/api.md#trilha--get-trail).

```json
{
  "masteryThreshold": 70,
  "masteredTopics": 0,
  "totalTopics": 2,
  "dueReviews": 0,
  "topics": [
    { "id": "0192f0c4-...", "slug": "operacoes-basicas", "name": "Operações básicas", "status": "available", "score": null, "prerequisites": [], "completedLessons": 1, "totalLessons": 2, "links": [{ "href": "http://localhost:5007/topics/0192f0c4-...", "rel": "topic", "method": "GET" }] },
    { "id": "0192f0c4-...", "slug": "fracoes", "name": "Frações", "status": "locked", "score": null, "prerequisites": ["0192f0c4-..."], "completedLessons": 0, "totalLessons": 2, "links": [{ "href": "http://localhost:5007/topics/0192f0c4-...", "rel": "topic", "method": "GET" }] }
  ],
  "links": [{ "href": "http://localhost:5007/trail", "rel": "self", "method": "GET" }]
}
```

- `status` é `locked`, `available` ou `mastered`. `score` vai de 0 a 100, com uma casa e **arredondada para baixo** (69,95 sai `69.9`, nunca `70.0`), e é `null` enquanto o aluno não respondeu nada no tópico. A nota usa as 10 respostas mais recentes do aluno no tópico.
- Os tópicos sem pré-requisito já vêm `available` para o aluno novo: o cadastro os libera, e abrir a trilha os libera de novo para quem se cadastrou antes de existir conteúdo (é a única escrita do endpoint, e é idempotente).
- `dueReviews` conta os cartões do aluno cuja próxima revisão já chegou.
- Cada item traz o link `topic`, e a trilha traz o `reviews`, que leva às revisões do dia.
- É preciso ter carregado o conteúdo (`dotnet run --project src/Estudarias.Api -- seed "$PWD/content"`); sem ele a trilha vem vazia.

Os `401` são os mesmos do `/me`: sem token, vencido ou inválido (sem `code`), e conta que não existe ou foi desabilitada (`identity.session.invalid`).

## API do tópico e da lição

`GET /topics/{id}` devolve o tópico do aluno logado, e `GET /lessons/{id}`, a lição. O contrato completo está em [`docs/api.md`](docs/api.md#tópico--get-topicstopicid). As duas são leituras (ADR-002) e não gravam nada.

```json
{
  "id": "0192f0c4-...", "slug": "fracoes", "name": "Frações", "status": "available",
  "score": 42.0, "attemptCount": 3, "masteryThreshold": 70, "minAttempts": 5,
  "prerequisites": [{ "id": "0192f0c4-...", "name": "Operações básicas", "status": "mastered" }],
  "unlocks": [{ "id": "0192f0c4-...", "name": "Equações do 1º grau" }],
  "lessons": [{ "id": "0192f0c4-...", "order": 1, "title": "Soma de frações", "status": "inProgress", "completedExercises": 1, "totalExercises": 3, "links": [{ "href": "http://localhost:5007/lessons/0192f0c4-...", "rel": "lesson", "method": "GET" }] }],
  "links": [
    { "href": "http://localhost:5007/topics/0192f0c4-...", "rel": "self", "method": "GET" },
    { "href": "http://localhost:5007/trail", "rel": "trail", "method": "GET" }
  ]
}
```

- **Estado da lição:** cada lição do tópico traz `status` (`notStarted`, `inProgress` ou `completed`) e os exercícios acertados, de quantos. Concluída é ter acertado **cada** exercício pelo menos uma vez; é derivado das tentativas, e errar depois não desfaz. A trilha traz `completedLessons` e `totalLessons` de cada tópico, e a resposta de responder traz a `lesson` já com a resposta nova. Concluir a lição não domina o tópico.
- **Tópico bloqueado abre** (`200`): a tela mostra o que falta, e as lições vêm **sem o link `lesson`**. `attemptCount` é o total de tentativas no tópico; `score` usa as 10 mais recentes.
- **A lição nunca revela o gabarito:** cada alternativa só tem `id` e `text`, e a consulta nem lê `is_correct` nem `error_type_id`. Exercícios e alternativas vêm na ordem do conteúdo (gravada em `position`).
- Cada exercício traz o link `answer` (`POST /exercises/{id}/attempts`).
- **Histórico de respostas:** `GET /topics/{id}/attempts` devolve as respostas que entram na nota (as 10 mais recentes, da mais recente para a mais antiga), com o enunciado, a dificuldade e se acertou, e sem a alternativa escolhida nem a correta. Refazer a nota por elas dá a nota do tópico. O link `attempts` do tópico só existe quando ele está liberado.

| Status | Quando | `code` |
|---|---|---|
| `401` | sem token, vencido ou inválido; conta que não existe ou foi desabilitada | sem `code`; `identity.session.invalid` |
| `403` | a lição, ou o histórico, de um tópico bloqueado | `student.topic.locked` |
| `404` | tópico ou lição que não existe | `trail.topic.notFound`, `trail.lesson.notFound` |

## API de responder um exercício

`POST /exercises/{id}/attempts` registra a resposta do aluno logado e devolve, de uma vez, o que a tela do retorno precisa. É o ciclo do produto numa chamada: **errar, diagnosticar, flashcard, nota, desbloqueio**. O contrato completo está em [`docs/api.md`](docs/api.md#responder-exercício--post-exercisesexerciseidattempts).

```json
// POST /exercises/0192f0c4-.../attempts
{ "alternativeId": "0192f0c4-...", "responseSeconds": 48 }
```

```json
// 201 Created
{
  "attemptId": "0192f0c4-...",
  "isCorrect": false,
  "correctAlternativeId": "0192f0c4-...",
  "feedback": { "kind": "incorrectWithDiagnosis", "correctAnswer": "5/6", "errorType": { "name": "Soma direta de numeradores e denominadores", "feedback": "..." } },
  "topic": { "id": "0192f0c4-...", "status": "available", "score": 38.5, "attemptCount": 4 },
  "lesson": { "id": "0192f0c4-...", "status": "inProgress", "completedExercises": 1, "totalExercises": 3 },
  "unlockedTopics": [],
  "flashcard": { "id": "0192f0c4-...", "nextReviewAt": "2026-11-17T13:00:00+00:00" },
  "links": [{ "href": "http://localhost:5007/topics/0192f0c4-...", "rel": "topic", "method": "GET" }]
}
```

- O retorno depende do grupo, e só dele: o grupo **diagnóstico** recebe `incorrectWithDiagnosis` com o tipo de erro; o **controle** recebe `incorrect` com `errorType: null`. A estrutura é a mesma. O grupo vem do aluno gravado, nunca do corpo.
- **O cartão nasce nos dois grupos** ([ADR-005](docs/adr/0005-flashcard-in-both-groups.md)): errar cria o cartão do tipo de erro, uma vez por aluno e por tipo. O controle vê o diagnóstico ao revisar, o que é uma limitação declarada da análise.
- A nota já inclui a resposta que acabou de chegar (as 10 mais recentes), e o domínio e o desbloqueio são avaliados na mesma chamada. Tentativa, progresso e cartão são gravados juntos.
- Responde `201` sem `Location`, porque a tentativa não tem um recurso próprio para ler.

| Status | Quando | `code` |
|---|---|---|
| `400` | alternativa que não é do exercício (ou ausente); tempo de resposta negativo | `answer.alternative.notInExercise`, `attempt.responseTime.invalid` |
| `401` | sem token, vencido ou inválido; conta que não existe ou foi desabilitada | sem `code`; `identity.session.invalid` |
| `403` | o tópico do exercício está bloqueado | `student.topic.locked` |
| `404` | o exercício não existe | `trail.exercise.notFound` |
| `415` | corpo que não é JSON | sem `code` |

## API das revisões

`GET /reviews/due` lista as revisões do dia do aluno logado, e `POST /reviews/{id}` registra uma revisão. O contrato completo está em [`docs/api.md`](docs/api.md#revisões-do-dia--get-reviewsdue).

```json
// GET /reviews/due
{
  "count": 1,
  "items": [{
    "id": "0192f0c4-...",
    "question": "Por que 1/2 + 1/3 não é 2/5?",
    "answer": "Porque só se somam numeradores quando os denominadores são iguais.",
    "topic": { "id": "0192f0c4-...", "name": "Frações" },
    "nextReviewAt": "2026-11-16T13:00:00+00:00",
    "links": [{ "href": "http://localhost:5007/reviews/0192f0c4-...", "rel": "review", "method": "POST" }]
  }],
  "links": [
    { "href": "http://localhost:5007/reviews/due", "rel": "self", "method": "GET" },
    { "href": "http://localhost:5007/trail", "rel": "trail", "method": "GET" }
  ]
}
```

```json
// POST /reviews/0192f0c4-...   { "rating": "good" }   ->   200 OK
{ "id": "0192f0c4-...", "nextReviewAt": "2026-11-22T13:00:00+00:00", "links": [{ "href": "http://localhost:5007/reviews/due", "rel": "reviews", "method": "GET" }] }
```

- A lista traz só os cartões **vencidos**, do mais atrasado ao mais recente, já com a pergunta e a resposta: virar o cartão é coisa do app. Sem nenhum, `count: 0`.
- `rating` é `again`, `hard`, `good` ou `easy`, em minúsculas. A próxima revisão parte do **instante em que o aluno revisou** (SM-2, [ADR-003](docs/adr/0003-spaced-repetition-scheduler.md)). Revisar antes do vencimento é aceito.
- O cartão é do aluno do token: o de outro aluno responde `404`, igual a um que não existe. O agendador é o do algoritmo gravado no cartão.
- Revisar **não** é uma tentativa: não muda a nota nem o status do tópico. Duas revisões simultâneas do mesmo cartão: a última gravação vale.

| Status | Quando | `code` |
|---|---|---|
| `400` | nota ausente, em maiúsculas ou desconhecida | `studentFlashcard.rating.invalid` |
| `401` | sem token, vencido ou inválido; conta que não existe ou foi desabilitada | sem `code`; `identity.session.invalid` |
| `404` | cartão que não existe ou é de outro aluno | `review.card.notFound` |
| `415` | corpo que não é JSON | sem `code` |
| `500` | algoritmo do cartão sem agendador, ou estado de agendamento corrompido | sem `code`, sem detalhe técnico |

