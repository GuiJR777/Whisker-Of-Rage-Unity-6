# M4 — Combat + Combos · Especificação técnica

- **Status:** proposta para revisão (design only — **não implementar antes da aprovação**) · **Data:** 2026-10-10
- **Unity:** 6000.6.5f1 · **Package:** `com.ramirestechgames.combat` (L3, `RamiresTechGames.Combat`)
- **Dependências (grafo):** hard `core`, `stats`; opcionais `hfsm`, `character`, `com.unity.inputsystem` (ADR-0002).
- **Base contratual:** Core `v0.3.0`, Stats `v0.1.0`, HFSM `v0.1.0`, Character `v0.1.0` (sem alterar APIs aprovadas;
  mudanças propostas em outros packages estão isoladas na §20 e dependem de decisão).
- **Inspiração de mecânicas:** gênero beat'em up (combos leve/pesado, hit-confirm, agarrões, reações, bloqueio e
  parry). O BeatEmUpTemplate2D é só referência de *game feel*; nenhum código, asset ou estrutura dele é consultado ou
  copiado. Nada de mecânicas específicas de Ryu ou do Whiskers of Rage; habilidades elementais e Jutsus pertencem ao
  Abilities (M5).
- **ADRs propostos:** 0013–0018 (§22). Decisões pendentes: §21.

---

## 1. Objetivo e escopo

Um sistema de combate corpo a corpo **genérico, data-driven e determinístico por passo fixo**, usado igualmente por
jogador e inimigos: ataques com fases, hitboxes/hurtboxes 3D, detecção e resolução de acertos independentes da ordem
entre atores, dano por pipeline configurável sobre o Stats, defesa (bloqueio, parry, esquiva, invulnerabilidade),
poise e reações (hitstun, knockback, launch, knockdown), combos por grafo com input buffer e janelas de cancelamento,
interrupções, agarrão/arremesso e integração com HFSM, Character e Stats.

### 1.1 Obrigatório no M4 · pendente de decisão · adiado

| Obrigatório no M4 | Pendente de decisão (§21) | Adiado (fora do M4) |
|---|---|---|
| `AttackDefinition` com fases, janelas ativas, propriedades de acerto, cancelamentos, armadura/invulnerabilidade | Modelo de tempo (D1) | Projéteis e efeitos elementais (Abilities, M5) |
| Hitboxes/hurtboxes 3D com consultas `NonAlloc` e anti-tunneling | Autoria das hitboxes (D2) | Clash de hitboxes (golpe × golpe) |
| Resolução em duas fases (detecção → resolução), trocas simultâneas | Fila de bordas genérica no Core (D5) | Wall bounce, ground bounce, OTG avançado, escala de gravidade de juggle |
| Pipeline de dano (`IDamageStep`) sobre Stats; 6 `HitOutcome` | `GameplayTag` no Core (D6) | Escape de agarrão por "mash", contra-agarrão |
| Bloqueio direcional, guard break, parry com janela, esquiva/i-frames, invulnerabilidade | Poise como recurso do Stats (D7) | Rede/rollback (R14) |
| Poise/armadura, hitstun, blockstun, knockback, launch, knockdown, levantar | Hitstop × motor (D8) | Dano por região (cabeça/corpo) |
| Combos por grafo, input buffer, janelas de cancelamento, timeout, interrupções | Escopo do agarrão (D9) | IA de combate (M6) |
| Agarrão/arremesso básico (segurar, golpear, arremessar contra outros) | Movimento de ataque (D10) | Animator/VFX/áudio (só eventos; adapters no jogo ou samples) |
| Integrações HFSM, Character, Stats, Input System | Times/facções (D11), samples (D12), trocas (D13), editor de combo (D14) | |
| Inspector com timeline, gizmos com scrub, Combo Graph Editor, Combat Debugger, validators | | |

---

## 2. Conceitos

| Termo | Definição |
|---|---|
| **Ação de combate** | Vocabulário de entrada do jogo (`CombatActionDefinition`: ex. "Leve", "Pesado", "Agarrar"). O package não conhece nomes. |
| **Borda de ação** | Ocorrência enfileirada no `CombatCommandBuffer` com token, instante e payload (ação, direção). Mesmas garantias do ADR-0004. |
| **Ataque** | `AttackDefinition`: linha do tempo com **startup → ativo(s) → recovery**, em segundos de jogo. |
| **Janela ativa** | Intervalo em que uma ou mais hitboxes daquele ataque consultam a física. |
| **Hitbox / Hurtbox** | Volume que acerta (do ataque) / volume que recebe (do alvo, `HurtBox` com collider em camada própria). |
| **Acerto (hit)** | Par (instância de ataque, alvo) detectado num passo; vira um `HitResult` na resolução. |
| **Hit-confirm** | O ataque conectou (`Hit` ou `Blocked`, conforme a aresta do combo exigir). |
| **Hitstun / Blockstun** | Tempo em que o alvo não age após ser atingido / após bloquear. |
| **Poise / armadura** | Resistência a interrupção: enquanto há poise (ou numa janela de armadura), o golpe causa dano mas não interrompe. |
| **Hitstop** | Congelamento curto do atacante e do alvo no impacto (sensação de peso). |
| **Janela de cancelamento** | Intervalo de um ataque em que outra ação (ataque, pulo, dash, bloqueio) pode interrompê-lo voluntariamente. |
| **Interrupção** | Fim involuntário de um ataque (o atacante foi atingido, agarrado, morreu). |

