# ADR-0020 — Gameplay Effects sobre o Stats: duração e stacking no Abilities, modificadores com fonte no Stats

- **Status:** Aceita (revisão do design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** especificação do M1 (Stats), [ADR-0019](0019-abilities-execution-and-timing.md),
  [especificação do M5](../design/m5-abilities-design.md) §7

## Contexto
O Stats já faz a matemática dos modificadores (`Flat`/`Additive`/`Multiplicative`, arredondamento, limites, derivadas),
identifica modificadores por fonte (`IModifierSource`, remoção exata por `RemoveModifiersFromSource`) e oferece
modificadores temporizados (`Duration`) que expiram no `Tick` do `StatsComponent`, chamado no `Update` com Δt
variável. Efeitos de habilidade precisam de stacking, períodos, inibição, pausa por hitstop e determinismo no passo
fixo — nada disso existe no Stats, e não deveria (Stats é L1, sem conceito de efeito).

## Decisão
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

## Ajustes da revisão
Aceita com o ajuste 5 da revisão (§25.5 da especificação), sem mudança no Stats:

1. O ASC registra, por efeito ativo, a coleção onde estão seus modificadores e acompanha `StatsComponent.Rebuilt`:
   ao receber o evento, reaplica os modificadores dos efeitos **ativos e não inibidos** na nova coleção (mesma fonte,
   magnitude pelos stacks atuais), sem duplicar e sem alterar stacks, tempos, períodos ou tags.
2. Inscrição em `OnEnable`, remoção em `OnDisable`/`OnDestroy`; ao reabilitar, ressincroniza se a coleção mudou.
3. Testes: efeitos ativos e inibidos atravessando `Rebuild`, remoção posterior igual a coleção virgem, `Rebuild` com o
   ASC desabilitado.
4. Efeitos condicionais carregam uma **cadeia causal** de tamanho fixo no `EffectSpec` (§25.6, ADR-0025 não muda):
   recorrência de definição sem `AllowRecurrence` ou cadeia acima de `MaxConditionalChain` (8) é bloqueada com
   diagnóstico; o validator acusa ciclos entre definições (A → B → A).
