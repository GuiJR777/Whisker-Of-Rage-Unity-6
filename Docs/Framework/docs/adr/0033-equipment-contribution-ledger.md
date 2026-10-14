# ADR-0033 — Livro de contribuições e reversibilidade exata do Equipment

- **Status:** Proposta (design v1 do M7, 2026-10-14) · **Milestone:** M7
- **Relacionada:** [ADR-0020](0020-gameplay-effects-over-stats.md), [ADR-0025](0025-owned-tags-in-abilities.md),
  [ADR-0034](0034-equipment-transactions-and-timing.md), [ADR-0035](0035-equipment-abilities-through-grants.md),
  [especificação do M7](../design/m7-equipment-design.md) §3.3, §8, §9

## Contexto
O critério do roadmap para o M7 é "equipar/desequipar restaura stats exatamente; grants revogados ao desequipar".
Um ator recebe contribuições de várias fontes ao mesmo tempo: itens, efeitos do Abilities, scripts do jogo. O Stats
garante que remover todos os modificadores de uma fonte iguala a coleção que nunca os recebeu, e o Abilities conta
tags e concessões por fonte; mas nenhum dos dois sabe o que um **item** aplicou em **outros** sistemas, nem lida com
falha no meio de uma aplicação, com `StatsComponent.Rebuild` ou com o componente desabilitado.

## Decisão
1. Cada `EquipmentItem` (instância) é a **fonte** das próprias contribuições: `IModifierSource` no Stats e `source` das
   concessões no Abilities, comparadas por referência. Dois itens da mesma definição são fontes distintas.
2. Toda contribuição aplicada gera um **recibo** com o handle que a reverte (`ModifierHandle`, `AbilityHandle`,
   `LooseTagHandle` ou `EquipmentEffectToken` de um efeito customizado), gravado **antes** da próxima aplicação, num
   **livro por item** (e por limiar de conjunto) de capacidade fixa.
3. Desequipar ou desabilitar reverte o livro do item **por handle**, em ordem inversa à de aplicação; o Equipment nunca
   remove por fonte alheia, por tag ou por "tudo" (`RemoveModifiersFromSource` só com a própria instância, como rede de
   segurança no `OnDestroy`).
4. Invariantes normativas I1–I5 (§8.1 da especificação): isolamento bit a bit dos stats, nenhuma ação sobre outras
   fontes, livro exato, atomicidade e ressincronização no `Rebuild` (reaplicar os modificadores dos itens habilitados na
   coleção nova e trocar os handles dos recibos).
5. Efeitos customizados (`IEquipmentEffect`) seguem o mesmo contrato: `Apply` tudo-ou-nada com token, `Revert` exato
   pelo token, sem alocação e sem abrir transação.
6. `Current` de recursos não é restaurado: segue a `ResourceMaxChangePolicy` do recurso (contrato explícito).

## Alternativas consideradas
- **Remover por fonte (`RemoveModifiersFromSource`/`RevokeAbilitiesFromSource`) sem livro:** funciona para Stats e
  concessões, mas não para tags soltas, efeitos customizados, falha parcial ou `Rebuild`; e convida a usar fontes
  compartilhadas (uma fonte "Equipment" do ator), que quebrariam o isolamento entre itens.
- **Recalcular tudo do zero a cada mudança (limpar e reaplicar todos os itens):** reconcede habilidades (perde
  cooldowns, encerra execuções) e gera eventos espúrios em outros packages.
- **Snapshot/restore do estado do ator:** desfaria contribuições de outras fontes ocorridas no meio.

## Consequências
- Positivas: reversão exata e auditável; ferramentas mostram cada recibo; mesmo mecanismo para Stats, Abilities,
  conjuntos e efeitos do jogo.
- Negativas / custos: capacidade fixa por item (padrão 32, validada); efeitos do jogo precisam respeitar o contrato
  `Apply`/`Revert` (R51); `Current` de recursos fica a cargo da política de máximo (R45).
- CONTRACTS do Equipment: invariantes I1–I5 como garantias com teste por modelo de referência.