---

## 3. Arquitetura

### 3.1 Camadas e assemblies

| Camada | Assembly | Conteúdo |
|---|---|---|
| Domain (puro) | `RamiresTechGames.Combat` | `AttackTimeline`, `ComboRunner`, `HitRegistry`, `DefenseModel`, `PoiseModel`, `ReactionModel`, `DamagePipeline`, `HitResolver`, `GrabModel`; recebem Δt, sem `UnityEngine.Object` em estado mutável |
| Unity Integration | `RamiresTechGames.Combat` | `CombatController` (atacante), `CombatTarget` (defensor), `HurtBox`, `HitboxAnchor`, `CombatCommandBuffer`, `RigidbodyKnockbackReceiver`, definições (SO) |
| Integration (opcionais) | `.Integration.HierarchicalStateMachine`, `.Integration.Character`, `.Integration.InputSystem` | Estados/condições de combate; knockback, movimento e facing via Character; fonte de input |
| Editor | `RamiresTechGames.Combat.Editor` | Inspector de ataque com timeline e preview, gizmos com scrub, Combo Graph Editor, Combat Debugger, validators |

Stats é dependência hard (grafo): o pipeline de dano lê `IStatValueSource` e aplica em `ResourcePool`. Abilities (M5)
estende o combate por `IHitEffect` e `IDamageStep` sem que o Combat o conheça.

### 3.2 Componentes de runtime

| Componente | Banda | Responsabilidade |
|---|---|---|
| `CombatCommandBuffer` | (escrito em `COMMAND_SOURCES`/`DECISION`) | Estado (`GuardHeld`, direção) e bordas de ação tokenizadas (D5) |
| `CombatController` | `COMBAT` (−600), `FixedUpdate` | Combo runner, linha do tempo do ataque ativo, **fase de detecção** das hitboxes |
| `CombatTarget` | `COMBAT + 50`, `FixedUpdate` | **Fase de resolução**: defesa, poise, dano, reações, hitstun/knockdown; registra hurtboxes |
| `HurtBox` | — | Collider na camada de hurtboxes; aponta para o `CombatTarget`; multiplicador opcional (adiado) |
| `HitboxAnchor` | — | Transform nomeado do ator (mão, arma, osso) onde formas de hitbox podem ser ancoradas (D2) |
| `RigidbodyKnockbackReceiver` | — | `IKnockbackReceiver` padrão para alvos sem Character (caixas, barris) |

O `CharacterMotor` (banda `CHARACTER`, −500) roda **depois** das duas fases no mesmo `FixedUpdate`: knockback/launch
aplicados na resolução entram no motor no mesmo passo (latência zero, §6.4 do M3).

### 3.3 Fluxo de um passo fixo

```
COMBAT (−600)   CombatController (todos os atores)
                  1. avança timelines (ataques, janelas, hitstop, timeouts) com Δt
                  2. combo runner: consome a borda de ação permitida (dono Combat) ou executa o pedido (dono External)
                  3. detecção: para cada janela ativa, consulta a física (NonAlloc) → enfileira HitCandidate no alvo
COMBAT+50       CombatTarget (todos os alvos)
                  4. resolução: ordena candidatos do passo (determinístico), defesa → poise → dano → reação
                  5. efeitos: knockback (IKnockbackReceiver), interrupção do ataque do alvo, hitstop dos dois lados
                  6. eventos enfileirados
CHARACTER(−500) CharacterMotor aplica o impulso do knockback no mesmo passo
fim do passo    eventos entregues (FIFO, cópia da fila — mesma regra da §19.8 do M3)
```

Como **toda** detecção do passo termina antes de qualquer resolução (bandas diferentes), o resultado não depende da
ordem entre atores (ADR-0003 deixa explícito que a ordem entre instâncias não é garantida). Trocas simultâneas
(A acerta B e B acerta A no mesmo passo) resolvem os dois acertos (D13).

---

## 4. Definições (ScriptableObjects, imutáveis em runtime)

### 4.1 `CombatProfileDefinition` (por arquétipo de ator)
| Grupo | Campos |
|---|---|
| Stats | `AttackPowerStat`, `DefenseStat` (opcionais), `HealthResource` (obrigatório), `PoiseResource` (opcional, D7), `GuardResource` (opcional; guard break por esgotamento) |
| Dano | `DamageProfile` (pipeline, §7) |
| Defesa | `BlockAngle` (graus, frontal), `ParryWindow` (s), `ParryCooldown` (s), `GuardChipMultiplier` padrão |
| Reações | `DefaultHitReaction`, `KnockdownGetUpTime`, `GetUpInvulnerability` (s), `MaxAirHits` (juggle) |
| Entrada | `InputBufferTime` (s, janela das bordas de ação), `ComboTimeout` padrão |
| Times | `Team` (bit), `HostileTeams` (máscara) (D11) |
| Hitstop | `HitstopScale` (multiplicador do hitstop recebido/causado) |

### 4.2 `CombatActionDefinition`
Identidade de uma ação (`StableId` como no Stats), `Priority` (desempate entre bordas simultâneas: maior primeiro;
empate → mais antiga) e `Tags`. Exemplos do sample: Leve, Pesado, Agarrar, Especial. Fecha a decisão adiada do
ADR-0004 para o M4 (prioridade entre bordas simultâneas e janela do buffer).

