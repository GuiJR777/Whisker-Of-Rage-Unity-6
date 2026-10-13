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

## Checklist com mouse físico (pendente do owner)

1. Selecionar uma `AbilityDefinition` no Project; passar o mouse sobre as barras da linha do tempo (tooltip com s,
   frames e passos) e abrir o foldout "Rows in seconds, frames and steps".
2. Editar `Commit Time` e as ações no Inspector (incluindo o menu de tipos do "+" das listas SR) e ver a linha do tempo
   e a validação mudarem; Ctrl+Z / Ctrl+Y.
3. Trocar o *Actor* do preview arrastando outro objeto da Hierarchy; girar o ator e ver cone e trajetórias seguirem o
   facing.
4. Em Play Mode, abrir `Tools/RamiresTech Games/Abilities/Ability Debugger`, clicar nos atores da lista, usar *Grant*,
   *Apply* e *Clear cooldowns and refill charges*.
