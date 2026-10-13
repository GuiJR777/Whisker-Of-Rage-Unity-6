# ADR-0027 — Modelo de execução da Behaviour Tree: conjunto ativo, aborts e orçamento

- **Status:** Aceita (auditoria do design do M6, 2026-10-13; proposta no v1) · **Milestone:** M6
- **Relacionada:** [ADR-0026](0026-ai-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §5

## Contexto
A BT precisa ser reativa, barata com dezenas de agentes, sem alocação e com semântica fechada, inclusive com ramos
paralelos. Reavaliar a árvore inteira a cada tick reinicia nós em execução; observadores puros são difíceis de manter
em ordem total.

## Decisão
1. **Ciclo de vida por ativação:** `OnStart` exatamente uma vez, antes de qualquer `OnTick`; `OnTick` no máximo uma
   vez por tick da árvore; `OnStop(reason)` exatamente uma vez (`Completed`, `Aborted`, `TreeStopped`), inclusive em
   abort antes do primeiro tick; cascatas da folha para a raiz (irmãos de `Parallel` em ordem reversa).
2. **Conjunto ativo** persistido; `Parallel` mantém **vários ramos ativos**, cada filho com estado e memória próprios;
   filho terminado guarda o resultado e não reinicia na mesma ativação. Tick: fase de aborts → services devidos →
   execução em pré-ordem.
3. **`Parallel`:** `SuccessPolicy`/`FailurePolicy` ∈ {`RequireOne`, `RequireAll`} avaliadas após todos os filhos do
   tick; ambas satisfeitas no mesmo tick → `ConflictPolicy` (`FailureWins` padrão, `SuccessWins`); ao decidir, filhos
   ainda ativos param com `Aborted`.
4. **Aborts:** `Self` — o nó ativo cuja condição deixa de valer para (o pai recebe `Failure`); `LowerPriority` — um ramo
   inferior do mesmo `Selector` ativo para quando um superior se torna elegível, que é ativado; `Both` — as duas.
   Candidatos em pré-ordem do conjunto ativo; o primeiro verdadeiro vence; **um abort por tick**; nós ativados no
   recomeço só são candidatos no tick seguinte (sem cascata). `LowerPriority` só em filhos de `Selector`.
5. **Orçamento de trabalho:** unidades = `OnStart`, `OnTick`, avaliação de condição, service, `OnStop`; aborts e
   paradas em cascata são atômicos (contados, nunca interrompidos); esgotado, o tick para **entre operações** com
   estado consistente e continua no tick seguinte.
6. **Canais de saída:** primeiro escritor em pré-ordem vence por tick; escritor recusado fica `Running` (ou falha com
   `FailOnConflict`); canal de estado volta ao neutro quando o detentor para.
7. Reinício no tick seguinte ao término da raiz; `Restart/Stop` param tudo e liberam escopos, canais e rastreios.

## Alternativas consideradas
- **Reativo puro (reavaliar da raiz):** reinicia ações e custa O(nós) por tick.
- **Observadores de Blackboard como único gatilho:** condições fora do Blackboard (distância, estado do Combat)
  exigiriam serviços extras.
- **Orçamento só de nós iniciados:** não limita ticks de `Parallel` largos nem condições caras.

## Consequências
- Positivas: semântica fechada e testável (AC3); determinismo; zero GC.
- Negativas / custos: condições com abort avaliadas a cada tick; canais exigem declaração por ação.
- CONTRACTS do AI: ciclo de vida, `Parallel`, aborts, orçamento, canais.

## Ajustes da revisão
Incorporados: vários ramos ativos no `Parallel` com política de conflito explícita, ciclo de vida exato por
ativação, definição precisa de `Self`/`LowerPriority`/`Both`, um abort por tick sem cascata, orçamento de trabalho
relevante com parada consistente, canais de saída.
