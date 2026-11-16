# ADR-003 — Repetição espaçada por um port no Domain, com estado opaco e o cartão preso ao algoritmo

- **Status:** aceita
- **Referência:** issue #20 (`StudentFlashcard` e o agendamento de revisões), com as sub-issues #21 (port e adapter do SM-2), #22 (criação e estado) e #23 (revisão).

## Contexto

O erro do aluno vira um flashcard que volta para revisão no momento certo: é o núcleo do diferencial do projeto. O documento do projeto prevê o SM-2, e o schema original guardava o estado dele em colunas próprias de `aluno_flashcard` (`repeticoes`, `intervalo` e `fator_facilidade`) e recebia a nota de 0 a 5 do SM-2.

Queremos começar com o SM-2 e poder trocá-lo depois, por exemplo pelo FSRS, que usa outro estado e outra escala de nota. Com o SM-2 espalhado pelo agregado e pelo schema, a troca mexeria no `StudentFlashcard`, na tabela e em todo dado já gravado.

## Decisão

**O agendamento é um port, e cada algoritmo é um adapter.**

```
StudentFlashcard ──usa──> IReviewScheduler (port, no Domain)
                                ▲
                                │ implementa
                          Sm2Scheduler (adapter)   [depois: FsrsScheduler]
```

- **O port mora no Domain**, porque o `StudentFlashcard` precisa dele e o Domain não depende de nada. O adapter do SM-2 também fica no Domain: é lógica pura, sem I/O. Um adapter futuro que precise de biblioteca, de parâmetros ajustados ou de I/O (caso provável do FSRS) pode morar na Infrastructure.
- **O port só conversa com tipos neutros ao algoritmo.** A entrada é uma nota de 4 níveis (`ReviewRating`: `Again`, `Hard`, `Good`, `Easy`), o denominador comum entre algoritmos, que também deixa o app oferecer 2 ou 4 botões. O adapter do SM-2 a traduz para a nota original (`Again` 1, `Hard` 3, `Good` 4, `Easy` 5), então `Hard` ainda é acerto e reduz o fator, e `Easy` o aumenta. A saída é o `ReviewOutcome`: o estado e a próxima revisão.
- **O estado de agendamento é opaco.** O agregado o guarda como texto e não o interpreta; só o adapter o escreve e o lê. No banco, `aluno_flashcard` tem `algoritmo`, `estado_agendamento` (`jsonb`) e `proxima_revisao`, que continua como coluna própria e indexada porque é por ela que as revisões do dia são buscadas. No SM-2, o estado é `{"repetitions":0,"intervalDays":1,"easeFactor":2.5}`.
- **O cartão fica preso ao algoritmo com que nasceu.** O agregado guarda o nome do algoritmo, e revisar com um scheduler de outro algoritmo é erro (`studentFlashcard.scheduler.mismatch`): um adapter nunca lê um estado que não escreveu.
- **Revisar antes do vencimento é permitido no Domain**, e a data da próxima revisão parte do instante real da revisão, não da data agendada. Oferecer a revisão só quando o cartão venceu é regra da API.
- **O SM-2 é o original** (Wozniak): fator inicial 2,5 e mínimo 1,3, em `decimal` com 2 casas; estado inicial de 0 repetições e 1 dia, seguindo a fórmula à risca (a primeira revisão correta mantém 1 dia e a segunda leva a 6).

Qual adapter está ativo é escolhido no composition root, quando existir o caso de uso de revisão.

## Alternativas consideradas

- **O SM-2 direto no agregado, com colunas próprias.** É o schema original e o caminho mais curto. Não adotado: trocar de algoritmo mexeria no agregado, na tabela e nos dados gravados.
- **A nota de 0 a 5 do SM-2 no port.** Não adotada: amarra o port ao SM-2, e o FSRS usa 4 níveis.
- **Estado tipado por algoritmo no agregado** (uma propriedade ou subclasse por algoritmo). Não adotado: o Domain passaria a conhecer cada algoritmo, que é exatamente o que o port evita.
- **Converter o estado ao revisar com outro algoritmo.** Não adotado por ora: a conversão entre SM-2 e FSRS não é óbvia e depende do adapter novo.

## Consequências

- **Trocar de algoritmo é um adapter novo**, sem mexer no `StudentFlashcard` nem no schema. Como migrar os cartões que já existem (reiniciar ou converter o estado) é decisão da issue desse adapter.
- **O estado deixa de ser coluna.** A análise do experimento consulta o `jsonb`, que o PostgreSQL indexa e filtra, em vez de colunas tipadas.
- **O schema guarda só o estado atual do cartão.** Se a análise precisar do histórico de revisões, para medir retenção, isso é uma tabela e um agregado novos.
- **Revisitar quando:** entrar o segundo algoritmo, ou a análise precisar do histórico de revisões.
