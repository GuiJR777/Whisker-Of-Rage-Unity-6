# M3 — Validação manual do Character 0.1.0 (samples, ferramentas de Editor)

- **Data:** 2026-10-10 · **Unity:** 6000.6.5f1 (URP) · **Samples:** montados por script, copiados para `Samples~`,
  importados pelo Package Manager (`Sample.Import`) em `Assets/Samples/...`, validados e removidos.
- **Como foi feito:** Editor do WOR controlado pelo Unity CLI. Capturas do Game view por `capture_game_view`; janelas
  de Editor pelo *back buffer* da própria janela (`GUIView.GrabPixels`). O Player do *Movement Playground* foi guiado
  por um `ScriptedCharacterSource` adicionado em Play Mode (o mesmo prefab com outra fonte), porque o teclado do sample
  lê eventos IMGUI que o CLI não injeta; no *Input System Player* a tecla foi injetada por
  `InputSystem.QueueStateEvent`. Não houve operador humano com teclado: verificação reproduzível, não teste de feel.

## Resultados

| # | Ação | Resultado verificado | Evidência |
|---|---|---|---|
| 1 | Importar os 3 samples, compilar, abrir cada cena e rodar o Validator | 0 erros, 0 warnings, 0 scripts ausentes nas 3 cenas e nos assets de `Assets/Samples` | — |
| 2 | *Movement Playground*: Player passa pela placa laranja | Canal externo −5,4 m/s logo após o contato (impulso −9; 2; 0), recuo e retomada da patrulha; evento `Impulse (−9,00; 2,00; 0,00)` no debugger | `03_knockback.png`, `04_debugger.png` |
| 3 | Player passa pela placa verde | Sai do chão apesar do snapping (13 m/s), ápice ~3,2 m, aterrissa: `Landed impact 15,6 m/s after 0,90 s` | `02_launch.png`, `04_debugger.png` |
| 4 | NPC (variante Model, script) | Patrulha, pula e dá dash periodicamente com o mesmo prefab base do Player (variante Sprite); só o filho visual e o presenter diferem | `02_launch.png` |
| 5 | Character Debugger com o Player | Gráficos de 2 s (velocidade plana, vertical, altura), valores ao vivo, últimos eventos (impulsos, aterrissagens) | `04_debugger.png` |
| 6 | Inspector do motor em Play Mode | Seção *Live*: canais, chão, timers, contadores, bordas, pedidos e atalho para o debugger | `05_motor_inspector.png` |
| 7 | Gizmos do personagem selecionado (pausado no chão) | Esferas do sensor e do snapping, vetor de locomoção, arco previsto do pulo segurado | `06_gizmos.png` |
| 8 | Inspector do profile (Edit Mode) | Valores derivados para Δt 0,02 (g_up 25, g_down 40, v0 10,253 m/s corrigido, ápice discreto 2,001 m, tempo de ar 0,716 s) e prévia: ápice 2,00 m a 0,400 s, alcance 4,32 m, mínimo 0,60 m, coyote 0,60 m | `07_profile_inspector.png` |
| 9 | *HFSM Locomotion*: NPC por script, Player parado | O NPC percorre Grounded → Jump → Airborne → Dash com `LastJumpRequest`/`LastDashRequest` = `Executed`; rótulo do estado sobre cada personagem | `08_hfsm_locomotion.png` |
| 10 | *Input System Player*: Espaço + D injetados | Fonte do buffer = `PlayerInputCharacterSource`, `Move` (1; 0) relativo à câmera, `JumpHeld`, personagem no ar | — |

## Problemas encontrados e corrigidos durante a validação
| Problema | Correção |
|---|---|
| Texto do painel do sample ilegível (estilo vazio, texto preto) | O `GUIStyle` privado voltava não-nulo e vazio do backup do Play Mode; campo marcado `[NonSerialized]` (sample) |
| "Last events" do debugger vazio depois de reabrir/redimensionar a janela | O debugger reassina os eventos do motor selecionado quando a assinatura se perde |
| `CreateAsset` recusa a extensão `.physicsMaterial` | Material físico salvo como `.asset` |
| GUIDs repetidos ao apagar e recriar materiais no mesmo script | Pastas do sample recriadas do zero antes de montar |
| `NewScene` descarregava o profile criado antes no mesmo script (motor sem profile na cena do Input System) | Assets recarregados do disco depois de `NewScene` |

## Observações
- Um erro registrado pelo próprio CLI (caminho de captura fora do projeto) disparou *Error Pause*; não envolve o
  package.
- O sprite do Player encara o comando (modo `MovementDirection`), não a velocidade: no recuo do knockback ele olha
  para onde o direcional aponta.
