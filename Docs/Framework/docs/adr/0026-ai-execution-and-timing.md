# ADR-0026 — Execução da IA: passo fixo na banda DECISION, fases e intervalos

- **Status:** Proposta (design v1 do M6, 2026-10-13) · **Milestone:** M6
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md), [ADR-0004](0004-command-buffers.md), [ADR-0019](0019-abilities-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §3.3, §13

## Contexto
O ADR-0003 colocou as Behaviour Trees em `Update` (banda `DECISION`) e o ADR-0004 manda a IA escrever comandos em
`Update`. Desde então Character, Combat e Abilities passaram a simular no passo fixo, com testes medidos por
`StepIndex` e equivalência com ordem embaralhada. Uma IA em `Update` tomaria decisões dependentes do frame rate e
entregaria bordas com latência variável aos executores.

## Decisão
1. A IA roda em `FixedUpdate` na banda `DECISION` (−900), antes de HFSM, Abilities, Combat e Character.
2. Fases: `DECISION + 0` (`AIWorld`: passo, snapshot P0 dos percebíveis, publicação dos estímulos de *n − 1*),
   `DECISION + 20` (agentes: entradas diferidas → sensores → memória → services → tick da BT → saídas),
   `DECISION + 60` (`AIWorld`: arbitragem de engajamento). Helpers de fase ocultos, como no Combat e no Abilities.
3. Cada agente avalia a cada `EvaluationInterval` passos (padrão 1), escalonado por `AIEntityId`; Δt do tick =
   passos desde a última avaliação × Δt fixo; relógio do agente em segundos de jogo.
4. Um agente só escreve no próprio estado, nos próprios buffers (pelos adaptadores), na fila de pedidos e no
   barramento de estímulos; lê outros atores pelo snapshot P0 ou pelo estado somente-leitura dos packages.
5. Este ADR revisa, **para a IA**, a linha "Behaviour Trees em `Update`" do ADR-0003 e a regra "AI escreve na banda
   `DECISION` (`Update`)" do ADR-0004. Fontes de input do jogador continuam em `Update` (`COMMAND_SOURCES`).

## Alternativas consideradas
- **`Update`, como no ADR-0003:** decisões dependem do frame rate; testes por passo e equivalência de ordem
  inviáveis.
- **Tick no `Update` com acumulador de passos próprio:** duplica o relógio da Unity e ainda entrega bordas fora de
  fase.

## Consequências
- Positivas: borda escrita pela IA é consumida no mesmo passo (HFSM em `FixedUpdate`, posse do package);
  decisões idênticas para qualquer frame rate e `timeScale`; testes determinísticos.
- Negativas / custos: frames sem passo fixo não decidem (aceitável: nada é executado neles); custo por passo fixo
  controlado por intervalos e escalonamento.
- CONTRACTS do AI: fases, latências (§13 da especificação) e intervalos.
