# Contribuindo

Todo trabalho aqui começa numa **issue** e termina num **pull request**: uma issue, uma branch, um PR. Este guia diz como nomear a branch, como escrever os commits e o que o PR precisa ter. Para rodar o projeto, veja o [README](README.md); para escrever testes, [`docs/testing.md`](docs/testing.md).

## Fluxo

1. **Issue.** Se o trabalho não tem uma issue, abra uma antes. Ela descreve o escopo, o que fica de fora e as entregas. Uma tarefa que faz parte de outra maior é uma **sub-issue** da issue-mãe, e a branch usa o número da sub-issue.
2. **Branch**, com o nome da seção abaixo.
3. **Commits** pequenos, no formato da seção de commits.
4. **Pull request**, com o template do repositório e `Closes #NN` apontando para a issue.

## Nome da branch

```
task-<NN>-<nome-curto>
```

- **`NN`** é o **número da issue**, com pelo menos dois dígitos (`task-02-...`, `task-17-...`, `task-102-...`). Quem lê a branch chega na issue sem procurar.
- **`nome-curto`** diz o que a tarefa entrega, em duas a quatro palavras: minúsculas, dígitos e hífens, sem acento, sem espaço e sem `_`. Os nomes existentes estão em inglês, e a convenção segue isso.

Em forma de expressão regular: `^task-[0-9]{2,}-[a-z0-9]+(-[a-z0-9]+)*$`.

| Issue | Branch |
|---|---|
| #2 Adicionar workflow de CI de build | `task-02-ci-build-workflow` |
| #17 Agregado Topic no Domain | `task-17-topic-aggregate` |
| #18 Exigir chaves em todos os blocos de controle | `task-18-editorconfig-braces` |
| #26 Rodar o CI em PRs empilhados | `task-26-ci-stacked-prs` |

O que **não** segue a convenção:

| Evite | Por quê |
|---|---|
| `task-persistence` | sem o número da issue |
| `task-37` | sem o nome curto |
| `Task-37-Persistence`, `task_37_persistence` | maiúsculas e `_` |
| `feature/login`, `fix/bug` | outro esquema de prefixo |

As branches do Dependabot (`dependabot/...`) são criadas pela ferramenta e ficam como estão. `task-api-project` e `chore/pull-request-template` são anteriores a esta convenção e não são renomeadas.

Cada branch sai da `main`. Quando a tarefa **depende de outra que ainda não foi mergeada**, a branch sai da branch dessa outra (PR empilhado), e o PR usa essa branch como base.

## Commits

Os commits seguem o [Conventional Commits](https://www.conventionalcommits.org/pt-br/), com a descrição em português:

```
tipo(escopo): descrição curta, no presente, sem ponto final
```

- **Tipos** em uso: `feat`, `fix`, `refactor`, `test`, `docs`, `build`, `ci` e `chore`.
- **Escopo** é a parte afetada, e é opcional: `domain`, `application`, `infrastructure`, `api`, `tests`, `schema`, `readme`, `srs`. Quando o commit é só de uma camada, use o nome dela.
- A descrição é como o histórico já escreve: `adiciona o agregado Topic`, `exige chaves em todos os blocos de controle`.
- O **corpo** explica o porquê, não o que o diff já mostra. É o lugar da regra de negócio e da decisão.
- **Um commit por mudança coerente, e cada commit compila e passa os testes sozinho.** Prefira vários commits pequenos (a regra, depois os testes dela, depois o CI e a documentação) a um commit grande, para o histórico se ler e se reverter por partes.
- O último commit pode fechar a issue (`Closes #NN`), como no histórico.

## Pull request

- Use o [template](.github/pull_request_template.md): preencha **O que muda**, o **Tipo de mudança**, **Como testar** e o **Checklist**, e escreva `Closes #NN` para a issue ser fechada no merge.
- O título segue o formato dos commits (`feat(domain): adiciona o agregado Topic`).
- A base é a `main`, ou a branch de que a tarefa depende, num PR empilhado. Diga no corpo do PR em qual ele está empilhado. O CI roda em PRs de qualquer base.
- O CI (`build`) deve estar verde antes do merge.

## Qualidade

- `dotnet build -c Release` passa **sem warnings**: avisos são tratados como erro (`TreatWarningsAsErrors`).
- O `.editorconfig` exige chaves em todo bloco de controle, mesmo com uma única instrução.
- Regra de negócio entra com teste. Os testes ficam num projeto por camada, e as convenções (nomes, `DisplayName` em pt-BR, Arrange/Act/Assert, relógio injetado) estão em [`docs/testing.md`](docs/testing.md):

  ```bash
  dotnet test --project tests/Estudarias.Domain.UnitTests
  ```

- Mudou comportamento, configuração ou como rodar? Atualize o README ou o `docs/` no mesmo PR.
