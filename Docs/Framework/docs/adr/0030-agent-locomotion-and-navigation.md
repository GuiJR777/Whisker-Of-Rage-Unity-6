# ADR-0030 — Contrato de locomoção do agente e navegação sem NavMesh

- **Status:** Aceita (auditoria do design do M6, 2026-10-13; proposta no v1) · **Milestone:** M6
- **Relacionada:** [ADR-0004](0004-command-buffers.md), [ADR-0011](0011-character-command-buffer.md), [ADR-0032](0032-ai-command-output-and-tracking.md), [especificação do M6](../design/m6-ai-design.md) §11

## Contexto
Ações de movimento são o núcleo de qualquer IA, mas o AI não pode mover nada diretamente e o Character é
dependência opcional. NavMesh exigiria um novo package no grafo.

## Decisão
1. `IAgentLocomotion` no domínio do AI: leitura (pose, velocidade, facing plano, chão, `CanMove`, `MaxSpeed`) e
   escrita de comandos (`SetMove` no plano XZ, `Stop`, facing, pulo/dash rastreados como no ADR-0032).
2. Ações de locomoção no domínio, só contra o contrato; o Character o implementa por `CharacterAgentLocomotion`
   (`CharacterCommandBuffer`).
3. `SteeringModel` puro (seek/arrive, separação pelo snapshot, desvio por sonda `NonAlloc` com a menor distância,
   travamento → `Failure`); `INavigationProvider` com `DirectNavigation`.
4. NavMesh fora do M6; só com ADR que o adicione como opcional no grafo.

## Alternativas consideradas
- **Ações só nas integrações:** cada backend duplicaria Patrol/Chase/KeepDistance.
- **NavMesh agora:** muda o grafo e a instalação isolada.

## Consequências
- Positivas: ações reutilizáveis; domínio testável com fixture.
- Negativas / custos: geometria complexa pode prender agentes (R38).
- CONTRACTS do AI: `IAgentLocomotion`, `INavigationProvider`, ações.

## Ajustes da revisão
Aceita sem mudança de decisão; plano XZ e NavMesh fora confirmados; pulo/dash seguem o rastreamento do ADR-0032.