### 4.3 `AttackDefinition`
| Grupo | Campos |
|---|---|
| Linha do tempo | `Startup`, `Recovery` (s); `ActiveWindows[]` (início, fim, hitboxes, `HitProperties`); duração total derivada |
| Hitboxes | Forma (`Box`/`Sphere`/`Capsule`), centro/tamanho/rotação no espaço do facing do ator, `Anchor` (id de `HitboxAnchor`, vazio = raiz) (D2) |
| `HitProperties` | `DamageMultiplier`, `PoiseDamage`, `Reaction` (`HitReactionDefinition`), `Hitstop` (s), `ChipMultiplier`, `MaxTargets`, `HitInterval` (multi-hit; 0 = uma vez por janela), flags `Unblockable`, `Unparryable`, `GuardBreak`, `HitsGrounded`, `HitsAirborne`, `HitsDown`, `Tags` |
| Requisitos | `RequiresGrounded` / `RequiresAirborne`, recurso a consumir (opcional, ex. energia: `ResourceCost` do Stats) |
| Cancelamentos | `CancelWindows[]`: intervalo, condição (`Always`, `OnHit`, `OnBlock`, `OnWhiff`), destinos (`CombatAction`s, `Jump`, `Dash`, `Guard`, `AnyAttack`) |
| Defesa própria | `ArmorWindows[]` (intervalo, limiar de poise ou "super armor"), `InvulnerabilityWindows[]` (i-frames do ataque) |
| Movimento | `MovementMultiplier` durante o ataque, `FacingLocked`, `MovementEvents[]` (instante, impulso no espaço do facing) (D10) |
| Grab | `Grab` (`GrabDefinition`, opcional, §11) |

### 4.4 `HitReactionDefinition`
`Severity` (`Flinch`, `Stagger`, `Knockback`, `Launch`, `Knockdown`), `Hitstun`, `Blockstun` (s), `Knockback`
(velocidade plana no espaço do atacante + vertical), variante aérea, `KnockdownTime`, `CausesGetUpInvulnerability`.
Poise quebrado eleva a severidade mínima para `Stagger` (configurável).

### 4.5 `ComboDefinition` (grafo, §10)
Nós = passos de combo (`AttackDefinition` + opções); arestas = (ação, condições SR, prioridade); entradas por contexto
(chão/ar); `Timeout` de reset. Editado no Combo Graph Editor (§15).

### 4.6 `DamageProfileDefinition`
Lista ordenada de `IDamageStep` (`[SerializeReference]` + seletor do Core, ADR-0008). §7.

### 4.7 `GrabDefinition`
Alcance, tempo de segurar, `PummelAttack`, `Throws[]` (direção, `AttackDefinition` do arremesso, velocidade de
lançamento, hitbox carregada pelo arremessado), alvos agarráveis (flag do alvo, tamanho máximo).

---

## 5. Tempo (ADR-0013, fecha o risco R8)

- **Unidade:** segundos de tempo de jogo, acumulando o **Δt fixo** (`FixedUpdate`), como o Character (ADR-0012).
  O Animator nunca dirige a simulação; adapters de apresentação podem seguir a linha do tempo.
- **Autoria:** em segundos; o inspector mostra também **frames de referência** (60 fps) e **passos** no Δt atual do
  projeto, para quem pensa em frames.
- **Regra de janela (normativa):** uma janela `[a, b)` está ativa num passo se o intervalo do passo `[t, t + Δt)`
  **sobrepõe** `[a, b)`. Consequências: (1) nenhuma janela de duração > 0 é pulada em nenhum Δt; (2) uma janela
  dura entre `⌈(b−a)/Δt⌉` e `⌈(b−a)/Δt⌉ + 1` passos; (3) o mesmo ataque é idêntico em `timeScale` diferentes (tempo
  de jogo). Testes com Δt 0,02 / 1/60 / 0,01 / 0,0333.
- **Hitstop:** pausa as linhas do tempo de combate do ator (ataque, janelas, timeouts, hitstun) por `Hitstop` s de
  jogo; ver D8 para o efeito no motor.
- **Ordem de avaliação no passo:** timeline → cancelamentos/novas ações → janelas ativas (detecção) — então uma ação
  aceita num passo pode ter janela ativa já no passo seguinte, nunca no mesmo (sem "ativo no frame 0" acidental).

---

## 6. Detecção de acertos (ADR-0014, ADR-0016)

1. Para cada janela ativa do ataque corrente, cada hitbox calcula a pose: âncora (ou raiz) × facing lógico do ator
   × offset da definição.
2. **Anti-tunneling:** se o deslocamento da hitbox desde o passo anterior for maior que metade da menor dimensão, a
   consulta é subamostrada (até `MAX_SUBSTEPS` = 4 poses interpoladas); acima disso, conta `TunnelingClampCount`.
3. Consulta `PhysicsScene.OverlapBox/Sphere/Capsule` (`NonAlloc`) na máscara de hurtboxes, triggers incluídos só se a
   camada de hurtbox for trigger (configurável). Buffer primário + estendido pré-alocados, com contagem de saturação
   residual (mesma política aprovada no GroundSensor, §19.8 do M3).
4. Filtros: própria hierarquia, time (`HostileTeams`), alvos já atingidos nesta janela (`HitRegistry`: alvo → último
   acerto; `HitInterval` libera multi-hit), `MaxTargets` por janela, estado aceito (`HitsGrounded/Airborne/Down`).
