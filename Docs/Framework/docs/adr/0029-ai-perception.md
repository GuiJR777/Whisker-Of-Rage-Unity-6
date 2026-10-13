# ADR-0029 — Percepção por registro, snapshot e estímulos com ordem total

- **Status:** Proposta (design v1 do M6, 2026-10-13) · **Milestone:** M6
- **Relacionada:** [ADR-0009](0009-ai-optional-stats.md), [ADR-0026](0026-ai-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §7

## Contexto
Sensores baseados em consultas de física dependem de camadas, saturam buffers e devolvem colliders em ordem não
garantida; eventos de outros packages (dano na fase 4 do Combat) chegam depois da banda `DECISION`. A auditoria do
M5 mostrou que toda saturação precisa de top-K pela chave canônica.

## Decisão
1. `AIWorld` mantém o registro de `AIPerceivable` e publica em P0 um snapshot (id, pose, pontos de mira, time,
   detectabilidade, flags das integrações). Sensores percorrem o snapshot com pré-filtro de distância; a física só
   responde a linha de visão (booleano).
2. Sensores SR (`Sight` 2.5D, `Proximity`, `Hearing`, `Stimulus`) com intervalos escalonados; detectabilidade por
   sentido no percebível (`IDetectabilitySource`); time por `IAITeamSource`.
3. Estímulos (`AIStimulus`) emitidos no passo *n* são publicados em P0 de *n + 1* em ordem (passo, origem,
   sequência), com capacidade fixa e top-K.
4. Memória por agente com capacidade fixa, top-K pela chave total da especificação, esquecimento por tempo e ameaça.

## Alternativas consideradas
- **Overlap de física por sensor:** dependente de camadas e da ordem de colliders; saturação difícil de tornar
  determinística.
- **Callbacks de trigger:** fora das bandas (ADR-0003).

## Consequências
- Positivas: percepção determinística e independente da ordem; nenhuma camada exigida para descoberta.
- Negativas / custos: O(agentes × percebíveis) por avaliação, mitigado por intervalos (R35); percebíveis precisam do
  componente.
- CONTRACTS do AI: sensores, estímulos, capacidades e contadores.
