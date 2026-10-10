# M2 — Validação manual do Graph Editor (HFSM 0.1.0)

- **Data:** 2026-10-10 · **Unity:** 6000.6.5f1 · **Sample:** *Patrol & Alert* importado pelo Package Manager
  (`Sample.Import`) em `Assets/Samples/...` (removido depois da validação).
- **Como foi feito:** Editor do WOR controlado pelo Unity CLI. As capturas são o *back buffer* da própria janela
  (`GUIView.GrabPixels`), independente do que está em primeiro plano na tela.
- **Mouse e teclado:** eventos sintéticos (`MouseDown`/`MouseDrag`/`MouseUp`, `clickCount = 2`, `KeyDown Delete`)
  enviados à janela por `EditorWindow.SendEvent`, o mesmo pipeline de eventos que recebe o mouse físico. Undo/Redo
  pelos itens de menu **Edit > Undo / Edit > Redo**. Não houve um operador humano com mouse físico: esta é uma
  verificação reproduzível do caminho de interação, não um teste de usabilidade.

## Resultados

| # | Ação | Resultado verificado | Evidência |
|---|---|---|---|
| 1 | Play; abrir o asset (duplo clique = `OpenAsset`); selecionar *Player* | Barra: "Debugging: Player (keyboard)"; `Alive` em verde com "Active 9,23 s"; validação sem problemas; parâmetros ao vivo do Player | `14_root_player_alert.png` |
| 2 | Duplo clique no nó `Alive` | Breadcrumb `PatrolAndAlert > Alive`; `Patrol` em verde (sub-máquina `PatrolLoop`), `Alert` com limiar 10 | `02_alive_level.png` |
| 3 | Duplo clique no nó `Patrol` | Abre o asset da sub-máquina; breadcrumb `PatrolAndAlert > Alive > Patrol (PatrolLoop)`; `Go Right` em verde para o Player (id qualificado pela inclusão) | `03_patrol_submachine_player.png` |
| 4 | Selecionar *NPC* na Hierarchy | Barra: "Debugging: NPC (timer)"; parâmetros do NPC; destaque passou para o estado do NPC (Player estava em `Go Left`, NPC em `Go Right`, conferido pela API) | `04_patrol_submachine_npc.png` |
| 5 | Clique no item `Alive` do breadcrumb e depois no item raiz | Volta para profundidade 2 e depois 1 | `06_breadcrumb_back_to_alive.png` |
| 6 | Debugger com o Player após `Alert` | Caminho ativo com tempos e limiares; histórico (mais recente primeiro) com nomes, prioridades e condições: `Alive / Patrol / Go Left -> Alive / Alert [ParameterCondition]` (P5) e `Alive / Alert -> Alive / Patrol [StateCompletedCondition]` (P10) | `15_debugger_player_names.png` |
| 7 | Edit Mode, cópia descartável de `PatrolLoop`: arrastar da porta *Out* do Any State até a porta *In* de `Go Left` | Transição criada no asset (2 → 3, `Root -> Go Left`), aresta desenhada; o Validator avisa na hora "no conditions" | `07_edit_before.png`, `08_edit_after_drag.png` |
| 8 | Edit > Undo, depois Edit > Redo | 3 → 2 → 3 transições; grafo reconstruído a cada passo | `09_edit_after_undo.png`, `10_edit_after_redo.png` |
| 9 | Arrastar o título do nó `Go Left` (+150, +120) e Edit > Undo | Posição no asset (300, 0) → (450, 120) → (300, 0) | `11_edit_after_move.png` |
| 10 | Clique sobre a aresta (seleção) e tecla Delete; Edit > Undo | 3 → 2 → 3 transições | `12_edit_after_delete_edge.png`, `13_edit_after_undo_delete.png` |

## Problemas encontrados e corrigidos durante a validação
| Problema | Correção |
|---|---|
| A janela abria sem enquadrar o conteúdo: estados no canto e nó Any State fora da área visível | Enquadramento automático depois do layout ao abrir, navegar e voltar pelo breadcrumb |
| Minimapa fixo no canto superior esquerdo cobria nós (visível nas capturas 02–06, feitas antes da correção) | Minimapa ancorado no canto superior direito, reposicionado quando a vista muda de tamanho |
| Arestas do Any State cruzavam os estados do nível | Nó Any State posicionado acima do nível |
| Histórico e "Request State" do debugger mostravam caminhos de GUIDs | Nomes legíveis (`Alive / Patrol / Go Right`) |
| No sample, arestas para os nós externos passavam por cima de `Dead` | Posição de `Dead` no asset do sample ajustada |

## Observações
- Ao importar o sample, a Unity registra avisos transitórios "Missing types referenced" porque os assets são
  importados antes de os scripts do sample compilarem. Depois da compilação os dois assets resolvem todos os tipos e o
  Validator não acusa nada.
- Arestas nos dois sentidos entre os mesmos dois estados se sobrepõem (GraphView desenha arestas retas); os rótulos
  ficam sobrepostos. Limitação estética conhecida, sem efeito nos dados.