5. Para cada `CombatTarget` distinto (várias hurtboxes do mesmo alvo contam uma vez), cria um `HitCandidate` (instância
   do ataque, janela, hitbox, ponto aproximado, direção) e enfileira no alvo. Ordenação determinística no alvo:
   prioridade do ataque, depois distância, depois id estável do atacante.

Sem alocação: `HitCandidate` é struct; filas de tamanho fixo por alvo (saturação contada).

---

## 7. Resolução e dano (ADR-0014, ADR-0017)

### 7.1 Precedência dos 6 `HitOutcome`
Avaliada por candidato, na ordem:

| # | Outcome | Quando | Efeito |
|---|---|---|---|
| 1 | `Invulnerable` | Alvo invulnerável (levantar, spawn, script) | Nada; registro e evento |
| 2 | `Dodged` | Alvo em i-frames de esquiva (janela de invulnerabilidade marcada como esquiva) | Nada; evento (gatilho de "esquiva perfeita" para o jogo) |
| 3 | `Miss` | Hurtbox sobreposta, mas o ataque não pode atingir o estado do alvo (`HitsAirborne`/`HitsDown`/`HitsGrounded` falsos) | Nada; o atacante não registra hit-confirm |
| 4 | `Parried` | Alvo em janela de parry, de frente, ataque não `Unparryable` | Sem dano; atacante em parry-stun; janela de parry consumida |
| 5 | `Blocked` | Alvo bloqueando, golpe dentro de `BlockAngle`, não `Unblockable` | Dano de chip, dano de guarda; blockstun; guard break se esgotar ou `GuardBreak` |
| 6 | `Hit` | Demais casos | Dano, poise, reação, hitstop |

Um ataque que termina sem nenhum `Hit`/`Blocked` é um *whiff* (`AttackEnded.HadContact = false`), base das arestas
`OnWhiff`.

### 7.2 Pipeline de dano
`DamageContext` (struct): atacante e alvo como `IStatValueSource`, propriedades do golpe, outcome, multiplicadores,
`ICombatRandom` injetado (determinismo; nunca `UnityEngine.Random`). `DamageProfileDefinition` executa `IDamageStep`s
em ordem sobre um `DamageCalculation` (base, flat, multiplicadores, final). Passos incluídos:

| Passo | Faz |
|---|---|
| `BaseFromStatStep` | base = stat de ataque do atacante × `DamageMultiplier` do golpe |
| `DefenseStep` | redução por stat de defesa (fórmula configurável: subtrativa ou percentual) |
| `BlockChipStep` | em `Blocked`, aplica `ChipMultiplier` |
| `TagMultiplierStep` | multiplicador por tag do golpe × tag do alvo (fraquezas genéricas) |
| `CriticalStep` | chance/multiplicador por stats, com o random injetado |
| `ClampStep` | mínimo/máximo e arredondamento |

O resultado é aplicado com `ResourcePool.Decrease(amount, instigator: HitInfo)` no recurso de vida do perfil; zero ou
esgotamento geram eventos do Stats (morte é decisão do jogo/HFSM, não do Combat). `IHitEffect` (Abilities) roda depois
do dano, por hit confirmado.

### 7.3 Poise, armadura e reação
1. Poise (D7): `PoiseDamage` reduz o recurso de poise; regen e atraso vêm do próprio Stats.
2. Interrupção: o golpe interrompe o alvo se **não** houver armadura ativa e (poise ≤ 0 **ou** o alvo não tiver
   poise configurado). Poise quebrado eleva a severidade mínima (`Stagger`) e reinicia o recurso.
3. Reação: `HitReactionDefinition` (variante aérea se o alvo estiver no ar). Hitstun/blockstun no `ReactionModel` do
   alvo; knockback/launch via `IKnockbackReceiver`; `Knockdown` entra em estado caído e, ao levantar,
   `GetUpInvulnerability`.
4. Juggle: alvo no ar conta `AirHits`; acima de `MaxAirHits` só cai (sem novo launch).
5. Interromper o alvo encerra o ataque dele (`AttackEnded(Interrupted)`), descarta bordas de combo pendentes e
   cancela pedidos externos pendentes dele (mesma semântica de cancelamento por token aprovada no M3).

### 7.4 Hitstop
Duração do golpe × `HitstopScale` de cada lado; atacante e alvo pausam as linhas do tempo de combate. O knockback de
um golpe com hitstop é aplicado **ao fim** do hitstop do alvo (o golpe "segura" e depois arremessa). Efeito no motor:
D8.

---

## 8. Defesa

| Mecanismo | Regra |
|---|---|
| **Bloqueio** | Estado (`GuardHeld` no buffer) permitido pelo estado do ator; direcional (`BlockAngle` em torno do facing lógico); bloqueio consome guarda (`GuardResource`, opcional); `GuardBreak` ou guarda esgotada → `Stagger` longo |
| **Parry** | Borda de ação de parry (uma `CombatAction` marcada) abre `ParryWindow`; golpe frontal dentro dela → `Parried`; o atacante entra em parry-stun (reação configurável); `ParryCooldown` impede spam; janela consumida no primeiro parry |
| **Esquiva** | Janelas de invulnerabilidade com marca `Dodge` (num ataque/ação de esquiva, ou concedidas pelo jogo); resultado `Dodged` |
| **Invulnerabilidade** | Handles independentes (`CombatTarget.AddInvulnerability(reason)` → handle, como as travas de facing do M3); levantar, spawn, cutscene |

Todos os estados defensivos são consultáveis por condições da HFSM e por eventos.

