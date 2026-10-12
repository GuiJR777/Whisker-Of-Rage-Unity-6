# M4 — Validação das ferramentas e dos samples do Combat (entrega 6)

- **Data:** 2026-10-11 (R20: 2026-10-12) · **Unity:** 6000.6.5f1 (URP) · **Packages:** Core 0.4.0, Stats 0.1.0, HFSM 0.1.0,
  Character 0.2.0, Combat 0.1.0 (working copies da entrega 6, sem tag).
- **Samples:** montados por script em `Assets/_CombatSampleBuild`, copiados para `Samples~`, importados pelo
  Package Manager (`Sample.Import`) em `Assets/Samples/...`, validados, executados e removidos. Os resultados abaixo
  são das cópias importadas.
- **Evidências:** capturas em [`m4-combat-validation/`](m4-combat-validation/) (back buffer da própria janela,
  `GUIView.GrabPixels`) e os logs brutos das execuções (`*_run.txt`).

## Interação real × automação sintética

**Nenhum passo desta validação foi feito com mouse ou teclado físicos.** O Editor foi controlado pelo Unity CLI:

| Tipo | Como | Onde |
|---|---|---|
| Mouse sintético | `MouseDown` / `MouseDrag` / `MouseUp` enviados por `EditorWindow.SendEvent` — o mesmo pipeline de eventos do mouse físico | conectar portas, mover nós |
| Drag-and-drop sintético | `DragAndDrop.objectReferences` + eventos `DragUpdated` / `DragPerform` enviados à janela | criar passo soltando um Attack |
| Teclado sintético | `KeyDown Delete` por `SendEvent` | excluir aresta |
| Seleção por API | `GraphView.AddToSelection(edge)` (o clique físico termina nesta chamada; o `worldBound` de arestas é vazio e não dá para mirar o centro) | selecionar link |
| Controles do painel | `value` atribuído ao `ObjectField`/`IntegerField` vinculados do painel (dispara o binding e o Undo como uma edição manual) | ação e prioridade do link |
| `SerializeReference` | elemento adicionado à lista `_conditions` pela mesma `SerializedProperty` que o painel vincula (`managedReferenceValue`); **o menu de tipos do botão "+" não foi clicado** | condição polimórfica |
| Botões do painel | `NavigationSubmitEvent` no `Button` (dispara `clicked`) | Delete, Repair Link Ids |
| Undo/Redo | `EditorApplication.ExecuteMenuItem("Edit/Undo" / "Edit/Redo")`; grupos de Undo separados entre operações, como ações distintas do usuário | edição |
| Play Mode | bordas escritas por script no `CombatCommandBuffer` do jogador (os samples leem o teclado por eventos IMGUI, que o CLI não injeta); o Editor é pausado por um callback no instante exato do estado capturado | samples, destaque ao vivo |

É uma verificação reproduzível dos caminhos de interação, não um teste de usabilidade. **Pendente do owner
(mouse físico):** a lista da seção "Checklist com mouse físico" abaixo.

## Combo Graph Editor — edição (cópia descartável de `BoxingCombo`)

| # | Ação | Resultado verificado | Evidência |
|---|---|---|---|
| 1 | Abrir o asset por `OpenAsset` (caminho do duplo clique) | 6 passos, 6 links, nós Entry/Jab/Cross/Hook/Uppercut/Sweep/Smash, 6 arestas | `01_open.png` |
| 2 | Soltar `Swing.asset` (AttackDefinition) numa área vazia do grafo | 7 passos; nó `Swing` criado com o ataque | `02_drop_attack.png` |
| 3 | Arrastar do conector *Next* de Hook ao conector *In* de Swing | 7 links (Hook → Swing) | `03_connect.png` |
| 4 | Selecionar o link; Action = Heavy e Priority = 3 nos campos do painel; adicionar `DirectionCondition` (Back) | Link com ação Heavy, prioridade 3, 1 condição `DirectionCondition Back`; painel mostra os campos vinculados | `04_link_panel.png` |
| 5 | Arrastar o título do nó Swing (+160, +90) | Posição no asset (55, 219) → (215, 309) | `05_move.png` |
| 6 | Selecionar a aresta Hook → Swing, tecla Delete; Undo; Redo; Undo | Links 7 → 6 → 7 → 6 → 7 | `06_after_undo.png` |
| 7 | Botão *Delete (with its links)* do nó Swing; Undo | Passos/links 7/7 → 6/6 → 7/7 | — |
| 8 | Salvar, `ImportAsset(ForceUpdate)`, recarregar do disco | 7 passos, 7 links; `Hook → Swing Heavy p3 [DirectionCondition Back]`; Swing em (215, 309); o YAML contém `type: {class: DirectionCondition ...}`; condições `ContactCondition` dos ramos Uppercut/Sweep preservadas | — |
| 9 | Reabrir a janela depois da reimportação | Mesmo grafo (7 nós de passo + Entry, 7 arestas) | `09_reopened.png` |
| 10 | Duplicar um elemento de `_links` como no Inspector (`DuplicateCommand`, mesmo id) | 8 links; Validator: 1 erro (id duplicado); painel mostra o erro e *Repair Link Ids* | `10_duplicate_id.png` |
| 11 | Clicar *Repair Link Ids*; Undo; Redo | Erros 1 → 0 → 1 → 0; 8 ids únicos em 8 links | `11_repaired.png` |

