# API REST — contrato da v1

Este é o contrato que o app (Expo + TypeScript, repositório [`estudarias-mobile`](https://github.com/LHPalma/estudarias-mobile)) consome. Ele cobre as sete telas do protótipo: login, trilha, mapa, tópico, exercício, diagnóstico do erro e revisão. A sessão (`/auth/*`), o perfil (`/me`) e a trilha (`GET /trail`) já estão no ar e são descritos no [README](../README.md#autenticação); o restante deste documento é o alvo dos próximos PRs, e cada seção diz em qual issue entra.

Toda a regra fica no servidor. O app não calcula nota, desbloqueio nem agendamento: mostra o que a API devolve e decide quais botões aparecem pelos **links** de cada recurso.

## Convenções

- **Base:** `http://localhost:5007` em desenvolvimento. JSON em UTF-8, nomes em `camelCase`.
- **Autenticação:** todo endpoint deste documento exige `Authorization: Bearer <accessToken>`. Sem token válido: `401`. O aluno é sempre o dono do token; nenhuma rota recebe id de aluno.
- **Ids:** GUID (v7), como texto. Tópicos também têm `slug`, que é estável entre cargas do conteúdo.
- **Datas:** ISO 8601 em UTC (`2026-11-16T13:00:00Z`).
- **Enums:** texto em minúsculas (`locked`, `easy`, `again`...).
- **Nota do tópico:** número de 0 a 100 com **uma casa, arredondado para baixo** (69,9997 vira `69.9`, nunca `70.0`), para a nota exibida nunca parecer ter atingido a meta sem ter atingido. A meta vem em `masteryThreshold` (70).

### Links

Cada recurso traz `links`: as ações que o aluno pode fazer naquele estado (formato da #8, sem HAL nem JSON:API, decidido no [ADR-004](adr/0004-action-links.md)). **Uma ação só aparece quando é permitida**; o app mostra o botão se o link existe.

```json
{ "href": "http://localhost:5007/topics/0192f0c4-0001-7000-8000-000000000002", "rel": "topic", "method": "GET" }
```

| `rel` | Onde aparece | Quando |
|---|---|---|
| `self` | todo recurso | sempre |
| `trail` | perfil, tópico | sempre |
| `topic` | item da trilha, tópico desbloqueado | sempre (o tópico bloqueado abre para mostrar o que falta) |
| `lesson` | item de lição | só se o tópico não está bloqueado |
| `answer` | exercício | só se o tópico não está bloqueado |
| `reviews` | trilha | sempre |
| `review` | cartão | só se a revisão venceu |

### Erros

Todo erro é um `ProblemDetails` (RFC 9457) com o código em `code`. **O app decide pelo `code`, não pela mensagem.**

```json
{ "status": 400, "title": "Validation error", "detail": "The chosen alternative does not belong to the exercise.", "code": "answer.alternative.notInExercise" }
```

| Status | Quando |
|---|---|
| `400` | entrada inválida (corpo malformado, alternativa de outro exercício, nota de revisão desconhecida) |
| `401` | sem token, token vencido ou inválido |
| `403` | ação num tópico bloqueado (`student.topic.locked`) |
| `404` | tópico, lição, exercício ou cartão que não existe (ou cartão de outro aluno) |

## Trilha — `GET /trail`

A trilha do aluno: os tópicos em ordem de pré-requisito, com o status e a nota de cada um. Serve a tela da trilha (lista) e a do mapa (grafo, pelos `prerequisites`). Na primeira chamada de um aluno novo, os tópicos sem pré-requisito já vêm `available`.

```json
{
  "masteryThreshold": 70,
  "masteredTopics": 1,
  "totalTopics": 4,
  "dueReviews": 2,
  "topics": [
    {
      "id": "0192f0c4-0001-7000-8000-000000000001",
      "slug": "operacoes-basicas",
      "name": "Operações básicas",
      "status": "mastered",
      "score": 86.4,
      "prerequisites": [],
      "links": [{ "href": ".../topics/0192f0c4-0001-7000-8000-000000000001", "rel": "topic", "method": "GET" }]
    },
    {
      "id": "0192f0c4-0001-7000-8000-000000000002",
      "slug": "fracoes",
      "name": "Frações",
      "status": "available",
      "score": 42.0,
      "prerequisites": ["0192f0c4-0001-7000-8000-000000000001"],
      "links": [{ "href": ".../topics/0192f0c4-0001-7000-8000-000000000002", "rel": "topic", "method": "GET" }]
    },
    {
      "id": "0192f0c4-0001-7000-8000-000000000003",
      "slug": "equacoes-1-grau",
      "name": "Equações do 1º grau",
      "status": "locked",
      "score": null,
      "prerequisites": ["0192f0c4-0001-7000-8000-000000000002"],
      "links": [{ "href": ".../topics/0192f0c4-0001-7000-8000-000000000003", "rel": "topic", "method": "GET" }]
    }
  ],
  "links": [
    { "href": ".../trail", "rel": "self", "method": "GET" },
    { "href": ".../reviews/due", "rel": "reviews", "method": "GET" }
  ]
}
```

- `status`: `locked`, `available` ou `mastered`. Um tópico liberado nunca volta a `locked`.
- `score`: `null` enquanto o aluno não respondeu nada no tópico.
- `dueReviews`: cartões com revisão vencida, para o aviso "N revisões para hoje".
- **Links entregues na #86:** o `self` da trilha. O `topic` de cada item entra com o `GET /topics/{id}` (#87) e o `reviews` com o `GET /reviews/due` (#89): um link só existe quando a rota dele existe (ADR-004), então até lá o `links` de cada item vem vazio.
- A nota usa as 10 respostas mais recentes do aluno no tópico. A trilha de um aluno que ainda não tem a raiz liberada a libera antes de responder (idempotente).

## Tópico — `GET /topics/{topicId}`

O tópico, suas lições e o que falta para dominá-lo. Um tópico bloqueado também abre (`200`), para a tela mostrar quais pré-requisitos faltam, mas as lições vêm sem o link `lesson`.

```json
{
  "id": "0192f0c4-0001-7000-8000-000000000002",
  "slug": "fracoes",
  "name": "Frações",
  "status": "available",
  "score": 42.0,
  "attemptCount": 3,
  "masteryThreshold": 70,
  "minAttempts": 5,
  "prerequisites": [{ "id": "0192f0c4-0001-7000-8000-000000000001", "name": "Operações básicas", "status": "mastered" }],
  "unlocks": [{ "id": "0192f0c4-0001-7000-8000-000000000003", "name": "Equações do 1º grau" }],
  "lessons": [
    {
      "id": "0192f0c4-0003-7000-8000-000000000001",
      "order": 1,
      "title": "Soma de frações com denominadores diferentes",
      "links": [{ "href": ".../lessons/0192f0c4-0003-7000-8000-000000000001", "rel": "lesson", "method": "GET" }]
    }
  ],
  "links": [
    { "href": ".../topics/0192f0c4-0001-7000-8000-000000000002", "rel": "self", "method": "GET" },
    { "href": ".../trail", "rel": "trail", "method": "GET" }
  ]
}
```

- Dominar o tópico exige `score >= masteryThreshold` **e** `attemptCount >= minAttempts`.
- `unlocks`: os tópicos que têm este como pré-requisito.
- Erros: `404 trail.topic.notFound`.

## Lição — `GET /lessons/{lessonId}`

O conteúdo da lição e os exercícios dela, **sem o gabarito**: nenhuma alternativa diz se é a correta.

```json
{
  "id": "0192f0c4-0003-7000-8000-000000000001",
  "topicId": "0192f0c4-0001-7000-8000-000000000002",
  "order": 1,
  "title": "Soma de frações com denominadores diferentes",
  "content": "Para somar frações, primeiro deixe os denominadores iguais.\n\n1/2 + 1/3 = 3/6 + 2/6 = 5/6",
  "exercises": [
    {
      "id": "0192f0c4-0004-7000-8000-000000000001",
      "statement": "Quanto é 1/2 + 1/3?",
      "difficulty": "easy",
      "alternatives": [
        { "id": "0192f0c4-0005-7000-8000-000000000001", "text": "2/5" },
        { "id": "0192f0c4-0005-7000-8000-000000000002", "text": "5/6" },
        { "id": "0192f0c4-0005-7000-8000-000000000003", "text": "2/6" },
        { "id": "0192f0c4-0005-7000-8000-000000000004", "text": "1/6" }
      ],
      "links": [{ "href": ".../exercises/0192f0c4-0004-7000-8000-000000000001/attempts", "rel": "answer", "method": "POST" }]
    }
  ],
  "links": [
    { "href": ".../lessons/0192f0c4-0003-7000-8000-000000000001", "rel": "self", "method": "GET" },
    { "href": ".../topics/0192f0c4-0001-7000-8000-000000000002", "rel": "topic", "method": "GET" }
  ]
}
```

- `content`: texto da lição, com quebras de linha. O formato é texto simples por ora; se a equipe adotar Markdown, este campo continua o mesmo.
- `difficulty`: `easy`, `medium` ou `hard`.
- As alternativas vêm na ordem do conteúdo.
- Erros: `404 trail.lesson.notFound`; `403 student.topic.locked` se o tópico da lição está bloqueado.

## Responder exercício — `POST /exercises/{exerciseId}/attempts`

Registra a resposta e devolve, de uma vez, tudo que a tela de diagnóstico precisa: o retorno conforme o grupo experimental, a nota nova do tópico, os tópicos liberados e o flashcard criado pelo erro.

**Corpo**

```json
{ "alternativeId": "0192f0c4-0005-7000-8000-000000000001", "responseSeconds": 48 }
```

- `responseSeconds`: opcional, tempo até responder (inteiro, 0 ou mais). Vai para a análise do experimento.

**Resposta `201 Created`** (`Location` aponta para a tentativa)

```json
{
  "attemptId": "0192f0c4-0008-7000-8000-000000000002",
  "isCorrect": false,
  "correctAlternativeId": "0192f0c4-0005-7000-8000-000000000002",
  "feedback": {
    "kind": "incorrectWithDiagnosis",
    "correctAnswer": "5/6",
    "errorType": {
      "name": "Soma direta de numeradores e denominadores",
      "feedback": "Para somar frações, primeiro deixe os denominadores iguais: ache o MMC, converta e só então some os numeradores."
    }
  },
  "topic": { "id": "0192f0c4-0001-7000-8000-000000000002", "status": "available", "score": 38.5, "attemptCount": 4 },
  "unlockedTopics": [],
  "flashcard": { "id": "0192f0c4-000d-7000-8000-000000000002", "nextReviewAt": "2026-11-17T13:00:00Z" },
  "links": [
    { "href": ".../topics/0192f0c4-0001-7000-8000-000000000002", "rel": "topic", "method": "GET" },
    { "href": ".../reviews/due", "rel": "reviews", "method": "GET" }
  ]
}
```

- `feedback.kind`: `correct`, `incorrect` ou `incorrectWithDiagnosis`. O grupo **diagnóstico** recebe `incorrectWithDiagnosis`, com `errorType`; o grupo **controle** recebe `incorrect`, com `errorType: null`. As mensagens ("Resposta correta", "Resposta incorreta") são do app.
- `unlockedTopics`: `[{ "id", "name" }]` dos tópicos liberados por esta resposta, para o aviso de desbloqueio. Vazio na maioria das respostas.
- `flashcard`: o cartão de revisão criado por este erro, ou `null` (acerto, ou o aluno já tem o cartão desse tipo de erro).
- Erros: `404 trail.exercise.notFound`; `400 answer.alternative.notInExercise`; `400 attempt.responseTime.invalid`; `403 student.topic.locked`.

## Revisões do dia — `GET /reviews/due`

Os cartões do aluno com revisão vencida, do mais atrasado para o mais recente. A resposta vem junto: virar o cartão é coisa do app.

```json
{
  "count": 1,
  "items": [
    {
      "id": "0192f0c4-000d-7000-8000-000000000001",
      "question": "Por que 1/2 + 1/3 não é 2/5?",
      "answer": "Porque só se somam numeradores quando os denominadores são iguais: 1/2 + 1/3 = 3/6 + 2/6 = 5/6.",
      "topic": { "id": "0192f0c4-0001-7000-8000-000000000002", "name": "Frações" },
      "nextReviewAt": "2026-11-16T13:00:00Z",
      "links": [{ "href": ".../reviews/0192f0c4-000d-7000-8000-000000000001", "rel": "review", "method": "POST" }]
    }
  ],
  "links": [
    { "href": ".../reviews/due", "rel": "self", "method": "GET" },
    { "href": ".../trail", "rel": "trail", "method": "GET" }
  ]
}
```

## Revisar cartão — `POST /reviews/{studentFlashcardId}`

**Corpo**

```json
{ "rating": "good" }
```

- `rating`: `again` (errei), `hard`, `good` ou `easy`. O app pode mostrar só dois botões (`again` e `good`) ou os quatro.

**Resposta `200 OK`**

```json
{
  "id": "0192f0c4-000d-7000-8000-000000000001",
  "nextReviewAt": "2026-11-22T13:00:00Z",
  "links": [
    { "href": ".../reviews/due", "rel": "reviews", "method": "GET" }
  ]
}
```

- Revisar antes do vencimento é aceito; o link `review` só aparece quando venceu.
- Erros: `404 review.card.notFound` (também para o cartão de outro aluno); `400 studentFlashcard.rating.invalid`.

## Desenvolvimento do app

- Em Development a API aceita requisições de qualquer origem (CORS), para o Expo na web; nos demais ambientes, só as origens de `Cors__AllowedOrigins__N` (veja o [README](../README.md#cors)).
- Com a API no ar em Development, o documento OpenAPI fica em `/openapi/v1.json`, gerado do código: quando divergir deste contrato, o código manda e este arquivo é corrigido no mesmo PR.