---

## 9. Comandos, posse e interrupções (ADR-0015)

- **`CombatCommandBuffer`:** mesmas 6 garantias do ADR-0004 e semântica de token do ADR-0011; bordas de ação com
  payload (ação, direção); estado `GuardHeld` e direção de mira. Janela de idade = `InputBufferTime` do perfil.
  Prioridade entre bordas simultâneas pela `CombatActionDefinition.Priority` (maior primeiro; empate → mais antiga).
- **Posse (espelha o §19.4 do M3):** `ActionOwner` = `Combat` (o combo runner consome a borda quando consegue
  executar) ou `External` (estado da HFSM faz *claim*, chama `RequestAction(token)` e o Combat consome no passo em que
  executa; `CancelActionRequest(token)` com as mesmas garantias aprovadas na §19.8 do M3: não executa, descarta a
  borda, registra e notifica uma vez, token antigo não cancela pedido novo).
- **Interrupções:** sair do estado de ataque da HFSM cancela pedidos pendentes e, por política do estado
  (`InterruptAttackOnExit`), encerra o ataque corrente com `AttackEnded(Interrupted)`; ser atingido sem armadura
  interrompe (§7.3); morte e agarrão também.
- **Cancelamentos voluntários:** dentro de uma `CancelWindow` cuja condição foi satisfeita, uma borda de ação
  compatível inicia o próximo ataque (combo/cancel) ou libera `Jump`/`Dash`/`Guard` — o Combat só expõe
  `CanCancelInto(target)`; quem executa pulo e dash continua sendo o Character, orquestrado pela HFSM (R9).

---

## 10. Combos

- **Grafo:** entradas por contexto (chão, ar) e ação; cada nó é um passo (ataque + opções: exige hit-confirm,
  reinicia timeout); arestas `(ação, condições, prioridade)` avaliadas na janela de cancelamento do nó corrente.
  Condições SR incluídas: `OnHit`, `OnBlock`, `OnWhiff`, `Grounded`, `Airborne`, `Held` (ação segurada por N s),
  `Direction` (relativa ao facing). Extensível por `[SerializeReference]`.
- **Runner (puro):** guarda o nó corrente, o tempo desde o último passo e o hit-confirm; no fim do ataque sem
  continuação, abre a janela de continuação até `Timeout`, depois volta à entrada. Bordas chegadas antes da janela
  ficam no buffer (dentro de `InputBufferTime`) — *input buffering* sem perder nem duplicar.
- **Determinismo:** mesma sequência de bordas e de hits → mesma sequência de ataques, em qualquer Δt (testes).
- **Mesmo sistema para jogador e inimigo:** a IA (M6) só escreve bordas no mesmo buffer.

---

## 11. Agarrão e arremesso (D9)

Agarrar é uma ação cujo ataque tem `Grab`: a janela ativa procura um alvo agarrável; sucesso → atacante em
`Grabbing`, alvo em `Grabbed`, preso a um `HitboxAnchor` do atacante. Durante o agarrão: `PummelAttack` por borda;
arremesso por direção → solta o alvo com velocidade (knockback) e o alvo **carrega uma hitbox** temporária que atinge
outros (reuso integral da detecção).

**Limite do contrato atual:** prender um alvo com `CharacterMotor` exige suspender o motor dele, e o Character 0.1.0
não tem essa operação. `Teleport` a cada passo não serve (cancela pedidos, zera canais e timers). Por isso D9 depende
de D8: com `Hold/Release` aditivo no motor (Character 0.2.0), o agarrão vale para qualquer alvo; sem ele, o M4 entrega
agarrão só para alvos `Rigidbody` simples (`IGrabbable` padrão) e adia o agarrão de personagens. Escape por "mash" e
contra-agarrão: adiados.

---

## 12. Integrações opcionais (ADR-0002)

| Assembly | Requer | Fornece |
|---|---|---|
| `Combat.Integration.HierarchicalStateMachine` | HFSM ≥ 0.1.0 | Estados: `AttackBehaviour` (claim → `RequestAction`, completa no fim do ataque, cancela no `OnExit`), `HitstunBehaviour`, `BlockstunBehaviour`, `KnockdownBehaviour`, `GetUpBehaviour`, `GuardBehaviour`, `GrabbingBehaviour`, `GrabbedBehaviour`. Condições: `HasCombatActionCommand` (espia + claim), `IsAttacking`, `AttackFinished`, `CanCancelInto`, `IsInHitstun`, `IsBlockstunned`, `IsKnockedDown`, `HitConfirmed`, `WasParried`, `IsGuarding`, `IsGrabbed`. |
| `Combat.Integration.Character` | Character ≥ 0.1.0 | `CharacterKnockbackReceiver` (`IKnockbackReceiver` → `IExternalForceReceiver.AddImpulse`); `CombatMovementModifierSource` (encadeia o modificador existente do motor e aplica `MovementMultiplier` do ataque / 0 em hitstun); travas de facing (`FacingController.LockFacing`) durante ataques com `FacingLocked`; facing lógico como orientação das hitboxes; `MovementEvents` via `AddImpulse` |
| `Combat.Integration.InputSystem` | Input System ≥ 1.0 | `PlayerInputCombatSource`: ações → bordas (só em `Update`), `GuardHeld`, direção relativa à câmera |

Com Stats hard, não há integração Stats separada. Abilities (M5) entra por `IHitEffect` e `IDamageStep`.

