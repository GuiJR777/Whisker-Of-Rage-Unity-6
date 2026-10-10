# ADR-0017 — Pipeline de dano e precedência dos resultados

- **Status:** Proposta (design do M4) · **Milestone:** M4
- **Relacionada:** [ADR-0008](0008-serialize-reference-picker-in-core.md), contratos do Stats (`IStatValueSource`,
  `ResourcePool`), [especificação do M4](../design/m4-combat-design.md) §7

## Contexto
O roadmap exige 6 `HitOutcome` testados e um dano configurável que leia stats sem que o Combat conheça stats
específicas. Abilities (M5) precisa estender o dano e os efeitos por acerto.

## Decisão
1. Precedência por candidato: `Invulnerable` → `Dodged` → `Miss` (estado do alvo não atingível pelo golpe) →
   `Parried` → `Blocked` → `Hit`. Ataque sem contato é *whiff* (`AttackEnded.HadContact = false`).
2. Dano por `DamageProfileDefinition`: lista ordenada de `IDamageStep` (`[SerializeReference]` + seletor do Core)
   sobre um `DamageCalculation`, com atacante e alvo como `IStatValueSource` e `ICombatRandom` injetado.
3. Aplicação em `ResourcePool.Decrease` do recurso de vida do perfil, com o `HitInfo` como instigador; morte é decisão
   do jogo/HFSM.
4. `IHitEffect` roda depois do dano para cada acerto confirmado (extension point do Abilities).

## Alternativas consideradas
- **Fórmula fixa no Combat:** cada jogo teria de reescrever o dano.
- **Eventos para o jogo calcular:** perde ordem determinística e testes do package.

## Consequências
- Positivas: dano testável e extensível sem código no Combat; determinismo com RNG injetado.
- Negativas / custos: passos SR seguem as regras do R4 (`sealed`, `[MovedFrom]` ao mover).
