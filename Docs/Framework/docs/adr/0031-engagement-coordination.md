# ADR-0031 — Escopo de engajamento por tokens e vagas

- **Status:** Aceita (auditoria do design do M6, 2026-10-13; proposta no v1) · **Milestone:** M6
- **Relacionada:** [ADR-0026](0026-ai-execution-and-timing.md), [ADR-0027](0027-behaviour-tree-execution-model.md), [especificação do M6](../design/m6-ai-design.md) §9

## Contexto
Em beat'em ups poucos inimigos atacam ao mesmo tempo e eles se distribuem ao redor do alvo. O v1 tinha ações
avulsas de pedir e liberar token: o token podia ser liberado ao completar o pedido, antes da aproximação e do ataque
que dependiam dele.

## Decisão
1. **Pedido, concessão e retenção separados**, todos no nó `EngagementScope` (um filho): pede token (e vaga) do pool
   do alvo lido de uma chave `Entity`; fica `Running` sem ativar o filho até a concessão (`WaitTimeout` opcional);
   concedido, ativa o filho e **retém** o token durante toda a ativação dele (aproximação, ataque, recuperação).
2. **Liberação exatamente uma vez por concessão:** filho termina; escopo abortado/parado; `MaxHoldTime` (revogação →
   filho abortado no tick seguinte); troca do alvo na chave observada; alvo inválido/morto/desregistrado; agente
   desabilitado, pausado, sem controle ou reiniciado. Não há ação avulsa de liberar.
3. **Arbitragem** na fase `DECISION + 60`: alvos por `AIEntityId`; por pool, liberações/revogações e depois pedidos
   por (prioridade ↓, score ↓, id do agente ↑); sem preempção; vagas por atribuição gulosa em ordem total;
   capacidades fixas com recusa contada (`Denied`).
4. **Handles** `EngagementTokenHandle` = (dono: `WorldId` + alvo + pool, vaga, geração); estrangeiros ou antigos são
   inertes.

## Alternativas consideradas
- **Ações avulsas de pedir/liberar:** liberação prematura ou esquecida; sem garantia de liberação única.
- **Decorator anexado:** precisaria esperar a concessão antes de ativar o nó, o que um decorator de condição não faz.
- **No jogo:** cada jogo reescreveria a coordenação essencial ao gênero.

## Consequências
- Positivas: retenção correta em sequências completas; liberação garantida; determinismo.
- Negativas / custos: latência de um tick na concessão; uma fase a mais no `AIWorld`.
- CONTRACTS do AI: `EngagementScope`, estados, liberação, arbitragem, handles.

## Ajustes da revisão
Separados pedido, concessão e escopo de retenção; ações avulsas do v1 removidas; handles com dono + vaga + geração;
testes de seis agentes com dois tokens em sequências completas (aborts, interrupções, troca de alvo).
