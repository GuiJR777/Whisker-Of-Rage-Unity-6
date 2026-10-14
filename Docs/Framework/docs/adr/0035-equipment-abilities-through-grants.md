# ADR-0035 — Efeitos do Equipment sobre o Abilities somente por concessões e tags soltas

- **Status:** Proposta (design v1 do M7, 2026-10-14) · **Milestone:** M7
- **Relacionada:** [ADR-0002](0002-optional-integration-assemblies.md), [ADR-0020](0020-gameplay-effects-over-stats.md),
  [ADR-0025](0025-owned-tags-in-abilities.md), [ADR-0033](0033-equipment-contribution-ledger.md),
  [especificação do M7](../design/m7-equipment-design.md) §8.5, §12, §19.2

## Contexto
Equipamentos concedem habilidades ativas, passivas ("enquanto equipado"), imunidades e tags. A API pública do
Abilities 0.1.0 oferece `GrantAbility(..., source)` → `AbilityHandle` e `RevokeAbility(handle)` (passivas entram na A2
seguinte e saem na revogação) e `AddLooseTag` → `LooseTagHandle` / `RemoveLooseTag(handle)`. Já
`ApplyEffect(effect, level)` retorna só `bool` (a aplicação vai para a caixa de entrada, sem handle), e as remoções
públicas restantes são por tags, que atingem efeitos de outras fontes. O grafo dá ao Equipment só `abilities` como
dependência opcional; Character e Combat não estão no grafo do Equipment.

## Decisão
1. A integração `RamiresTechGames.Equipment.Integration.Abilities` (asmdef própria, define `RAMIRESTECHGAMES_ABILITIES`)
   oferece `GrantAbilityEffect`, `GrantPassiveAbilityEffect`, `GrantOwnedTagsEffect` e `OwnedTagsRequirement`.
2. **"Efeito enquanto equipado" = passiva concedida.** Não há efeito que chame `ApplyEffect` diretamente: sem handle,
   não seria reversível com exatidão (ADR-0033).
3. Toda concessão usa o item como `source` e é revertida por handle; tags soltas, por handle.
4. Revogar no meio de uma execução segue o Abilities (`Revoked`, sem reembolso); efeitos que a habilidade já aplicou
   com duração própria não são contribuições do item e continuam (documentado, R44).
5. **Character e Combat por Stats**: movimento (`StatMovementModifierSource`), ataque, defesa, poise e guarda vêm de
   stats; golpes de arma são habilidades concedidas com `PerformAttackAction` (integração Abilities ↔ Combat
   existente). Nenhuma aresta nova no grafo no M7.

## Alternativas consideradas
- **`ApplyEffect` + remoção por tags exclusivas do item:** depende de disciplina de autoria (tags únicas por item) e
  ainda falha com efeitos que outro sistema aplica com a mesma tag.
- **Mudar o Abilities para devolver handle em `ApplyEffect`:** correto a longo prazo, mas altera API de package
  existente (fora do M7); registrado como mudança futura (§19.2).
- **Integração direta com Combat para trocar moveset:** exige API nova no Combat e aresta nova no grafo; adiada (R47).

## Consequências
- Positivas: reversão exata com a API atual; nenhum package existente alterado; caso comum (só stats) sem Abilities.
- Negativas / custos: efeitos simples "enquanto equipado" exigem um asset de habilidade passiva; moveset do Combat não
  muda por equipamento no M7.
- CONTRACTS do Equipment: tabela de efeitos da integração e a regra "sem `ApplyEffect` direto".
