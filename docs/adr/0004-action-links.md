# ADR-004 — Links de ação nas respostas, em formato próprio e montados só na API

- **Status:** aceita
- **Referência:** issue #8 (links HATEOAS nas respostas da API REST). O contrato dos links está na seção **Links** do [`docs/api.md`](../api.md).

## Contexto

O app (Expo) precisa saber quais ações o aluno pode fazer em cada estado: abrir a lição de um tópico liberado, responder um exercício, revisar um cartão vencido. Se o app decidir isso sozinho, as regras do grafo de pré-requisitos e do agendamento passam a existir em dois lugares (C# e TypeScript), e o servidor deixa de ser a única fonte da regra: o app não calcula nota, desbloqueio nem agendamento.

A saída é HATEOAS: cada resposta traz, como links, as ações permitidas naquele estado, e o app mostra um botão só quando o link correspondente existe.

## Decisão

**Formato próprio: cada recurso traz uma lista `links` de `Link(Href, Rel, Method)`.**

```json
"links": [
  { "href": "http://localhost:5007/me", "rel": "self", "method": "GET" }
]
```

- **`href` é absoluta.** O app segue o link sem conhecer a URL base da API, sem concatenar texto e sem resolver URL relativa.
- **`rel` diz o que a ação significa.** É por ele que o app procura o link, então os valores são contrato: estão na tabela do `docs/api.md` e em `LinkRelations`, e um valor publicado não muda de nome.
- **`method` diz como disparar a ação.** É o que o app precisa para chamar um `POST` de uma ação sem saber da rota.

**Os links são assunto de HTTP e ficam só no projeto Api.** A Application devolve o estado (o status do tópico, se o cartão venceu), e o controller o transforma em links. Domain e Application não conhecem URL.

**Nenhuma URL é escrita à mão.** Toda action que é destino de link tem uma rota nomeada (`[HttpGet(Name = MeController.GetMeRoute)]`), com o nome numa constante, e o link sai de `Url.LinkTo(rel, method, routeName, values)`, que usa o `Url.Link` do ASP.NET Core. O template da rota fica num lugar só, o atributo da action, e a URL gerada já vem codificada e com o esquema e o host da requisição. Uma rota que não gera URL (nome inexistente ou valor faltando) é exceção, e não um `href` nulo.

**Regras de presença:**
- Todo recurso tem `self`, sempre.
- Um link de ação só aparece quando a ação é permitida naquele estado. A ausência do link é a resposta "não pode".
- Um `POST` que cria recurso responde `201 Created` com `CreatedAtRoute`, e o `Location` aponta para o recurso criado. O cadastro (`POST /auth/register`) aponta para o `/me`, o perfil que ele criou.

## Alternativas consideradas

- **HAL** (`application/hal+json`): um objeto `_links` indexado pelo `rel`, mais `_embedded` para recursos aninhados. É simples e tem bibliotecas, mas o link não informa o método: o app teria de saber por fora que responder um exercício é `POST`. Não adotado.
- **JSON:API**: uma especificação completa (`data`, `attributes`, `relationships`, `links`, paginação, erros). É padronizada, mas muda a forma de todas as respostas e também não informa o método das ações. Não adotado.
- **O app decide as ações pelo estado** (`status: "locked"` esconde o botão). É o caminho mais curto, mas duplica a regra no cliente, e cada mudança de regra exige uma versão nova do app. Não adotado.

## Consequências

- O app depende só dos `rel` documentados, e não das regras nem das rotas: mudar o limiar de domínio ou um caminho não exige versão nova do app.
- Não há biblioteca pronta para o formato. Com três campos, ela não faz falta.
- Cada endpoint novo monta os próprios links e entra com testes funcionais: o link presente no estado permitido, ausente no bloqueado, e a URL absoluta da rota certa.
- Atrás de um proxy reverso, o host da requisição é o do proxy. Quando a API for implantada, o pipeline precisa do `ForwardedHeaders`, senão os links saem com o endereço interno.
- **Entregue:** todos os links do contrato. O tipo `Link`, o `Url.LinkTo`, o `self` e o `trail` do `/me`, o `self` e o `reviews` do `/trail` com o `topic` de cada item, os links do tópico (`self`, `trail`, e, só com o tópico liberado, o `lesson` de cada lição e o `attempts`), o do histórico (`self` e `topic`) e da lição (`self`, `topic` e o `answer` de cada exercício), o `topic` e o `reviews` da resposta de `POST /exercises/{id}/attempts`, e as revisões (`self` e `trail` na lista, `review` em cada cartão vencido, `reviews` na revisão feita). Um link entrou sempre junto com a rota dele, porque o `Url.LinkTo` recusa uma rota que não existe. Um `POST` que cria algo sem um recurso próprio para ler (a tentativa) responde `201` sem `Location`.
