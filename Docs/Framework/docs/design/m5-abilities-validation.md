# M5 — Validação das ferramentas e dos samples do Abilities

- **Data:** 2026-10-13 · **Unity:** 6000.6.5f1 (URP) · **Packages:** Core 0.4.0, Stats 0.1.0 (+ correção `4f29bfa`),
  HFSM 0.1.0, Character 0.2.0, Combat 0.1.0, Abilities 0.1.0 (working copies, sem tag).
- **Evidências:** capturas em [`m5-abilities-validation/`](m5-abilities-validation/) (back buffer da própria janela,
  `GUIView.GrabPixels`) e os logs brutos (`*_run.txt`).
- **Cena e assets de evidência:** criados por script em `Assets/_AbilitiesToolsEvidence` (habilidade `Fireball`, efeito
  `Burn`, projétil `Ember`, atores `Caster` e `Target`, parede e chão), usados e **removidos** depois; não fazem parte do
  repositório.

## Interação real × automação sintética

**Nenhum passo desta validação foi feito com mouse ou teclado físicos.** O Editor foi controlado pelo Unity CLI:

| Tipo | Como | Onde |
|---|---|---|
| Seleção de asset | `Selection.activeObject` + `ActiveEditorTracker.ForceRebuild` (o clique físico no Project termina na mesma seleção) | inspectors |
| Edição de campo | `SerializedObject.FindProperty(...).floatValue` + `ApplyModifiedProperties` (o mesmo caminho do campo do Inspector, com Undo) | `Commit Time` da `Fireball` |
| Undo | `Undo.PerformUndo()` com grupo separado, como uma ação distinta do usuário | edição |
| Scene View | `SceneView.LookAt` e `RepaintAll`; o preview é desenhado pelo `duringSceneGui` do inspector aberto | preview |
| Play Mode | `EditorApplication.isPlaying`/`isPaused`; habilidade concedida e ativada pela API pública (`GrantAbility`, `TryActivateAbility`), como faria um script do jogo | debugger |
| Debugger | janela aberta pelo método do menu; ator escolhido pela seleção (mesmo efeito do clique na lista) | debugger |
| Botões de teste do debugger | **não clicados**; a ação do botão *Clear cooldowns* é coberta por teste EditMode (`AbilityToolsTests`) | — |

É uma verificação reproduzível dos caminhos de interação, não um teste de usabilidade. **Pendente do owner (mouse
físico):** a lista "Checklist com mouse físico" abaixo.

## Ferramentas (entrega 5)

