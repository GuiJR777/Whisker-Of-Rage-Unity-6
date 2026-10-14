# M7 — Evidências de implementação do Equipment

- **Especificação:** [m7-equipment-design](m7-equipment-design.md) v2 (aprovada em 2026-10-14, WOR `02ded26` + registro
  da aprovação); ADRs 0033–0037 (aceitas).
- **Unity:** 6000.6.5f1 · **Package:** `com.ramirestechgames.equipment` 0.1.0 (sem tag), repositório privado
  `GuiJR777/com.ramirestechgames.equipment`, submodule do host.
- **Packages existentes:** nenhum alterado (Core 0.4.0, Stats 0.1.1, HFSM 0.1.0, Character 0.2.0, Combat 0.1.0,
  Abilities 0.1.0, AI 0.1.0 nos commits fixados no WOR).

As entregas são registradas abaixo à medida que são concluídas.

## Entrega 1 — fundação (Equipment `bccaecb`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings (achado e corrigido: UAC0009 — `DEVELOPMENT_BUILD` obsoleto, trocado por `DEBUG`) |
| EditMode | 27/27 — `TransactionTests` (13 casos), `ReversibilityModelTests`, `EnqueueTests` (6), `AllocationTests`, `DefinitionValidationTests` (4), metadados (2) |
| PlayMode | 2/2 — `EquipmentComponent` com `StatsComponent` real (eventos, `Rebuild`, `enabled`, destruição) |
| Modelo de referência | 3 sementes × 600 operações (equipar, desequipar, trocar, mover, habilitar/desabilitar, trocas/equipamentos recusados, outras fontes, `Rebuild`) — **1803 verificações bit a bit** contra uma `StatCollection` real reconstruída; 0 violações; 0 usos indevidos de fonte |
| Atomicidade | troca recusada com falha no 1º, 2º e 3º efeito do item novo: stats bit a bit, recibos e handles do item antigo, slot e gerações intactos; efeitos provisórios revertidos; só `EquipFailed`; **nenhum `StatChanged`** |
| Violações | exceção em `Revert` e handle já removido depois do ponto sem rejeição → `Succeeded = true`, `IsClean = false`, `ContractViolations = 2`, `RecentViolations` com tipo, transação e item |
| Fila | reentrância recusada em qualquer controlador; FIFO; operação enfileirada no provisório de uma transação recusada → `Discarded`; recuperação enfileirada por `EquipFailed` executa; ciclo de falhas reenfileiradas para em 64 (`QueueOverflow`) |
| Zero GC | ciclos aquecidos de equipar, troca recusada, troca, mover, desabilitar/habilitar e desequipar com efeito (`EquipmentTokenStorage<T>`) |
| QG12 (Core + Stats) | 29 testes (27 + 2), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Desvios e precisões: (1) o contrato `IEquipmentEffect` foi antecipado da entrega 3 para esta, porque o kit de falhas
injetadas precisa de uma contribuição falível (os testes completos do contrato continuam na entrega 3);
(2) `EquipmentTokenStorage<T>` foi acrescentado: handles de outros packages têm construtores internos e não cabem em
inteiros do token; guardá-los num objeto (`Data`) alocaria a cada equipamento — o pool mantém zero GC em regime;
(3) motivo `InvalidModifier` acrescentado (valor não finito ou fator negativo no nível do item) e `InvalidHandle`
separado de `InvalidItem`; (4) efeitos que alteram stats precisam de `IStatsRebuildAware` para sobreviver a um
`Rebuild` (o modelo de referência usa um efeito assim); (5) `Deactivate` chamado durante uma transação (componente
desabilitado por um efeito) retorna `Reentrant` e só marca o controlador inativo. Nenhum package existente alterado.
