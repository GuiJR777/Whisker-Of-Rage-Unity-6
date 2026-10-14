# ADR-0037 — Instâncias de item, localização, identidade e estado de loadout

- **Status:** Proposta — revisada na v2 do design do M7 (2026-10-14), após a auditoria do v1 · **Milestone:** M7
- **Relacionada:** [ADR-0033](0033-equipment-contribution-ledger.md), [ADR-0034](0034-equipment-transactions-and-timing.md),
  [ADR-0036](0036-equipment-slots-occupancy-and-storage.md), [especificação do M7](../design/m7-equipment-design.md) §5,
  §11

## Contexto
Jogos têm várias cópias do mesmo item com níveis e modificadores diferentes e precisam salvar o que está equipado. O
padrão do framework é definição imutável (SO) + instância de runtime. Cada instância precisa ser uma fonte distinta
(ADR-0033), ter identidade estável e estar num lugar conhecido, para que `CaptureLoadout` → `TryApplyLoadout` encontre
os itens certos sem duplicá-los nem recriá-los.

## Decisão
1. `EquipmentItem` = definição + `ItemId` (`ulong` ≠ 0, fornecido pelo jogo) + `Level` + `InstanceModifiers`
   (somente leitura) + `Location` (`Detached` | `Slot(componente, índice)` | `Storage(armazenamento)`).
2. **Localização única**, alterada só pelo commit de uma transação; item em slot de outro componente →
   `ItemNotAvailable`; item em armazenamento só participa se o armazenamento for uma fonte **autorizada** da chamada.
3. **Identidade imutável**: o package nunca recria uma instância existente; itens de loadouts-asset recebem ids de
   runtime determinísticos do componente.
4. `EquippedItemHandle` = (dono, índice do slot principal, geração).
5. **Estado**: `CaptureLoadout()` exporta, por slot, `ItemId`, `StableId` da definição, nível, modificadores de
   instância e habilitado. `TryApplyLoadout(state)` **nunca cria itens** e resolve cada id em (1) itens equipados neste
   componente e (2) armazenamentos autorizados, na ordem dada; ausente → `ItemNotFound`; em outro ator ou armazenamento
   não autorizado → `ItemNotAvailable`; id repetido no estado → `DuplicateItemId`; definição divergente →
   `DefinitionMismatch`; nível e modificadores do **item** prevalecem.
6. **Políticas**: `ReplaceAll` (estado final exato; itens no mesmo slot permanecem sem reaplicar; itens do mesmo
   componente em outro slot são movidos; os demais saem para o destino) e `FillEmpty` (só slots vazios e não
   bloqueados; entradas ignoradas contadas em `SkippedEntries`). Captura → aplicação no mesmo ator é no-op.
7. Persistência fica no jogo (recria instâncias, coloca num armazenamento, aplica o estado).

## Alternativas consideradas
- **Só definições:** impede níveis por cópia e faz cópias compartilharem a fonte.
- **Recriar instâncias ao aplicar um estado:** perderia identidade e reconcederia contribuições de itens que já estavam
  equipados.
- **Serializador de save no package:** cada jogo tem formato e versão próprios.

## Consequências
- Positivas: cópias independentes, save simples, loadouts sem duplicação nem reaplicação desnecessária.
- Negativas / custos: o jogo administra ids (R49) e autoriza armazenamentos; `StableId` nas definições do Equipment.
- CONTRACTS do Equipment: `EquipmentLoadoutState` como contrato versionado e regras de resolução.

## Revisão v2 (auditoria do v1)
Acrescentados: localização normativa (item 2), resolução de ids sem duplicação e sem recriação (item 5), políticas
`ReplaceAll`/`FillEmpty` com permanência e movimento (item 6).
