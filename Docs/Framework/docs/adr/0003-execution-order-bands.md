# ADR-0003 — Ordem de execução por bandas definidas no Core

- **Status:** Aceita (M0)

## Contexto
Bugs de ordem (input lido depois da decisão, motor antes do knockback) são difíceis de reproduzir.
A recomendação de Jonas Tyroller é ter ordem de execução explícita e previsível.

## Decisão
`RamiresTechGames.Core.ExecutionOrder` define bandas usadas em `[DefaultExecutionOrder]`:

| Banda | Valor | Quem |
|---|---|---|
| `COMMAND_SOURCES` | -1000 | Fontes de comando do jogador (adapters de input) |
| `DECISION` | -900 | Behaviour Trees |
| `STATE_MACHINE` | -800 | HFSM |
| `ABILITIES` | -700 | Ability System |
| `COMBAT` | -600 | Combat (combo runner, hitboxes) |
| `CHARACTER` | -500 | Motor de personagem |
| `STATS` | -400 | Regeneração e durações de modificadores |
| `DEFAULT` | 0 | Código do jogo sem banda |
| `PRESENTATION` | 1000 | Animator, VFX, áudio, UI |
| `DEBUG` | 2000 | Debuggers |

Os intervalos entre bandas (100) permitem ordenar componentes dentro de uma banda (`BANDA + n`).

## Alternativas
- **Manager central que tica todos os sistemas:** máxima previsibilidade, mas acopla packages a um runtime comum
  e dificulta uso isolado. Pode ser reavaliado no M8 se a ordem por bandas não for suficiente.
- **Script Execution Order nas Project Settings:** depende de cada projeto configurar.

## Consequências
- Ordem garantida entre packages sem dependência entre eles.
- `FixedUpdate` e `Update` seguem a mesma ordem relativa.
