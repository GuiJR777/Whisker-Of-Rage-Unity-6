# ADR-0036 — Slots, ocupação, unicidade e armazenamento transacional

- **Status:** Aceita (auditoria da v2 do design do M7, 2026-10-14; proposta no v1, revisada na v2) · **Milestone:** M7
- **Relacionada:** [ADR-0018](0018-gameplay-tags-in-core.md), [ADR-0034](0034-equipment-transactions-and-timing.md),
  [ADR-0037](0037-equipment-item-instances-and-loadout-state.md), [especificação do M7](../design/m7-equipment-design.md)
  §4, §6, §10

## Contexto
Jogos modelam equipamento com slots fixos (arma, armadura), grupos de slots iguais (anéis, acessórios), itens que
ocupam mais de um slot (arma de duas mãos) e itens únicos. O M0 definiu que o Equipment "não faz inventário", mas
desequipar precisa de um destino, loadouts precisam buscar itens não equipados, e a transação (ADR-0034) só pode
remover o antigo quando nada mais puder falhar — inclusive o armazenamento.

## Decisão
1. **Slots por definição** (categorias aceitas/recusadas por `GameplayTag` hierárquica) e **layout** expandido com índice
   total. Slots repetidos são genéricos; nenhum mecanismo de um jogo específico é assumido.
2. **Ocupação múltipla** resolvida **no plano final**; slots a bloquear ocupados por itens que permanecem: `Fail`
   (padrão) ou `DisplaceToStorage` (os ocupantes saem na mesma transação, sob as mesmas regras).
3. **Unicidade** por `UniqueGroup`/`UniqueLimit` no estado final.
4. **Requisitos** SR avaliados uma vez, na validação, contra o estado antes da transação (D8).
5. **Armazenamento transacional** `IEquipmentStorage`: leitura (`TryFind`, `Contains`, `Count`, `Capacity`);
   validação sem efeitos (`CanRelease`, `CanAccept`, capacidade líquida do plano); commit **infalível por contrato**
   (`CommitRemove` antes de `CommitAdd` no mesmo armazenamento), sem eventos do armazenamento antes do commit da
   transação. Falha antes/na validação → `StorageRejected`/`StorageFull` sem efeitos; violação depois → item `Detached`
   e violação reportada (ADR-0034).
6. `EquipmentStash` é a implementação mínima. Inventário completo (grade, peso, empilhamento, economia, UI) fica fora.

## Alternativas consideradas
- **Slots como enum ou strings:** sem validação nem herança, com nomes de jogo no código.
- **Inventário completo no package:** regras próprias de cada jogo; conflita com o M0.
- **Armazenamento sem fases (`Store`/`Remove` que podem falhar):** a falha viria depois da remoção do item antigo e
  quebraria a atomicidade.

## Consequências
- Positivas: armas, armaduras e acessórios só com assets; inventários do jogo plugam pelo contrato; falhas de
  armazenamento detectadas antes do ponto sem rejeição.
- Negativas / custos: implementações do jogo precisam honrar o commit infalível (R53); "primeira instância livre" pode
  não servir a layouts exóticos (o jogo escolhe o slot).
- `02-package-boundaries-and-contracts.md`: seção do Equipment atualizada com os contratos aprovados.
