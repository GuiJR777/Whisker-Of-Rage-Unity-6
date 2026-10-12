# ADR-0020 — Gameplay Effects sobre o Stats: duração e stacking no Abilities, modificadores com fonte no Stats

- **Status:** Proposta (design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** especificação do M1 (Stats), [ADR-0019](0019-abilities-execution-and-timing.md),
  [especificação do M5](../design/m5-abilities-design.md) §7

## Contexto
O Stats já faz a matemática dos modificadores (`Flat`/`Additive`/`Multiplicative`, arredondamento, limites, derivadas),
identifica modificadores por fonte (`IModifierSource`, remoção exata por `RemoveModifiersFromSource`) e oferece
modificadores temporizados (`Duration`) que expiram no `Tick` do `StatsComponent`, chamado no `Update` com Δt
variável. Efeitos de habilidade precisam de stacking, períodos, inibição, pausa por hitstop e determinismo no passo
fixo — nada disso existe no Stats, e não deveria (Stats é L1, sem conceito de efeito).

## Decisão (proposta)
1. Cada `ActiveEffect` é um `IModifierSource`. Seus modificadores entram no Stats **sem** `Duration` (infinitos) e
   saem por `RemoveModifiersFromSource` quando o efeito termina, é inibido ou muda de stacks (reaplicação com a
   magnitude nova).
2. Duração, período, stacking, inibição, imunidade e dispel são do Abilities, no passo fixo (ADR-0019).
3. Mudanças instantâneas usam a API de recursos/base do Stats (`Increase`, `Decrease`, `TrySetBaseValue`,
   `TryAddToBaseValue`); magnitudes leem `IStatValueSource`.
4. O Abilities não reimplementa matemática de atributos nem guarda cópias de valores de stats.
5. Nenhuma mudança no Stats é necessária.

## Alternativas consideradas
- **Usar `Duration` do Stats:** expiração no `Update`, sem stacks, sem hitstop, sem eventos de efeito.
- **Abilities com atributos próprios:** duplicação do Stats e duas fontes de verdade.
- **Mover "efeitos" para o Stats:** Stats ganharia conceitos de L4 (tags, stacking, instigador).

## Consequências
- Positivas: zero duplicação de matemática; remoção exata garantida pelo Stats (garantia 2); equipamentos (M7) usam
  o mesmo mecanismo de fonte.
- Negativas / custos: mudanças de stacks reaplicam modificadores (custo por mudança, sem GC); o Debugger precisa
  correlacionar fontes com efeitos.