| # | Ferramenta | Verificado | Evidência |
|---|---|---|---|
| 1 | Inspector de `AbilityDefinition` | Validação inline (aviso de input tag; **erro** "delivering action before the commit"); resumo (ativação, custos, commit `AtTime 0,1 s`, cooldown, tags); linha do tempo com execução, commit (amarelo), fases Windup/Release/Recovery, ações (a entrega em 0,05 s antes do commit em **vermelho**), janela; ator do preview escolhido automaticamente (`Caster`) | `01_ability_inspector.png` |
| 2 | Linha do tempo × edição | Antes: `Apply Effect To Targets` em `step #2 [BEFORE COMMIT]`, commit em `step #5`. `Commit Time = 0,04` pelo `SerializedProperty`: commit `step #2`, sem marca e 0 erros; Undo: commit 0,1 s e 1 erro | `edit_run.txt` |
| 3 | Preview na Scene | Cone de targeting (raio 3, 1,5 m à frente) no frame de facing do `Caster` (+X); 3 trajetórias em leque (30°) a partir do ponto `Hand`, um ponto por passo com gravidade; o disparo que sobe à parede para no passo 19 ("Obstacle"), os outros no chão no passo 31 | `02_scene_preview.png` |
| 4 | Inspector de `GameplayEffectDefinition` | Resumo legível: `HasDuration 3 s, every 0.5 s`; stacking (limite 3); modificador `Speed Multiplicative 0.8`; execução `Health -2`; tags concedidas `Burning`; imunidades/dispel/inibição/remoção | `03_effect_inspector.png` |
| 5 | Ability Debugger (Play Mode, pausado no passo 1698) | `Target`: time 1 e máscara hostil, `Burning ×1 ← Effect:0:1:0`, `Burn ×1 · 2,72 / 3,00 s · period 0,28 / 0,50 s`, eventos `#1684 applied Burn` / `+tag Burning` (ativação pedida no passo 1681 → ativada na A1 de 1682 → entrega em τ = 0,05 s = passo #2 = 1684). `Caster`: `Fireball ACTIVE τ 0,340 s #1 (Committed)`; a janela `Casting` [0; 0,3) já fechou (sem tags) | `04_debugger_target.png`, `05_debugger_caster.png`, `debugger_run.txt` |

### Testes automatizados das ferramentas (EditMode)

| Teste | O que garante |
|---|---|
| `TimelineRows_MatchARealExecution` (Δt 0,02; 1/60; 0,01; 1/30) | Passo de cada ação e primeiro passo/contagem de cada janela da linha do tempo **iguais** aos de uma execução real do `AbilityRig` (bordas 0,1999/0,2, janela de 0,0001 s, ação depois do fim) |
| `Timeline_FlagsDeliveriesBeforeTheCommit_AndActionsAfterTheEnd` | Marca de entrega antes do commit; ação depois do fim "never runs"; passos do commit e da execução |
| `Trajectory_EqualsTheDriverPositions_StepByStep` | Amostras do preview = posições do `ProjectileDriver` passo a passo (gravidade; mesma `ProjectileKinematics`) |
| `TrajectoryObstacle_IsFoundByTheSweep` | Parada no obstáculo pelo sweep do raio (face da parede − raio) |
| `EffectSummary_...`, `DebuggerModel_...` | Textos do resumo; linhas do debugger (concessões, tags, efeitos, reservas), log de eventos e *Clear cooldowns* |
| `ActorValidationTests` (4) | Validators da §15/§25.8 que dependem do ator: custo fora do stat set (erro), modificador fora do stat set (aviso), habilidade com projéteis sem driver (aviso), grupo de cooldown com durações diferentes |

## Problemas encontrados e corrigidos durante a entrega 5

| Problema | Correção |
|---|---|
| Projéteis com entrega externa (`CombatDelivery`) não consultavam obstáculos: atravessavam paredes (o driver pulava toda a detecção) | O driver varre só a `ObstacleMask` para entregas externas e encerra o disparo no obstáculo; a ponte do Combat varre o trecho encurtado. Teste de orquestração `CombatProjectile_IsStoppedByAnObstacle_BeforeReachingTheTarget` |
| Preview em Edit Mode não via objetos recém-movidos (transforms não sincronizados com a física) | `Physics.SyncTransforms()` antes do sweep do preview (só fora do Play Mode) |
| Regras de passo duplicadas entre runtime e ferramenta | `AbilityDefinition.WindowOverlaps`, `IsActionDue`, `HasReachedDuration` e `ProjectileKinematics.NextVelocity` usados pela execução, pelo driver e pelas ferramentas |

## Samples (entrega 6, cópias importadas)

Montados por script em `Assets/_AbilitiesSampleBuild`, copiados para `Samples~`, a pasta de montagem apagada,
importados pelo Package Manager (`Sample.Import`) em `Assets/Samples/RamiresTech Games - Abilities/0.1.0`, validados,
executados e removidos. As entradas de teclado foram **sintéticas** (`KeyboardAbilitySource.Press`,
`HeroKeyboardSource.PressAbility`, modo do rival por propriedade); o motor foi reposicionado uma vez por
`CharacterMotor.Teleport` para encostar no rival.

| Verificação | Resultado | Evidência |
|---|---|---|
| Importação e compilação | 2 samples importados, assemblies `...Samples.AbilitiesPlayground` e `...Samples.AbilitiesCombatCharacterHfsm` compiladas sem erros/avisos | — |
| Validator nas duas cenas, assets e prefabs | **0 erros, 0 avisos, 0 scripts ausentes** | `samples_validation.txt` |
| Abilities Playground (só Abilities + Stats, tudo em assets) | Soak (cone) aplica `Wet` nos dois dummies; Zap no mais próximo: `removed Wet (Interaction)` → `Stunned` no passo seguinte; Fireball (projétil do ponto `Hand`) aplica `Impact` + `Burn` no Hexer; Shield dispersa o veneno ativo (`Dispelled`) e bloqueia o próximo dardo (`blocked Poison (Immune)`); Cleanse remove o veneno; Energy Burst: 2 cargas, a terceira falha (`NoCharges`); passiva `FocusAura` regenera energia | `playground_run.txt`, `10_playground.png` |
| Abilities Combat Character HFSM | Bolt contra guarda: **Blocked** (chip 1,4); contra parry: **Parried**; sem defesa: **Hit 7** + `Burn` (via `ApplyGameplayEffectOnHit`); Dash: impulso (x −3 → −1,74), facing travado, i-frames; Flame Strike (`PerformAttack`): Hit 15 → `Event.HitConfirmed` → passiva **Battle Focus** (`ManaSurge` +5); Meditate ativo (`Meditation`) **interrompido** no passo seguinte ao golpe que interrompe (`Event.Interrupted` → `ended Meditate (Interrupted)`, efeito da janela removido); estados da HFSM (Cast, Hitstun, Attack, Guard) | `combat_character_hfsm_run.txt`, `11_combat_sample.png` |

Problemas encontrados e corrigidos nos samples: a interação Wet + Shock não removia `Wet` (a remoção usa as tags de
asset; o efeito `Wet` passou a ter a tag `Wet` como asset tag); `AbilityTargetPoint.Validate` acusava erro falso em
prefabs (procurava o ASC só em pais ativos; corrigido com `includeInactive` e teste de regressão); o painel mostrava
"Infinito" para efeitos infinitos; o script de montagem que apagava e recriava `Data` no mesmo passo criava
`Data 1` e perdia campos (montagem passou a exigir pasta vazia e a criar os assets depois de configurados, como no M4).

## Fechamento (entrega 6)

### Regressão completa do host (2026-10-13)
| Package | EditMode | PlayMode |
|---|---|---|
| Core | 62/62 | — (sem testes de PlayMode) |
| Stats | 126/126 | 4/4 |
| HFSM | 118/118 | 5/5 |
| Character | 164/164 | 85/85 |
| Combat | 164/164 | 105/105 |
| Abilities (inclui integrações e ferramentas) | 68/68 | 96/96 |
| **Total** | **702/702** | **295/295** |

QG12 (projeto vazio só com Core + Stats + Abilities): ver CHANGELOG/TESTING do package (134 testes, 0 mensagens do
compilador; as assemblies de integração e o sample 2 não compilam sem os outros packages, como esperado).

### Critérios de aceite (§18.2)
| # | Status | Evidência |
|---|---|---|
| AC1 | ✅ | Playground: 7 habilidades e 9 efeitos só com assets (projétil, periódico com stacking); sample 2: 5 habilidades só com assets |
| AC2 | ✅ | `AbilityLifecyclePlayModeTests` (ativa, passiva, por evento com a mesma validação); Battle Focus por evento no sample 2 |
| AC3 | ✅ | `AbilityCostPlayModeTests`; validator `OnFirstDelivery` × Combat (`IntegrationValidationAndAllocationTests`) |
| AC4 | ✅ | `ActiveEffectSetTests`, `AbilitySystemPlayModeTests` |
| AC5 | ✅ | Testes com ordem embaralhada do `AbilityRig` (efeitos, habilidades, projéteis com saturação) |
| AC6 | ✅ | Δt 0,02/1/60/0,01/0,0333 (`AbilityLifecyclePlayModeTests`, `CooldownAndCommandPlayModeTests`, `AbilityToolsTests`) e `timeScale` 0,5 no player loop (`ClosurePlayModeTests`) |
| AC7 | ✅ | `CombatOrchestrationPlayModeTests` (n + 2, n + 1, Blocked/PierceGuard, Parried, obstáculo, hitstop) sem mudar o Combat |
| AC8 | ✅ | `ClosurePlayModeTests.FiftyFullActors_WithPhysics_DoNotAllocate_InTheSteadyState` (50 atores PhysX com custo, cooldown, targeting de área, leque de projéteis, área persistente, janela e efeitos periódicos); efeitos com 50 atores; integrações |
| AC9 | ✅ | Busca por nomes de jogo no código, assets e samples do package: só nas regras de governança (CLAUDE/ROADMAP) |
| AC10 | ✅ | QG1–QG12 abaixo |
| AC11 | ✅ | Testes de `Rebuild` (entrega 1) |
| AC12 | ✅ | `ConditionalEffectTests`, validator de ciclos |

### Quality Gates
| QG | Status | Observação |
|---|---|---|
| QG1 Compila sem erros/avisos | ✅ | console 0/0 no host; QG12 0 mensagens |
| QG2 Sem jogo consumidor | ✅ | QG12 em projeto vazio |
| QG3 Testes verdes | ✅ | tabela acima |
| QG4 Sample funcional | ✅ | dois samples importados e executados |
| QG5 Docs | ✅ | README, CONTRACTS, ARCHITECTURE, TESTING, ROADMAP, CHANGELOG |
| QG6 API pública listada | ✅ | CONTRACTS |
| QG7 Editor UX | ✅ | inspectors, preview, debugger, Header/Tooltip, menus |
| QG8 Validações | ✅ | §15 e §25.8 (definições, ator, conjunto, pontes) |
| QG9 Debug de runtime | ✅ | Ability Debugger, contadores de saturação, painéis dos samples |
| QG10 `check_dependencies` | ✅ | OK |
| QG11 Caso comum só pelo Inspector | ✅ | samples montados só com componentes e assets (scripts só de entrada/painel/rotina) |
| QG12 Instalação isolada | ✅ | 134 testes (66 EditMode + 68 PlayMode), 0 mensagens do compilador |

### Clone limpo
`git clone` do host + `git submodule update --init` em pasta temporária: os seis submodules nos commits fixados
(Abilities resolvido do repositório local por `url.insteadOf`, porque o repositório remoto ainda não existe — ver o
relatório final) e `check_dependencies` OK no clone.

## Auditoria preliminar (2026-10-13) — correções e gates

Decisões e correções (normativas em [m5-abilities-design §26](m5-abilities-design.md)):

| Item | Resultado | Teste |
|---|---|---|
| Passivas por acertos de projéteis e áreas | Mesma passiva "ganhar recurso ao acertar" funciona com corpo a corpo e projétil, ambos processados na A1 de *n + 1* (sem duplicação); `Blocked` e `Parried` geram seus próprios eventos; área com três alvos no mesmo passo gera três eventos na ordem (4, 6, 9) de identidade; disparo perfurante gera um evento por alvo; instigador destruído não recebe nada (contado); registrar e despachar não alocam | `ProjectileCausedEventsPlayModeTests` (5) |
| Parry de projétil | Disparo encerrado, sem reflexão e sem parry-stun no lançador | `BlockedAndParriedShots_...` |
| Áreas com entrega externa | **Bug corrigido:** o feedback era descartado (acertavam a cada passo, ignorando o intervalo); agora registram e contam | `SeveralTargetsInOneStep_...` (falhava com 12 eventos antes da correção) |
| Magnitudes do instigador em efeitos de golpe | **Bug corrigido:** usavam o fallback; agora capturadas (corpo a corpo na decisão, projéteis/áreas no lançamento, valor preservado mesmo com o lançador destruído ou com o stat alterado depois) | `HitEffectCapturePlayModeTests` (2) |
| Magnitudes do instigador em efeitos condicionais | Fallback mantido (determinismo da A2), agora **com aviso do validator**; decisão do owner pendente | `ConditionalEffect_ReadingInstigatorMagnitudes_...` |
| `GrantArmor` | Adiada (design §26, CONTRACTS, ROADMAP) | — |

Gates depois das correções:

| Gate | Resultado |
|---|---|
| Host EditMode | Core 62, Stats 126, HFSM 118, Character 164, Combat 164, Abilities 69 — **703/703** |
| Host PlayMode | Stats 4, HFSM 5, Character 85, Combat 105, Abilities 103 (inclui 35 de integração Combat/Character/HFSM) — **302/302** |
| Input System | `PlayerInputAbilitySourceTests` 2/2 (dentro dos 69 do Abilities) |
| Zero GC | `ClosurePlayModeTests` (50 atores), `IntegrationValidationAndAllocationTests`, `RecordAndDispatch_DoNotAllocate` — verdes |
| QG12 | 135 testes (67 EditMode + 68 PlayMode), 0 mensagens do compilador |
| `check_dependencies` | OK |

## Checklist com mouse físico (pendente do owner)

1. Selecionar uma `AbilityDefinition` no Project; passar o mouse sobre as barras da linha do tempo (tooltip com s,
   frames e passos) e abrir o foldout "Rows in seconds, frames and steps".
2. Editar `Commit Time` e as ações no Inspector (incluindo o menu de tipos do "+" das listas SR) e ver a linha do tempo
   e a validação mudarem; Ctrl+Z / Ctrl+Y.
3. Trocar o *Actor* do preview arrastando outro objeto da Hierarchy; girar o ator e ver cone e trajetórias seguirem o
   facing.
4. Em Play Mode, abrir `Tools/RamiresTech Games/Abilities/Ability Debugger`, clicar nos atores da lista, usar *Grant*,
   *Apply* e *Clear cooldowns and refill charges*.
5. Jogar os dois samples pelo teclado (Playground: Q W E R A S; sample 2: A/D, Espaço, J, L, Q, E, R, Shift e 1–4).
