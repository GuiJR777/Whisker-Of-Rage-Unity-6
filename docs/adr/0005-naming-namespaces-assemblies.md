# ADR-0005 — Nomes de packages, namespaces e assemblies

- **Status:** Aceita (M0)

## Contexto
Regra RamiresTech: `namespace RamiresTechGames.[SISTEMA].[CAMINHO]`. Master Prompt define ids de package
(`com.ramirestechgames.hfsm`, `.ai`). Regra Unity6: sem abreviações.

## Decisão
| Package id | Assembly / namespace raiz |
|---|---|
| `com.ramirestechgames.core` | `RamiresTechGames.Core` |
| `com.ramirestechgames.stats` | `RamiresTechGames.Stats` |
| `com.ramirestechgames.hfsm` | `RamiresTechGames.HierarchicalStateMachine` |
| `com.ramirestechgames.character` | `RamiresTechGames.Character` |
| `com.ramirestechgames.combat` | `RamiresTechGames.Combat` |
| `com.ramirestechgames.abilities` | `RamiresTechGames.Abilities` |
| `com.ramirestechgames.ai` | `RamiresTechGames.AI` |
| `com.ramirestechgames.equipment` | `RamiresTechGames.Equipment` |

- Sub-assemblies: `.Editor`, `.Tests.Editor`, `.Tests.Runtime`, `.Integration.<Outro>`, `.Presentation`.
- Namespace interno = raiz + caminho de pastas a partir de `Runtime/` ou `Editor/`.
- Símbolo de define: `RAMIRESTECHGAMES_<ID>` (ex.: `RAMIRESTECHGAMES_HFSM`).
- `AI` mantém a sigla (precedente `UnityEngine.AI`); `HFSM` é expandido.

## Consequências
- Nomes de package curtos e namespaces legíveis.
- `dependency-graph.json` mapeia id ↔ assembly para o verificador.