**Uso do `IExternalForceReceiver` (Experimental até o M4):** o Combat é o primeiro consumidor real. Proposta: promover
a interface a `Stable` no próximo minor do Character, se os testes de knockback/launch do M4 passarem sem mudança
de contrato.

---

## 13. Orquestração (R9)

A HFSM compõe os domínios; nenhum package chama outro fora das integrações. Definição de exemplo "Combatant":

```
Root
├── Alive
│   ├── Locomotion (sub-máquina Locomotion do Character)
│   ├── Attack            ← HasCombatActionCommand / CanCancelInto(AnyAttack)
│   ├── Guard             ← GuardHeld
│   ├── Hitstun           ← IsInHitstun (Any State de Alive, limiar alto)
│   ├── Knockdown → GetUp ← IsKnockedDown
│   └── Grabbing / Grabbed
└── Dead                  ← HealthDepleted (glue do jogo ou condição do Stats)
```

Entrar em Hitstun a partir de Attack chama `OnExit` do ataque → cancela pedidos pendentes de combate **e** de
Character (Jump/Dash, §19.8 do M3). Mesma definição para jogador e inimigo.

---

## 14. Execução e desempenho
| Item | Proposta |
|---|---|
| Loop | `FixedUpdate`: `CombatController` (`COMBAT`), `CombatTarget` (`COMBAT + 50`); fontes em `Update` |
| Eventos | `AttackStarted`, `AttackEnded`(motivo), `HitConfirmed`(atacante), `HitReceived`(alvo), `Parried`, `Blocked`, `GuardBroken`, `PoiseBroken`, `KnockedDown`, `GotUp`, `ComboAdvanced`, `ComboReset`, `ActionResolved`, `Grabbed`, `Thrown` — `readonly struct`, FIFO no fim do passo (cópia da fila) |
| Zero GC | Buffers e filas pré-alocados, structs, `ListenerList` local (dívida conhecida, não bloqueante); teste com 50 atores trocando golpes |
| Determinismo | Sem `Random`/`Time.time` no domínio; RNG injetado; ordenação determinística dos candidatos |

---

## 15. Ferramentas de Editor e estudo do Combo Graph Editor

### 15.1 Ferramentas
| Ferramenta | Conteúdo |
|---|---|
| Inspector de `AttackDefinition` | Timeline (startup/ativo/recovery, cancelamentos, armadura, i-frames) em s, frames@60 e passos no Δt; validação inline |
| Preview na Scene | Scrub da timeline mostrando as hitboxes posicionadas no ator selecionado (âncoras incluídas) |
| Gizmos em Play Mode | Hitboxes ativas, hurtboxes, último ponto de acerto, alcance de agarrão |
| Combo Graph Editor | Grafo de `ComboDefinition` (§15.2) com destaque do nó corrente do ator selecionado |
| Combat Debugger | Atores; ataque e fase; janelas ativas; combo; poise/guarda; hitstun; últimos 20 `HitResult`s; botões de teste |
| Validators | Janelas fora da duração, cancelamento sem destino, ataque sem hitbox, perfil sem recurso de vida, máscara de hurtbox vazia ou incluindo a camada do atacante, combo com nó inalcançável/ciclo sem timeout |

### 15.2 Estudo: reuso da infraestrutura GraphView da HFSM
**Situação verificada (2026-10-10, 6000.6.5f1):** mesmo Editor do spike do M2; `GraphView` sem `[Obsolete]` (0 de 90
tipos exportados); Graph Toolkit presente e inalterado (51 tipos) — os gatilhos de reavaliação do spike (valores
polimórficos no Graph Toolkit, deprecação do GraphView) **não** dispararam. A recomendação do spike/ADR-0001 da HFSM
continua válida.

| Peça da HFSM (`HierarchicalStateMachine.Editor`) | Linhas | Necessidade no Combo Graph | Reuso proposto no M4 |
|---|---|---|---|
| `StateMachineGraphModel` (modelo de edição sem UI, Undo, cascata, duplicação) | ~500 | Mesmo papel sobre `ComboDefinition` | **Padrão** (novo `ComboGraphModel`), não código |
| `StateMachineGraphView` (grade, zoom, arraste, conexão, framing, minimapa) | ~460 | Igual, sem hierarquia | Padrão; ~60% do comportamento é igual |
| `StateNodeView` / `TransitionEdgeView` | ~200 | Nó de passo (ataque, ícones de hit-confirm) e aresta com rótulo de ação | Padrão |
| `GraphInspectorPanel` (`PropertyField` + seletor do Core) | ~380 | Igual (condições SR) | Padrão |
| `StateSearchProvider`, `GraphStyles` | ~90 | Igual | Padrão |
| `StateMachineGraphWindow` (breadcrumb, sub-máquinas, debug ao vivo) | ~430 | Sem breadcrumb; debug do nó corrente | Padrão parcial |

**Recomendação (D14):** o Combo Graph Editor vive em `Combat.Editor`, implementa o próprio modelo de edição sem UI e
a própria vista GraphView seguindo o ADR-0001 da HFSM, **sem extrair abstrações para o Core no M4** (diretriz do
owner). As peças acima são registradas como candidatas; a extração só é avaliada no M6 (Behaviour Tree, terceiro
consumidor), com ADR e evidência concreta (diff de duplicação). Custo aceito: ~1.000–1.500 linhas de Editor com
padrão repetido (atualiza o risco R1). Formato do asset independente da vista (mesma garantia do ADR-0001).

---

