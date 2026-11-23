# ADR-005 — O cartão de revisão nasce para os dois grupos experimentais

- **Status:** aceita
- **Referência:** issue #88 (`POST /exercises/{id}/attempts`). Fecha as decisões PEND-RSP-01 e PEND-REV-01 dos SRS de Resposta e Diagnóstico e de Revisão Espaçada.

## Contexto

O experimento do TCC compara dois grupos de alunos. O grupo **diagnóstico** recebe, ao errar, o nome e o feedback do tipo de erro; o grupo **controle** recebe só a informação de que errou (`AnswerDiagnostician.BuildFeedback`). A diferença entre os dois é o que o experimento mede.

Errar também gera um flashcard de revisão, e o flashcard é **do tipo de erro**: a pergunta dele repete o diagnóstico ("Por que 1/2 + 1/3 não é 2/5?"). Isso cria uma pergunta de desenho: o aluno do grupo controle recebe o cartão?

- Se **recebe**, ele vê, ao revisar, o diagnóstico que o retorno imediato lhe nega.
- Se **não recebe**, os dois grupos passam a diferir em **duas** coisas (o retorno imediato e a revisão espaçada), e o experimento não separa o efeito de uma do da outra.

## Decisão

**O cartão é criado para o aluno dos dois grupos**, com o mesmo agendamento e a mesma revisão. Errar uma alternativa cria o `StudentFlashcard` do tipo de erro, uma vez por aluno e por tipo de erro (RN-RSP-10), qualquer que seja o grupo.

Assim, o que o experimento mede é **só o retorno imediato**: o diagnóstico no momento do erro. A revisão espaçada é a mesma para todos, e o efeito dela não se confunde com o do retorno.

## Alternativas consideradas

- **Cartão só para o grupo diagnóstico.** Mantém o controle sem nenhuma pista do diagnóstico, mas confunde dois efeitos: uma diferença de desempenho depois de uma semana poderia vir do retorno ou da revisão, e a análise não saberia dizer qual. Não adotado.
- **Cartão no controle com a pergunta genérica** (sem o diagnóstico). Evitaria o vazamento, mas exige um segundo texto por tipo de erro, um cartão diferente por grupo e um agendamento que já não é o mesmo. Muda o que se revisa, e não só o retorno. Não adotado.

## Consequências

- **O controle vê o diagnóstico ao revisar.** É um vazamento conhecido e aceito, e vira uma limitação a declarar na análise dos resultados: o grupo controle só não recebe o diagnóstico **no momento do erro**. A comparação entre os grupos vale para o retorno imediato.
- O caso de uso `SubmitAttempt` não olha o grupo para decidir o cartão; o grupo só decide o `feedback` da resposta. A estrutura da resposta é a mesma nos dois grupos (RN-RSP-05).
- A tela de revisão (`GET /reviews/due`) e o agendamento são iguais para os dois grupos, e o app não precisa de um caminho para o controle sem cartão.
- Se o experimento pedir, trocar a decisão é mudar uma condição no caso de uso e a tela de revisão do controle, sem mexer no modelo de dados: o cartão já é uma linha por aluno e flashcard.
