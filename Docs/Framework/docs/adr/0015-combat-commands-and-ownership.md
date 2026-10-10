# ADR-0015 — Comandos de combate, prioridade e requisição em duas fases

- **Status:** Proposta (design do M4) · **Milestone:** M4
- **Relacionada:** [ADR-0004](0004-command-buffers.md) (decisões adiadas para o M4), [ADR-0011](0011-character-command-buffer.md),
  especificação do M3 §19.4 e §19.8, [especificação do M4](../design/m4-combat-design.md) §9

## Contexto
O ADR-0004 adiou para o M4 a janela do input buffer de combo e a prioridade entre bordas simultâneas. O Character
definiu o padrão de bordas tokenizadas, dono declarado e requisição em duas fases com cancelamento (aprovado no M3).
O ADR-0011 previu mover o tipo genérico de fila de bordas para o Core no segundo consumidor.

## Decisão
1. `CombatCommandBuffer`: estado (`GuardHeld`, direção) e bordas de ação (`CombatActionDefinition` + direção) com
   token e instante de jogo; as 6 garantias do ADR-0004.
2. Janela de idade das bordas = `InputBufferTime` do perfil de combate; prioridade entre bordas simultâneas por
   `CombatActionDefinition.Priority` (maior primeiro; empate → mais antiga).
3. `ActionOwner` = `Combat` (o combo runner consome quando consegue executar) ou `External` (estado da HFSM:
   `RequestAction(token)` consome no passo em que executa; `CancelActionRequest(token)` com as garantias da §19.8 do
   M3 — não executa, descarta a borda, registra e notifica uma vez, token antigo não cancela pedido novo).
4. Fila de bordas: conforme a decisão D5 — `EdgeQueue<TPayload>` no Core 0.4.0 (recomendado) ou cópia interna.

## Alternativas consideradas
- **Só a HFSM decide ataques:** cada jogo precisaria de um grafo de estados para combos simples.
- **Só o combo runner:** sem como compor com hitstun, morte e cancelamentos de outros domínios (R9).

## Consequências
- Positivas: mesmo modelo mental de Character e Combat; jogador e IA escrevem as mesmas bordas.
- Negativas / custos: se D5 = Core, o Core ganha um tipo (Core 0.4.0) e o Character migra sua fila interna no próximo
  minor, sem mudar API pública.
