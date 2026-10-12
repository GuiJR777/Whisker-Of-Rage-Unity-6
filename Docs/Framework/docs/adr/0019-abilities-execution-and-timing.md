# ADR-0019 — Execução de habilidades: passo fixo, duas sub-bandas, caixa de entrada e hitstop

- **Status:** Proposta (design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md), [ADR-0013](0013-combat-timing.md),
  [ADR-0014](0014-two-phase-hit-resolution.md), [especificação do M5](../design/m5-abilities-design.md) §3.3, §13

## Contexto
Habilidades aplicam efeitos a outros atores, criam projéteis e interagem com o Combat, que já é determinístico por
passo fixo e independente da ordem entre atores. Se um ator aplicasse efeitos diretamente em outro durante o próprio
tick, o resultado (stacks, tags, ativações bloqueadas) dependeria da ordem de `FixedUpdate` entre instâncias, que a
Unity não garante (ADR-0003).

## Decisão (proposta)
1. O `AbilitySystemComponent` roda no `FixedUpdate`, banda `ABILITIES` (−700), em duas sub-bandas:
   **A1 (+0)** — pedidos, validação, commit, avanço das execuções e ações do passo (efeitos para outros atores vão para
   a caixa de entrada deles; projéteis e áreas são criados); **A2 (+50)** — cada ator aplica a própria caixa em ordem
   total (instigador, sequência), avança efeitos periódicos/expiração e entrega eventos FIFO.
2. Pedidos gerados em A2 (efeitos condicionais, listeners) entram na caixa do passo seguinte.
3. Tempo em segundos de jogo acumulando Δt fixo; ações em τ e janelas `[a, b)` pela regra de sobreposição do
   ADR-0013 (nenhuma pulada em Δt diferentes).
4. Hitstop: a linha do tempo das execuções pausa quando o ator tem `IAbilityTimeSource` em pausa (padrão por
   habilidade); cooldowns, recargas e durações/períodos de efeitos não pausam (padrão; configurável).
5. Como `ABILITIES` precede `COMBAT`, modificadores aplicados em A2 valem para o Combat do mesmo passo; efeitos pedidos
   pelo Combat (`IHitEffect`) são aplicados em A2 do passo seguinte.

## Alternativas consideradas
- **Aplicação imediata entre atores:** resultados dependentes de ordem (como o problema resolvido no ADR-0014).
- **Coordenador global (singleton) de habilidades:** acoplamento e estado estático; contrário ao padrão de componentes.
- **Relógio no `Update`:** quebra a equivalência entre Δt e a integração com Combat/Character.

## Consequências
- Positivas: determinismo e integração natural com Combat e Character; mesma regra temporal em todo o framework.
- Negativas / custos: um passo de atraso para efeitos entre atores; componente de fase oculto (como no Combat).
