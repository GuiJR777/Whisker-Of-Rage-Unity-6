# ADR-0018 — `GameplayTag` no Core

- **Status:** Proposta (design do M4; depende da decisão D6) · **Milestone:** M4
- **Relacionada:** [ADR-0001](0001-package-per-domain.md), risco R6 (Core monólito), roadmap do M4 ("GameplayTags entram
  no Core (ADR) se necessárias"), [especificação do M4](../design/m4-combat-design.md) §21

## Contexto
O Combat marca golpes, ações e alvos (imbloqueável, tipo de dano, fraquezas). Abilities (M5) e AI (M6) vão filtrar e
reagir pelas mesmas marcas. Se o tipo nascer no Combat e migrar depois, os assets de tag (ScriptableObject) teriam o
script trocado de assembly — referências quebram (`[MovedFrom]` só cobre `[SerializeReference]`).

## Decisão (proposta)
1. Core 0.4.0 ganha `GameplayTag` (ScriptableObject com `StableId`, nome e pai opcional para hierarquia) e
   `GameplayTagSet` (contêiner serializável com consulta sem alocação: `Has`, `HasAny`, `HasAll`, herança pelo pai).
2. Nada além disso no Core (sem consultas por string, sem registro global).
3. Combat é o primeiro consumidor; Abilities e AI os seguintes (regra de ≥ 2 consumidores atendida no M5).

## Alternativas consideradas
- **Tags locais do Combat:** migração de assets no M5.
- **Strings:** sem validação, erros silenciosos de digitação.
- **Enum de flags:** fechado; o package não pode conhecer as categorias do jogo.

## Consequências
- Positivas: um vocabulário comum para os domínios de gameplay sem acoplar packages.
- Negativas / custos: o Core cresce (R6); mitigado pelo escopo mínimo acima.