## Combo Graph Editor — destaque ao vivo e cadeia (Play Mode, sample Combat Arena)

O jogador (`Player (keyboard)`) selecionado na Hierarchy; eventos `ComboAdvanced`/`ComboReset` registrados com o
tempo de jogo.

| # | Situação | Estado lido do controller e eventos | Evidência |
|---|---|---|---|
| L1 | Jab executando (pausado nesse passo) | passo Jab, cadeia 1; `Advanced Jab chain 1` | `20_live_jab.png` |
| L2 | Light de novo: Cross | passo Cross, cadeia 2; `Advanced Cross chain 2` | `21_live_cross.png` |
| L3 | Cross terminou | continuação aberta (`open = True`); nó Cross em amarelo, "Open 0,23 s" | `22_live_open.png` |
| L4 | Timeout (0,25 s) esgotado | passo Entry, cadeia 0; `Reset Timeout` | `23_live_reset.png` |
| L5 | Jab enquanto o manequim Attacker acerta (janela 0,35–0,45 s) | `Advanced Jab chain 1` → `Reset Interrupted` no mesmo instante de `Player received Hit (interrupts)` | `24_live_interrupted.png` |

## Ferramentas de Attack e debugger

| Ferramenta | Verificado | Evidência |
|---|---|---|
| Inspector de `AttackDefinition` (Hook) | Linha do tempo: startup/ativo/recovery, janela ativa com marcadores de keyframe, Δt do projeto, total em s · frames@60 · passos; linha de scrub; controles do preview (ator, tempo, passo #10 = 12,6 f) | `30_attack_inspector.png` |
| Preview na Scene | Hitbox do Hook no tempo 0,21 s posicionada pela âncora `Hand` do jogador (rótulo "anchor Hand"), amostras por passo; testes de EditMode garantem a mesma pose do runtime (`HitboxPose`) | `31_scene_preview.png` |
| Combat Debugger | Seleção do jogador, ataque/contato, combo, agarrão e **golpes causados** (Blocked, depois Hit com guard-break/poise-break na Guard) | `32_debugger.png` |

## Samples (cópias importadas)

| Sample | Verificado | Log |
|---|---|---|
| Ambos | Importados por `Sample.Import`, compilados sem erros/avisos; Validator nas duas cenas e nos assets de `Assets/Samples`: **0 erros, 0 avisos, 0 scripts ausentes** | — |
| Combat Arena | Cadeia Jab → Cross → Hook (Flinch, Stagger, Knockback com poise-break); Smash derruba; Jab no chão = **Miss**; Jab logo após levantar = **Invulnerable**; Guard: **Blocked** (chip 1,8) e Smash = Hit com guard-break; Parry = **Parried** (ParryStun no atacante); Dodge = **Dodged**; agarrão (`IsGrabbed` = true), pummel (Hit 3) e arremesso cuja hitbox carregada atinge a Guard (Blocked → Knockdown). Os seis `HitOutcome` produzidos. | `arena_run.txt` |
| Combat Character HFSM | 3 runners, **1 definição**; rotina do rival (Attack, Guard, Jump, Airborne, Dash) acertando o jogador (Hitstun, Down); `FROZEN` nos dois lados durante o hitstop; agarrão: parceiro `Grabbed+HELD`; pummel e arremesso **executados pela máquina de estados** (dono External); a hitbox carregada do parceiro arremessado derruba o rival (Hit → Knockdown) | `character_run.txt` |

## Problemas encontrados e corrigidos durante a validação

| Problema | Correção |
|---|---|
| Com `Action Owner = External`, a máquina de estados nunca reivindicava pummel nem arremesso: `CanExecuteAction` era falso para tudo durante o agarrão | `CanExecuteAction` responde, durante o agarrão, se o arremesso/pummel executaria (mesma checagem de `TryGrabAction`); testes PlayMode e de integração com HFSM |
| Nenhuma condição entrava num estado Guard com o dono External (o `IsGuarding` depende da intenção que o próprio estado escreve) | `CombatStateCheck.IsGuardHeld` (guarda segurada no buffer); teste da HFSM |
| Debugger mostrava "Pending" como pedido de um controller com dono `Combat` | Mostra "-" para o dono `Combat` |
| Rótulo da hitbox sobreposto ao da âncora no preview | Rótulo acima da forma |
| Sample: perfis do time 1 calculados com `DEFAULT_HOSTILE_TEAMS & ~(1 << time)` não eram hostis ao time 0 | Máscara "todos menos o próprio time e o neutro" nos perfis dos samples |
| Sample: aresta Jab → Sweep cruzava o nó Uppercut | Posições dos passos no asset do sample |
| Script de montagem: um `GrabDefinition` no mesmo caminho da ação `Grab` sobrescrevia a ação; referências a assets criados depois eram gravadas nulas; `NewScene` descarregava os assets já criados | Caminhos únicos, assets criados em memória e gravados juntos (re-serialização depois de todos existirem), cena criada antes dos assets |

## Pose de autoria das âncoras (R20, auditoria da entrega 6)

Correção: pose de autoria canônica, relativa ao frame de facing (ADR-0016, refinamento; especificação §24.4). Sample
*Combat Character HFSM* reconstruído com a âncora `Hand` dentro do `Model` girado pelo `TransformFacingPresenter`
(Jab, Cross e Launcher ancorados), reimportado, validado (0/0, sem scripts ausentes) e executado.

| Verificação | Resultado | Evidência |
|---|---|---|
| Edit Mode (modelos ainda não girados): preview do Jab no jogador (facing +X, em x = −3) e no rival (facing −X, em x = 2) | Hitbox em x = −2,15 e x = 1,15: 0,85 m à frente de cada um, mão do lado certo (z = −0,15 / +0,15) | `r20_run.txt`, `33_r20_preview_rival.png` |
| Play Mode logo após o `Awake` e 3 s depois: pose capturada e hitbox dos três atores | Pose capturada (0,15; 1,15; 0,35) nos três (facings +X, −X, −X); hitbox 0,85 m à frente; modelos já girados (forward = facing) | `r20_run.txt` |
| Rotina do sample com golpes ancorados | O rival (facing inicial −X) acerta o jogador (Hitstun, Down); o jogador acerta rival e parceiro | `character_run.txt` |
| Mutação: regra anterior (`Inverse(FacingRotation)` no `Awake`) restaurada temporariamente | 4 testes PlayMode com presenter real falham (facings +X, −X, −Z e mudança de facing; +Z passa) e 9 EditMode; com a correção, todos passam | — |

Os testes automatizados da correção estão em TESTING do Combat (`AnchorPoseTests`, `CharacterCombatBodyPlayModeTests`,
`AllocationPlayModeTests` com âncoras). No preview em Edit Mode o marcador "anchor Hand" aparece na posição atual do
transform (modelo ainda não girado); a hitbox é desenhada pela pose canônica.

## Estabilidade dos testes (R19)

Numa rodada completa do host nesta entrega, um teste de infraestrutura do Core falhou:

```
RamiresTechGames.Core.Tests.Editor.TemporaryAssetFolderTests.Delete_RemovesTheFolderFromDatabaseAndDisk_AndForgetsTheSessionKey
Unhandled log message: '[Error] Cannot open file 'Assets/_RamiresTechGamesCoreTests_922ca26a4253/Nested.meta' for write.'
```

As duas rodadas completas seguintes passaram (589/589).

Depois da correção do R20, a primeira rodada completa teve outra falha da mesma assinatura, agora no Stats:

```
RamiresTechGames.Stats.Tests.Editor.StableIdTests.Create_StableIdEqualsAssetGuid("ResourceDefinition")
SetUp : Unhandled log message: '[Error] Cannot open file 'Assets/_RamiresTechGamesStatsTests_71cb16892dda.meta' for write. '
```

As duas rodadas completas seguintes (consecutivas) passaram: 633/633 EditMode e 199/199 PlayMode cada. Registrado como **possível recorrência da família R19**
([riscos](../08-architectural-risks.md)); a causa raiz não foi demonstrada. Nenhum retry, espera ou `Ignore` foi
adicionado. As rodadas de release estão em TESTING/CHANGELOG da entrega.

## Checklist com mouse físico (pendente do owner)

1. Arrastar um Attack do Project para o grafo e conectar *Entry*/*Next* → *In* com o mouse.
2. Clicar numa aresta (seleção pelo clique) e excluir com Delete; Ctrl+Z / Ctrl+Y.
3. No painel do link, botão "+" de *Conditions* → escolher `DirectionCondition` no menu de tipos; editar campos.
4. Fechar o Editor, reabrir o projeto e o asset: condições, posições e links preservados.
5. Duplicar um link pelo Inspector do asset (menu de contexto do elemento) e usar *Repair Link Ids*.
6. Em Play Mode no *Combat Arena*, jogar a cadeia pelo teclado (J, J, J; J + K) com o Combo Graph aberto e o jogador
   selecionado.
7. Selecionar um Attack e arrastar o scrub da linha do tempo vendo a hitbox se mover na Scene.

## Observações
- `Dodged` e `Invulnerable` aparecem uma vez por passo enquanto a esquiva/invulnerabilidade cobre a janela
  (comportamento documentado: esses outcomes liberam o registro da janela).
- A sobreposição de rótulos de arestas paralelas continua possível no GraphView (arestas retas), como no M2.
