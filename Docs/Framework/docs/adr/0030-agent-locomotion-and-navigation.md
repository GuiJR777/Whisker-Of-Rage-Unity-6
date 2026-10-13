# ADR-0030 — Contrato de locomoção do agente e navegação sem NavMesh

- **Status:** Proposta (design v1 do M6, 2026-10-13) · **Milestone:** M6
- **Relacionada:** [ADR-0004](0004-command-buffers.md), [ADR-0011](0011-character-command-buffer.md), [especificação do M6](../design/m6-ai-design.md) §11

## Contexto
Ações de movimento (perseguir, patrulhar, manter distância) são o núcleo de qualquer IA, mas o AI não pode mover
nada diretamente e o Character é dependência opcional. Navegação por NavMesh exigiria um novo package no grafo.

## Decisão
1. Extension point `IAgentLocomotion` no domínio do AI: leitura (pose, velocidade, facing plano, chão, `CanMove`,
   `MaxSpeed`) e escrita de **comandos** (`SetMove` planar, `Stop`, facing, pedidos de pulo/dash).
2. As ações de locomoção vivem no domínio e usam só o contrato; o Character o implementa por
   `CharacterAgentLocomotion`, que escreve no `CharacterCommandBuffer`.
3. `SteeringModel` puro (seek/arrive, separação pelo snapshot, desvio por sonda `NonAlloc` usando a menor distância,
   travamento → `Failure`); `INavigationProvider` com padrão `DirectNavigation`.
4. NavMesh (`com.unity.ai.navigation`) fica fora do M6; entra só com ADR que o adicione como opcional no grafo.

## Alternativas consideradas
- **Ações só nas integrações:** cada backend de movimento duplicaria Patrol/Chase/KeepDistance.
- **NavMesh agora:** muda o grafo e a instalação isolada; arenas de beat'em up não precisam.

## Consequências
- Positivas: ações reutilizáveis com qualquer motor; domínio testável com fixture.
- Negativas / custos: geometria complexa pode prender agentes sem NavMesh (R38).
- CONTRACTS do AI: `IAgentLocomotion`, `INavigationProvider`, ações.
