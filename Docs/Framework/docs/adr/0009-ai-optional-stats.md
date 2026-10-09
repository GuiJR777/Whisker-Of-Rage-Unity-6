# ADR-0009 — AI → Stats como dependência opcional

- **Status:** Aceita (M1, decisão D6 do design de Stats)

## Contexto
Decisões de IA típicas dependem de atributos e recursos ("fugir com vida abaixo de 30%", "não usar ability sem
energia"). No grafo do M0, `ai` só tinha integrações opcionais com `character`, `combat` e `abilities`; ler stats
exigiria passar por Abilities, criando acoplamento indireto e frágil.

## Decisão
Adicionar `com.ramirestechgames.stats` às dependências **opcionais** de `com.ramirestechgames.ai`.
O código fica em `RamiresTechGames.AI.Integration.Stats` (condições/sensores sobre `StatCollection`/`ResourcePool`),
guardado por `defineConstraints`/`versionDefines` (`RAMIRESTECHGAMES_STATS`).

## Consequências
- Camadas continuam válidas (AI L5 → Stats L1); nenhum ciclo.
- `dependency-graph.json` e [01-dependency-graph.md](../01-dependency-graph.md) atualizados.
- Implementação somente no M6.
