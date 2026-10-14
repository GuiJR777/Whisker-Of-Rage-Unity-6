# ADR-0033 — Livro de contribuições e reversibilidade exata do Equipment

- **Status:** Aceita (auditoria da v2 do design do M7, 2026-10-14; proposta no v1, revisada na v2) · **Milestone:** M7
- **Relacionada:** [ADR-0020](0020-gameplay-effects-over-stats.md), [ADR-0025](0025-owned-tags-in-abilities.md),
  [ADR-0034](0034-equipment-transactions-and-timing.md), [ADR-0035](0035-equipment-abilities-through-grants.md),
  [especificação do M7](../design/m7-equipment-design.md) §3.3, §8, §9

## Contexto
O critério do roadmap para o M7 é "equipar/desequipar restaura stats exatamente; grants revogados ao desequipar". Um
ator recebe contribuições de várias fontes: itens, efeitos do Abilities, scripts do jogo. O Stats 0.1.1 garante que
remover todos os modificadores de uma fonte iguala a coleção que nunca os recebeu, com soma canônica (operação,
prioridade, valor); o Abilities conta tags e concessões por fonte. Nenhum dos dois sabe o que um **item** aplicou em
**outros** sistemas, nem lida com falha no meio de uma aplicação, com `StatsComponent.Rebuild` ou com o componente
desabilitado.

## Decisão
1. Cada `EquipmentItem` é a **fonte** das próprias contribuições (Stats e Abilities), por referência; um item fica em
   no máximo um lugar, então não há fonte compartilhada entre atores.
2. Toda contribuição aplicada gera **um recibo** (handle do Stats, `AbilityHandle`, `LooseTagHandle` ou
   `EquipmentEffectToken`), gravado antes da próxima aplicação, num **livro de capacidade fixa** por item e por limiar
   de conjunto. Um efeito = um recibo.
3. Reverter = desfazer os recibos **por handle**, em ordem inversa. O Equipment **nunca** remove por fonte, por tag ou
   "tudo"; um handle que o package dono já não reconhece faz o recibo ser descartado e contado, sem remoção
   compensatória.
4. **Invariantes normativas** (§8.1): I1 isolamento bit a bit com coleção real; I2 nenhuma ação sobre outras fontes;
   I3 livro exato; I4 atomicidade sem reconcessão (com ADR-0034); I5 `Rebuild`.
5. **`Rebuild`**: no `Rebuilt`, reaplica na coleção nova os modificadores de itens habilitados e limiares ativos e
   **substitui os handles** nos recibos; stat ausente → recibo `Dormant` (tentado de novo no próximo `Rebuild`; reverter
   é no-op). O Abilities reaplica sozinho os modificadores dos seus efeitos.
6. **Contrato estrito de `IEquipmentEffect`**: só contribuições reversíveis (sem som, VFX, UI, notificações ou disco);
   `Apply` tudo-ou-nada (retornar `false` com resíduo é violação, detectável pelo kit de contrato dos testes); exceção
   em `Apply` = recusa, registrada; `Revert` infalível (exceção registrada, recibo descartado); sem abrir transações do
   Equipment nem lotes/`Rebuild` do Stats; roda dentro do lote da transação; chamadas ao Abilities seguem as regras de
   tempo dele; sem alocação em regime.
7. `Current` de recursos segue a `ResourceMaxChangePolicy` (aprovado).
8. Em Development, depois de reverter um item, `GetAllModifiers` é conferido: modificadores ainda com o item como fonte
   indicam uso indevido pelo jogo (aviso e contador; nada é removido).

## Alternativas consideradas
- **Remover por fonte sem livro:** não cobre tags soltas, efeitos customizados, falha parcial nem `Rebuild`, e convida a
  fontes compartilhadas.
- **Recalcular tudo do zero a cada mudança:** reconcede habilidades e gera eventos espúrios.
- **Snapshot/restore do ator:** desfaria contribuições de outras fontes.

## Consequências
- Positivas: reversão exata e auditável; mesmo mecanismo para Stats, Abilities, conjuntos e efeitos do jogo.
- Negativas / custos: capacidade fixa por livro (validada); efeitos do jogo precisam respeitar o contrato (R51);
  recursos pela política de máximo (R45).
- CONTRACTS do Equipment: invariantes I1–I5 e o contrato de efeitos como garantias com teste.

## Revisão v2 (auditoria do v1)
Detalhados: contrato estrito de efeitos (item 6), substituição de handles e recibos dormentes no `Rebuild` (item 5),
handles obsoletos e fontes compartilhadas (itens 3 e 8); retirada a "rede de segurança" por fonte no `OnDestroy` do v1,
que contrariava I2.
