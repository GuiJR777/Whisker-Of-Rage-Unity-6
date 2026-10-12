# ADR-0022 — Comandos de habilidade e posse em duas fases

- **Status:** Proposta (design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** [ADR-0004](0004-command-buffers.md), [ADR-0011](0011-character-command-buffer.md),
  [ADR-0015](0015-combat-commands-and-ownership.md), [especificação do M5](../design/m5-abilities-design.md) §5.3

## Contexto
Jogador e AI precisam pedir habilidades pelo mesmo caminho; a HFSM precisa reivindicar o pedido (estado de habilidade)
sem que o pedido execute duas vezes, como já ocorre com pulo/dash (Character) e ações de combate (Combat).

## Decisão (proposta)
1. `AbilityCommandBuffer` com bordas sobre `EdgeQueue` do Core 0.4.0 (3.º consumidor): token, instante, prioridade e
   payload (tag de entrada, direção/mira, alvo ou ponto sugerido, valores `SetByCaller` opcionais).
2. Habilidades são endereçadas por **tag de entrada** (`GameplayTag`), não por índice: a concessão define a tag;
   trocar a concessão troca o que o botão faz sem mexer no input.
3. Posse `Abilities` (o ASC consome a melhor borda válida) ou `External` (`RequestActivation(token)`,
   `CancelRequest(token)`, `ActivationResolved` uma vez), com as garantias do ADR-0015 (token antigo não cancela pedido
   novo, expiração = `Rejected`).
4. Estado segurado por tag de entrada (`SetHeld`) para canalizar/carregar; **sem** duração de botão na fila de bordas
   (decisão do M4).

## Alternativas consideradas
- **Ativação direta por API (`TryActivate`) apenas:** continua existindo para scripts, mas sem buffer/posse a AI e a
  HFSM duplicariam lógica.
- **Reutilizar o `CombatCommandBuffer`:** acoplaria Abilities ao Combat (não é dependência hard).

## Consequências
- Positivas: mesmo modelo mental em Character, Combat e Abilities; AI (M6) escreve só no buffer.
- Negativas / custos: mais um buffer por ator (componente leve, opcional para atores sem habilidades por comando).