## 16. Samples (D12)
- **Combat Arena** (só Combat + Stats): atores com `Rigidbody` e `RigidbodyKnockbackReceiver`, fonte de teclado
  IMGUI e fonte por script; manequins que bloqueiam, dão parry, esquivam, têm armadura; painel com o último
  `HitResult` (os 6 outcomes demonstráveis); combos leve/pesado com ramificação por hit-confirm; agarrão/arremesso.
- **Combat Character HFSM** (opcional, requer Character + HFSM): definição "Combatant" (§13) com a sub-máquina
  Locomotion do M3; jogador e inimigo com a mesma definição; knockback/launch/juggle no motor; cancelamento em
  pulo/dash.

---

## 17. API pública (proposta)
| Tipo | Categoria | Estabilidade |
|---|---|---|
| `CombatPackageInfo` | static | Stable |
| `CombatProfileDefinition`, `CombatActionDefinition`, `AttackDefinition`, `HitReactionDefinition`, `ComboDefinition`, `DamageProfileDefinition`, `GrabDefinition` | SO | Experimental |
| `HitProperties`, `ActiveWindow`, `CancelWindow`, `HitboxShape` | serializable | Experimental |
| `CombatController`, `CombatTarget`, `HurtBox`, `HitboxAnchor`, `CombatCommandBuffer`, `RigidbodyKnockbackReceiver` | MB | Experimental |
| `HitResult`, `HitOutcome`, `AttackEndReason`, `DamageContext`, `DamageCalculation` | struct/enum | Experimental |
| `IDamageStep`, `IHitEffect`, `IKnockbackReceiver`, `ICombatRandom`, `ComboCondition` | extension points | Experimental |
| Eventos (§14) | readonly struct | Experimental |
| Domain (`AttackTimeline`, `ComboRunner`, `HitRegistry`, `HitResolver`, modelos) | — | Internal |

---

## 18. Qualidade

### 18.1 Testes
**EditMode (domínio):** regra de janela por sobreposição em 4 Δt (nenhuma janela pulada; contagem de passos);
timeline com hitstop; precedência dos 6 outcomes (um teste por outcome e combinações); pipeline de dano (ordem dos
passos, chip, crítico determinístico, clamp); poise/armadura/quebra; parry (janela, frontal, cooldown, consumo);
bloqueio (ângulo, guard break); reações (variante aérea, juggle, knockdown/levantar com i-frames); combo runner
(ramificação por ação e hit-confirm, timeout, buffer dentro/fora da janela, prioridade entre bordas simultâneas,
determinismo em Δt diferentes); interrupção (ataque encerrado, bordas descartadas, pedidos cancelados); command
buffer (6 garantias + duas fases + cancelamento por token); validators; hitboxes com colliders reais (formas, âncoras,
filtros de time, multi-alvo, `MaxTargets`, multi-hit, saturação, anti-tunneling).

**PlayMode (física real):** hitbox rápida contra hurtbox fina sem tunneling; **independência de ordem** (mesma
cena, ordem de criação dos atores embaralhada → mesmos resultados) e trocas simultâneas; knockback/launch no
`CharacterMotor` aplicados no mesmo passo e com a distância do canal externo (§19.3 do M3); hitstop; juggle;
agarrão/arremesso atingindo terceiros; orquestração HFSM (Hitstun interrompe Attack e cancela Jump pendente);
Input System (uma borda por pressionamento com vários passos fixos); zero GC com 50 atores trocando golpes.

### 18.2 Critérios de aceite
| # | Critério |
|---|---|
| AC1 | Os 6 `HitOutcome` cobertos por testes (roadmap). |
| AC2 | Mesmo sistema e mesma definição de HFSM para jogador e inimigo; só a fonte de comandos muda. |
| AC3 | Resolução independente da ordem entre atores (teste com ordem embaralhada). |
| AC4 | Nenhuma janela ativa é pulada e combos são idênticos em Δt 0,02 / 1/60 / 0,01 / 0,0333 e `timeScale` 0,5. |
| AC5 | Knockback/launch via `IExternalForceReceiver` sem alteração de contrato (base para promovê-lo a Stable). |
| AC6 | Combos montados só com assets e com o Combo Graph Editor, sem código. |
| AC7 | Zero GC em regime com 50 atores; consultas de física sem alocação. |
| AC8 | Nenhum código ou asset de terceiros; nenhuma mecânica de jogo específico no package. |

### 18.3 Quality Gates
QG1–QG12 como nos milestones anteriores (inclui QG12 em projeto vazio com Core + Stats; integrações não compilam
lá por definição).

---

## 19. Estrutura do package (proposta)
```
com.ramirestechgames.combat/
├── Runtime/{Definitions, Commands, Timeline, Detection, Resolution, Damage, Defense, Combo, Grab, Components, Events}
├── Runtime/Integration/{HierarchicalStateMachine, Character, InputSystem}
├── Editor/{Inspectors, Timeline, Preview, ComboGraph, Debugger, Validation}
├── Tests/{Editor, Runtime, Editor/Integration/<Outro>}
├── Samples~/{CombatArena, CombatCharacterHfsm}
└── Documentation~/adr  (ADR-0001 do package: Combo Graph como vista GraphView sobre modelo de edição)
```

---

