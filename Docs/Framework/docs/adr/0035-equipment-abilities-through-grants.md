# ADR-0035 — Efeitos do Equipment sobre o Abilities somente por concessões e tags soltas

- **Status:** Proposta — decisão aprovada na auditoria do v1; texto revisado na v2 do design do M7 (2026-10-14) ·
  **Milestone:** M7
- **Relacionada:** [ADR-0002](0002-optional-integration-assemblies.md), [ADR-0020](0020-gameplay-effects-over-stats.md),
  [ADR-0025](0025-owned-tags-in-abilities.md), [ADR-0033](0033-equipment-contribution-ledger.md),
  [ADR-0034](0034-equipment-transactions-and-timing.md), [especificação do M7](../design/m7-equipment-design.md) §8.7,
  §12, §19.2

## Contexto
Equipamentos concedem habilidades ativas, passivas ("enquanto equipado"), imunidades e tags. A API pública do
Abilities 0.1.0 oferece `GrantAbility(..., source)` → `AbilityHandle` e `RevokeAbility(handle)` (passivas entram na A2
seguinte e saem na revogação), e `AddLooseTag` → `LooseTagHandle` / `RemoveLooseTag(handle)`. `ApplyEffect(effect,
level)` retorna só `bool` (caixa de entrada, sem handle), e as remoções restantes são por tags, que atingem outras
fontes. O Abilities enfileira seus eventos e os despacha na A2. O grafo dá ao Equipment só `abilities` como dependência
opcional.

## Decisão
1. Integração `RamiresTechGames.Equipment.Integration.Abilities`: `GrantAbilityEffect`, `GrantPassiveAbilityEffect`,
   `GrantOwnedTagsEffect` (desfaz as tags já adicionadas pelo próprio efeito antes de recusar) e `OwnedTagsRequirement`.
2. **"Efeito enquanto equipado" = passiva concedida.** Sem efeito que chame `ApplyEffect` diretamente.
3. Concessões com o item como `source`, revertidas por handle; tags soltas por handle.
4. Concessões são contribuições **provisórias** até o ponto sem rejeição da transação (ADR-0034): a revogação de uma
   concessão provisória numa transação desfeita deixa no Abilities apenas o registro do descarte da passiva pendente
   (`EffectBlocked` com `SourceEnded`) — resíduo documentado, não desfeito.
5. Revogar no meio de uma execução segue o Abilities (`Revoked`, sem reembolso); isso só acontece em transação
   consolidada, nunca numa recusada.
6. **Character e Combat por Stats**; golpes de arma = habilidades concedidas com `PerformAttackAction`. Troca dinâmica de
   moveset/combo fora do M7. Nenhuma aresta nova no grafo.

## Alternativas consideradas
- **`ApplyEffect` + remoção por tags exclusivas:** depende de disciplina de autoria e falha com tags de outras fontes.
- **Mudar o Abilities para devolver handle em `ApplyEffect` ou consultar vagas livres:** correto a longo prazo, mas
  altera package existente (fora do M7; §19.2).
- **Integração direta com Combat para moveset:** exige API nova no Combat e aresta nova no grafo.

## Consequências
- Positivas: reversão exata com a API atual; nenhum package existente alterado.
- Negativas / custos: efeitos simples exigem asset de habilidade passiva; capacidade de tags soltas fixa em 16 (R46);
  resíduo de passiva provisória (R52).
- CONTRACTS do Equipment: tabela de efeitos da integração e a regra "sem `ApplyEffect` direto".
