# ADR-0037 — Instâncias de item, identidade e estado de loadout

- **Status:** Proposta (design v1 do M7, 2026-10-14) · **Milestone:** M7
- **Relacionada:** [ADR-0033](0033-equipment-contribution-ledger.md), [ADR-0036](0036-equipment-slots-occupancy-and-storage.md),
  [especificação do M7](../design/m7-equipment-design.md) §5, §11

## Contexto
Roguelikes e RPGs têm várias cópias do mesmo item com níveis e afixos diferentes, e precisam salvar o que está
equipado. O padrão do framework é definição imutável (SO) + instância de runtime (classe pura). Cada instância precisa
ser uma fonte distinta (ADR-0033) e ter identidade estável para save e ferramentas.

## Decisão
1. `EquipmentItem` = definição + `ItemId` (`ulong` ≠ 0) + `Level` + `InstanceModifiers` (somente leitura após criar) +
   local (`None`, `Slot`, `Storage`). Um item está em no máximo um lugar.
2. **O jogo fornece ids e modificadores de instância**; o package não sorteia nem gera itens. Itens criados por
   loadouts-asset recebem ids de runtime determinísticos do componente.
3. Id duplicado no mesmo componente ou armazenamento é recusado (`DuplicateItemId`); unicidade global é do jogo (R49).
4. `EquippedItemHandle` = (dono, índice do slot principal, geração); handles inválidos, antigos ou estrangeiros não agem.
5. `CaptureLoadout()` exporta `EquipmentLoadoutState` (por slot: `ItemId`, `StableId` da definição, nível,
   modificadores de instância, habilitado); `TryApplyLoadout` aceita esse estado ou um asset. **Persistência fica no
   jogo.**

## Alternativas consideradas
- **Só definições (sem instâncias):** impede níveis/afixos por cópia e faria duas cópias compartilharem a mesma fonte.
- **Ids gerados pelo package (GUID por instância):** alocação e acoplamento com o formato de save do jogo.
- **Serializador de save no package:** cada jogo tem seu formato e sua política de versão.

## Consequências
- Positivas: cópias independentes, save simples, ferramentas com identidade estável.
- Negativas / custos: o jogo administra ids; `StableId` nas definições do Equipment (mesmo mecanismo do Stats).
- CONTRACTS do Equipment: formato de `EquipmentLoadoutState` como contrato versionado.
