# ADR-002 — Leitura por portas de leitura na Application, sem caso de uso nem repositório

- **Status:** aceita
- **Referência:** issue #60 (`GET /me`), o primeiro caminho de leitura da API. O padrão vem do `icrhub` (ADR-003, a separação entre escrita e leitura, e ADR-012, o EF Core na camada Application para leitura), adaptado ao REST e ao tamanho deste projeto.

## Contexto

Até aqui toda a API era escrita: cadastro, login, renovação e logout gravam, e cada um passa por um caso de uso (`*Handler`) que usa os repositórios e a unidade de trabalho. Os repositórios existem para o **agregado na escrita**: carregam o agregado inteiro, o domínio muda o estado dele e a `IUnitOfWork` grava.

O `GET /me` é a primeira leitura: devolve o perfil de quem está logado (a conta e o aluno ligado a ela). Fazê-la pelo caminho da escrita seria o caminho natural e tem três custos:

1. **Carrega o agregado para devolver três campos.** O `Student` é mapeado só com `id`, `name` e `experimental_group` (o progresso por tópico ainda não está mapeado), então um `Student` carregado volta sempre com o `Progress` vazio. Quem depois o usasse para `Unlock` ou `MarkMastered` perderia o progresso sem erro nenhum.
2. **Lê colunas que nunca devem sair.** O `User` tem o hash da senha e a versão do pepper.
3. **Um caso de uso que só repassa é uma camada morta.** Sem regra de negócio, ele só empurra a chamada do controller para o repositório.

E a Application não podia fazer a consulta sozinha: ela não enxerga o EF Core, e a leitura precisa de `FirstOrDefaultAsync` para materializar.

## Decisão

**Leitura e escrita são caminhos separados.**

```
escrita   controller → handler (caso de uso) → repositório + IUnitOfWork   (o agregado)
leitura   controller → IXxxReads (porta de leitura) → banco                (um DTO)
```

- A leitura **não passa por caso de uso nem por repositório**. O controller chama a porta de leitura direto. O repositório continua sendo só o agregado na escrita.
- Cada consulta é uma porta `IXxxReads`, com a implementação `XxxReads` ao lado, em `Application/<Módulo>/Queries/`. Hoje existe uma: `IProfileReads`, em `Application/Students/Queries/`.
- A consulta devolve **um DTO só com as colunas do contrato** (`ProfileResponse`), montado por uma projeção (`Select`). Nenhum agregado é carregado, e o hash da senha nunca é lido.
- A consulta lê o banco por **`IApplicationDbContext`**, uma interface em `Application/Common` que expõe cada agregado como `IQueryable<T>`, sem `SaveChanges`, `Add` nem rastreamento. O `AppDbContext` a implementa, sem rastreamento (`AsNoTracking`), então uma leitura nunca deixa entidade no contexto.
- **A consulta mora na Application, e não na Infrastructure.** O que a resposta contém, de onde cada campo vem e quando a conta conta como existente são decisões da aplicação, e ficam junto do contrato. Para materializar, a Application passa a referenciar o `Microsoft.EntityFrameworkCore`. Esse é o único uso permitido: o ponto de materialização leva um comentário que explica por que existe.

**O que esta decisão não muda**
- A escrita segue exclusivamente por repositório e `IUnitOfWork`. `SaveChanges`, `Add` e o `AppDbContext` concreto continuam fora da Application.
- O `AppDbContext`, o mapeamento (`IEntityTypeConfiguration`) e as migrations continuam só na Infrastructure.
- O Domain continua sem dependência de pacote.

**A fronteira vira regra testada.** Com o EF Core na Application, o compilador deixa de impedir que um caso de uso use o contexto. `ApplicationBoundaryTests` (em `Application.UnitTests`) devolve a barreira: fora das classes `*Reads`, nenhum tipo da Application depende do `Microsoft.EntityFrameworkCore` nem do `IApplicationDbContext`. Um terceiro teste, de controle, prova que as regras enxergam as dependências, e não passam no vazio.

**Duas formas de leitura.** As leituras aqui são **consultas fechadas**: o servidor decide o que devolver (o `/me`, e as próximas, como o progresso). Uma consulta que o cliente comporia por conta própria (filtro, ordenação e paginação abertos) é outra forma de leitura, que o projeto ainda não tem; se aparecer, volta a este ADR.

## Alternativas consideradas

- **Um `FindByIdAsync` no `IStudentRepository` e um caso de uso `GetProfile`.** Não adotado, pelos três custos do contexto: carrega o `Student` com o `Progress` vazio, é uma camada que só repassa, e o repositório ganharia um método de leitura que o agregado não pede.
- **A mesma porta de leitura, com a implementação na Infrastructure.** Preserva a regra de que só a Infrastructure conhece o EF Core, e era a primeira inclinação. Não adotada: a projeção e o contrato são a mesma decisão, e separá-los em duas camadas obriga quem muda o `/me` a mexer em dois projetos. A fronteira que se perde é recuperada pelo teste de arquitetura.
- **`IQueryable` direto no controller, sem porta.** Não adotado: tira o `DbContext` do controller só na aparência, e a consulta ficaria sem lugar nem teste próprios.
- **Um `IQuery`/`IQueryHandler` genérico (CQRS completo).** Não adotado por ora: com uma única consulta, o mediador seria infraestrutura sem uso. Entra se as consultas passarem a ter comportamento em comum (cache, auditoria).

## Consequências

- **A Application passa a referenciar o EF Core.** A regra "só a Infrastructure conhece o EF Core" deixa de valer, e a garantia do compilador vira convenção, revisão e o teste de arquitetura. É o débito que esta decisão assume de propósito. Um teste de arquitetura só enxerga dependência de tipo: renomear um caso de uso para terminar em `Reads` o isentaria, e isso fica para a revisão.
- **Os testes de leitura precisam de um provider relacional.** `ProfileReads` é testada contra o PostgreSQL real, em `Infrastructure.IntegrationTests` (que já tem a fixture), e não com dublê: um provider em memória deixaria passar uma projeção que o PostgreSQL não traduz. Os testes de unidade da Application continuam usando NSubstitute para os casos de uso.
- **A leitura e o agregado podem divergir.** O DTO é uma projeção feita à mão; se um campo mudar no agregado, a consulta precisa mudar junto. O teste de integração e o funcional do `/me` pegam a divergência.
- **Cada consulta nova é um arquivo novo em `Queries/`**, com a porta, a implementação e o DTO, e entra em `AddApplication`.
- **Revisitar quando:** o projeto precisar de leituras abertas (filtro e paginação pelo cliente), as consultas ganharem comportamento em comum, ou o número de consultas pedir uma separação física da Application.
