# ADR-0026 — Execução da IA: passo fixo na banda DECISION, fases e intervalos

- **Status:** Aceita (auditoria do design do M6, 2026-10-13; proposta no v1) · **Milestone:** M6
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md) e [ADR-0004](0004-command-buffers.md) (revisão parcial), [ADR-0019](0019-abilities-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §3.3, §12.5, §13

## Contexto
O ADR-0003 colocou as Behaviour Trees em `Update` (banda `DECISION`) e o ADR-0004 manda a IA escrever comandos em
`Update`. Desde então Character, Combat e Abilities simulam no passo fixo, com testes medidos por `StepIndex` e
equivalência com ordem embaralhada. Uma IA em `Update` decidiria conforme o frame rate e entregaria bordas com
latência variável.

## Decisão
1. **A IA decide em `FixedUpdate`**, na banda `DECISION` (−900), antes de HFSM, Abilities, Combat e Character.
2. Fases: `DECISION + 0` (`AIWorld`: passo, snapshot P0, estímulos de *n − 1* → caixas dos agentes),
   `DECISION + 20` (agentes), `DECISION + 60` (`AIWorld`: arbitragem de engajamento). Helpers de fase ocultos.
3. Avaliação a cada `EvaluationInterval` passos (padrão 1), escalonada por `AIEntityId`; Δt do tick = passos desde a
   última avaliação × Δt fixo; relógio do agente em segundos de jogo.
4. Um agente só escreve no próprio estado, nos próprios canais (bridges), na fila de pedidos e no barramento; lê outros
   atores pelo snapshot P0 ou por estado somente-leitura dos packages.
5. **Revisão parcial dos ADRs 0003 e 0004, sem apagar as decisões históricas:**
   - a linha "Behaviour Trees" da tabela de bandas do ADR-0003 passa a valer em `FixedUpdate`;
   - a regra "AI escreve na banda `DECISION` (`Update`)" do ADR-0004 passa a "AI escreve na banda `DECISION` no passo
     fixo";
   - **fontes de input do jogador continuam em `Update`** (`COMMAND_SOURCES`), sem mudança;
   - as bandas continuam **convenção de execução, não scheduler**;
   - HFSM com `EvaluationLoop = FixedUpdate` consome as bordas da IA **no mesmo passo fixo**; HFSM em `Update`
     mantém a latência entre loops do ADR-0003 (claim no `Update` seguinte, execução no próximo passo fixo);
   - nenhum código do Core ou da HFSM muda.

## Alternativas consideradas
- **`Update`, como no ADR-0003:** decisões dependentes do frame rate; testes por passo e equivalência de ordem
  inviáveis.
- **`Update` com acumulador de passos próprio:** duplica o relógio da Unity e entrega bordas fora de fase.

## Consequências
- Positivas: decisões idênticas para qualquer frame rate e `timeScale` (equivalência lógica); latência zero até
  executores no passo fixo.
- Negativas / custos: frames sem passo fixo não decidem (nada é executado neles); custo por passo controlado por
  intervalos.
- Documentação: notas de revisão nos ADRs 0003/0004; CONTRACTS do AI com fases e latências.

## Ajustes da revisão
Formalizada a revisão parcial dos ADRs 0003/0004 (item 5) com notas nos próprios ADRs; nenhuma regra temporal
contraditória permanece na documentação do framework.
