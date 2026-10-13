# ADR-0031 — Coordenação de engajamento por tokens e vagas

- **Status:** Proposta (design v1 do M6, 2026-10-13) · **Milestone:** M6
- **Relacionada:** [ADR-0026](0026-ai-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §9

## Contexto
Em beat'em ups vários inimigos cercam o jogador, mas poucos atacam ao mesmo tempo e eles se distribuem ao redor
dele. Sem coordenação, todos atacam juntos e se amontoam; com coordenação ad hoc em cada agente, o resultado depende
da ordem de execução.

## Decisão
1. `EngagementProfileDefinition` no alvo (`AIEngagementTarget`): pools de tokens por `GameplayTag` (capacidade,
   posse máxima) e um anel de vagas no frame do facing do alvo, com tags de preferência.
2. Agentes pedem tokens/vagas durante o tick; na fase `DECISION + 60` o `AIWorld` processa alvos por `AIEntityId` e
   pedidos por (prioridade ↓, score ↓, id ↑); vagas por atribuição gulosa (vaga livre compatível mais próxima,
   índice).
3. Tokens liberados por pedido, tempo máximo, parada do nó, desabilitar/morrer, alvo perdido; concessões visíveis no
   tick seguinte. Capacidades fixas com saturação contada.

## Alternativas consideradas
- **No jogo:** cada jogo reescreveria a mesma coordenação, essencial ao gênero.
- **Atribuição ótima (húngaro) de vagas:** mais cara, sem ganho perceptível na escala do framework.

## Consequências
- Positivas: hordas legíveis e determinísticas; a BT só pede e consulta.
- Negativas / custos: uma fase a mais no `AIWorld`; latência de um tick na concessão.
- CONTRACTS do AI: perfil, pedidos, ordem de arbitragem.