## 20. Impacto em packages existentes (nada muda sem aprovação)
| Package | Mudança | Condição |
|---|---|---|
| Core | `EdgeQueue<TPayload>` genérico (fila de bordas com token) — **minor 0.4.0** | Só se D5 = Core |
| Core | `GameplayTag` (+ container sem alocação) — minor | Só se D6 = Core |
| Character | Nenhuma obrigatória. Opcional: `Hold/Release` do motor para hitstop e agarrão (minor 0.2.0, aditivo) | Só se D8/D9 = "API no motor" |
| Character | Promover `IExternalForceReceiver` a Stable | Após AC5 |
| Stats, HFSM | Nenhuma | — |

---

## 21. Decisões para aprovação
| # | Decisão | Recomendação | Alternativas |
|---|---|---|---|
| D1 | Modelo de tempo | **Segundos de jogo acumulando Δt fixo + regra de sobreposição de janela**; frames@60 só na autoria (ADR-0013) | Ticks fixos inteiros (muda com o Δt do projeto); frames@60 em runtime (arredondamento por Δt) |
| D2 | Autoria das hitboxes | **Formas nas definições, ancoradas em `HitboxAnchor`s nomeados** (sprite e 3D, preview com scrub; ADR-0016) | Colliders filhos animados pelo Animator (acopla simulação à animação, R8) |
| D3 | Resolução | **Duas fases por bandas (detecção `COMBAT`, resolução `COMBAT+50`)** (ADR-0014) | Resolver na detecção (dependente de ordem); coordenador global singleton |
| D4 | Posse das ações | **`ActionOwner` Combat/External com `RequestAction`/`CancelActionRequest(token)`**, espelhando o Character (ADR-0015) | Só HFSM; só combo runner |
| D5 | Fila de bordas | **Extrair `EdgeQueue<T>` para o Core 0.4.0** (ADR-0011 previu a extração no 2º consumidor); o Character migra no próximo minor sem mudar API | Cópia local no Combat (mais uma duplicação, como `ListenerList`) |
| D6 | Tags (ataque, alvo, ação) | **`GameplayTag` no Core** (ADR-0018): Combat agora, Abilities (M5) e AI (M6) depois; evitar migrar assets de SO entre packages | Tags locais do Combat (migração de assets no M5); strings (frágeis) |
| D7 | Poise | **Recurso do Stats** (regen/atraso prontos), opcional por perfil | Modelo interno do Combat |
| D8 | Hitstop × motor | **Pausar as linhas do tempo de combate + apresentação; knockback no fim do hitstop**; o motor só é suspenso se o `Hold` de D9 for aprovado (aí o hitstop também o usa) | Hitstop sem tocar o motor em nenhum caso; time scale por ator |
| D9 | Agarrão | **Básico no M4** (agarrar, golpear, arremessar atingindo terceiros) **com `Hold/Release` aditivo no Character 0.2.0** para prender personagens (§11) | Agarrão só de `Rigidbody` simples no M4; adiar agarrão para 0.2.0 |
| D10 | Movimento de ataque | **Impulsos por evento via `IExternalForceReceiver`** (avanços, recuos) + `MovementMultiplier` | Curvas de deslocamento (root motion data-driven) — adiado |
| D11 | Times | **Bit de time + máscara de hostis** no perfil | Tags; layers de física por time |
| D12 | Samples | **Combat Arena (Combat + Stats) + Combat Character HFSM (opcional)** | Um sample só exigindo Character + HFSM |
| D13 | Trocas simultâneas | **Os dois acertos valem** (resolução em duas fases); prioridade/clash adiados | Prioridade por ataque já no M4 |
| D14 | Combo Graph Editor | **Próprio modelo + GraphView em `Combat.Editor`, sem extração para o Core**; reavaliar no M6 | Extrair infra comum agora; Graph Toolkit |
| D15 | Projéteis | **Fora do M4** (Abilities) | Hitbox móvel genérica no Combat |

---

## 22. ADRs propostos
| ADR | Título | Status |
|---|---|---|
| [0013](../adr/0013-combat-timing.md) | Tempo de combate: segundos de jogo, Δt fixo e janelas por sobreposição | Proposta |
| [0014](../adr/0014-two-phase-hit-resolution.md) | Resolução de acertos em duas fases (detecção → resolução) | Proposta |
| [0015](../adr/0015-combat-commands-and-ownership.md) | Comandos de combate, prioridade e requisição em duas fases | Proposta |
| [0016](../adr/0016-data-driven-hitboxes.md) | Hitboxes como dados ancorados, consultas sem alocação e anti-tunneling | Proposta |
| [0017](../adr/0017-damage-pipeline-and-outcomes.md) | Pipeline de dano e precedência dos resultados | Proposta |
| [0018](../adr/0018-gameplay-tags-in-core.md) | `GameplayTag` no Core | Proposta (depende de D6) |

---

## 23. Riscos
| Risco | Tratamento |
|---|---|
| R8 timing (combate × animação × Δt) | ADR-0013; Animator nunca dirige; testes em 4 Δt |
| R9 orquestração | HFSM compõe; integrações finas; nenhum package chama outro fora delas |
| R1 graph editor | Gatilhos verificados (não dispararam); duplicação de padrão aceita até o M6 |
| R18 escopo aberto | Lista fechada da §1.1; adiados explícitos |
| Tunneling de hitboxes rápidas | Subamostragem com contagem (ADR-0016) |
| Saturação de consultas (muitos alvos) | Buffer estendido + contagem residual (política do GroundSensor) |
| Hitstop sem pausar o motor (D8) | Medir no sample; fallback `Hold` no Character 0.2.0 |
