# ADR 0001 — Links de ação nas respostas da API

- **Status:** aceito
- **Data:** 2026-11-04

## Contexto

O app (Expo) precisa saber quais ações o aluno pode fazer em cada estado: começar a
lição de um tópico desbloqueado, ir para a próxima questão, revisar um flashcard
vencido. Se o app decidir isso sozinho, as regras do grafo de pré-requisitos e do
SM-2 passam a existir em dois lugares (C# e TypeScript), o que contraria o RNF007:
o cliente não calcula nota, status nem agendamento.

A saída é HATEOAS: a resposta traz, como links, as ações permitidas naquele estado,
e o app mostra uma ação só quando o link correspondente existe.

## Opções consideradas

1. **HAL** (`application/hal+json`): objeto `_links` indexado pelo `rel`, mais
   `_embedded` para recursos aninhados. Simples e com bibliotecas, mas o link não
   informa o método HTTP; o app teria que saber por fora que `start-lesson` é `POST`.
2. **JSON:API**: especificação completa (`data`/`attributes`/`relationships`/`links`,
   paginação, filtros, erros). Padronizado, mas muda o formato de todas as respostas
   e também não informa o método das ações.
3. **Formato próprio**: uma lista `links` de `Link(Href, Rel, Method)` em cada recurso.

## Decisão

Formato próprio (opção 3):

```json
"links": [
  { "href": "https://api/topics/0192f0c4-...", "rel": "self", "method": "GET" },
  { "href": "https://api/topics/0192f0c4-.../lessons", "rel": "start-lesson", "method": "POST" }
]
```

- Links são assunto de HTTP e ficam **só no projeto Api**. A Application devolve o
  estado (`IsUnlocked`, `IsDue`...); o controller o transforma em links. Domain e
  Application não conhecem URL.
- As URLs vêm de rotas nomeadas (`[HttpGet(..., Name = "GetTopic")]`) e de
  `Url.Link(...)`, que gera a URL absoluta; nenhuma URL é escrita à mão.
- Todo recurso tem `self`. Links de ação só aparecem quando a ação é permitida.
- `POST` que cria recurso devolve `201 Created` com `CreatedAtRoute`.

## Consequências

- O app depende só dos `rel` documentados em `LinkRelations`, não das regras nem das
  rotas: mudar o limiar de desbloqueio ou um caminho não exige release do app.
- O formato informa o método, que é o que o app precisa para disparar a ação.
- Não há bibliotecas prontas para o formato. Com três campos, não fazem falta.
- Cada endpoint novo precisa montar seus links e ter testes funcionais cobrindo o
  link no estado permitido e a ausência dele no bloqueado.
