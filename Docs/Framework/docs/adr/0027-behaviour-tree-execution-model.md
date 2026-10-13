# ADR-0027 — Modelo de execução da Behaviour Tree: caminho ativo, aborts e orçamento

- **Status:** Proposta (design v1 do M6, 2026-10-13) · **Milestone:** M6
- **Relacionada:** [ADR-0026](0026-ai-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §5

## Contexto
A BT precisa ser reativa (abandonar um ramo quando a situação muda), barata com dezenas de agentes, sem alocação e
com semântica testável. Reavaliar a árvore inteira a cada tick reinicia nós em execução; modelos puramente orientados
a observadores são difíceis de garantir em ordem total.

## Decisão
1. Nós SR com `OnStart`/`OnTick`/`OnStop` e `NodeStatus` {`Running`, `Success`, `Failure`}; `OnStop` exatamente
   uma vez com `StopReason` {`Completed`, `Aborted`, `TreeStopped`}, da folha para a raiz.
2. A instância persiste o caminho ativo; um tick retoma a folha ativa e continua pelos pais no mesmo tick quando nós
   terminam instantaneamente, até o `TickBudget` (nós iniciados por tick; excedido → `Running` e contador).
3. Decorators e services **anexados** aos nós. Decorator de condição com `Abort` {`None`, `Self`, `LowerPriority`,
   `Both`}. A cada tick, antes de retomar a folha, o caminho é percorrido da raiz para a folha: em cada `Selector`, os
   aborts `LowerPriority/Both` dos filhos de maior prioridade, em ordem; em cada nó, os `Self/Both`. O primeiro
   verdadeiro vence (no máximo um abort por tick); o ramo para (`Aborted`) e a execução recomeça no mesmo tick.
4. `Parallel` só com políticas explícitas; `Repeat` reinicia o filho no máximo uma vez por tick; exceção de nó vale
   `Failure` com log. Memória por agente e por nó criada uma vez.

## Alternativas consideradas
- **Reativo puro (reavaliar da raiz):** simples, porém reinicia ações e custa O(nós) por tick.
- **Observadores de Blackboard (estilo Unreal) como único gatilho:** eficiente, mas condições que não dependem do
  Blackboard (distância, estado do Combat) exigiriam serviços extras; versões por chave ficam como otimização.
- **Decorators como nós de um filho:** mais nós no grafo e aborts menos legíveis.

## Consequências
- Positivas: semântica fechada e testável (AC3); aborts determinísticos; zero GC.
- Negativas / custos: condições com abort são avaliadas a cada tick (custo linear nas condições observadas).
- CONTRACTS do AI: ciclo de vida, ordem dos aborts, orçamento, políticas.
