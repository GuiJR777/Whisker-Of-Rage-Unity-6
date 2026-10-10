# M3 — Character Controller · Especificação técnica

- **Package:** `com.ramirestechgames.character` · assembly/namespace raiz `RamiresTechGames.Character` · camada L2
- **Dependência hard:** `com.ramirestechgames.core` (≥ 0.3.0: `ExecutionOrder`, `IValidatable`, `[SelectImplementation]`)
- **Opcionais (integration assemblies, ADR-0002):** `com.ramirestechgames.hfsm`, `com.ramirestechgames.stats`,
  `com.unity.inputsystem` (já declarados em `dependency-graph.json`)
- **Status:** **v2 — aprovada com ajustes; implementação autorizada.** D1–D12 aprovadas (ressalvas em D3, D5,
  D9, D11); ADRs 0010–0012 aceitos. Ajustes na **§19, que prevalece sobre as seções anteriores em caso de
  conflito**. M3 só é concluído após a revisão final.
- **Insumos:** Master Prompt §4.3 · [02 — fronteiras](../02-package-boundaries-and-contracts.md#character--ramirestechgamescharacter-l2--m3)
  · [ADR-0003](../adr/0003-execution-order-bands.md) (decisões temporais do M3) ·
  [ADR-0004](../adr/0004-command-buffers.md) (primeiro buffer, capacidade, janela do jump buffer) ·
  [07 — roadmap](../07-roadmap.md) (critérios do M3) · documentação da Unity 6000.6 (§3.1).

---

## 1. Objetivo e escopo
Motor físico 3D genérico, configurado por assets e pelo Inspector, que move **qualquer** ator (Player, Enemy, NPC) a
partir de **comandos**, independentemente da representação visual (sprite 2D em mundo 3D ou modelo 3D).

**O motor executa movimento. Ele não decide** (IA, M6), **não ataca** (Combat, M4), **não anima** (apresentação do
jogo) **e não lê input** (adapters). O futuro Combat aplica knockback e launch pelo contrato `IExternalForceReceiver`,
sem assumir a física do Character.

### 1.1 Obrigatório no M3 · pendente de decisão · adiado
| Obrigatório no M3 | Pendente (§16) | Adiado (fora do M3) |
|---|---|---|
| Motor com Rigidbody dinâmico, aceleração/desaceleração, controle aéreo | Modelo físico (D1) | Degraus com *step-up* dedicado |
| Gravidade própria, pulo por altura/tempo exatos, pulo variável, coyote, jump buffer | Dono das bordas pulo/dash (D3) | Plataformas móveis com herança de velocidade/rotação |
| Pulos aéreos configuráveis (padrão 0) | Pulos aéreos e dash aéreo no M3 (D7) | Agachar, escalar, wall jump, nado, ledge grab |
| Dash por distância/duração com cooldown | Inclinação: modelo de rampa e snapping (D5) | Hitstop local / time scale por ator (M4) |
| Command buffer tipado (estado + borda) com garantias ADR-0004 | Plataformas móveis (D6) | Root motion |
| `IExternalForceReceiver`: impulso, força ao longo do tempo, limpar | Detecção contínua padrão (D8) | IK de pés, alinhamento visual ao chão |
| GroundSensor: chão, inclinação, borda, aterrissagem, snapping | Sample sem Input System (D10) | Rede/determinismo (R14) |
| Facing lógico/físico/visual separados; presenters de sprite e de transform | Escala de eixo para 2.5D (D9) | Física de empurrar outros personagens com regras |
| Integrações HFSM, Stats, Input System | Extração do ring buffer para o Core (D4) | |
| Inspector, gizmos, prévia de trajetória, debugger, validators, sample | | |

---

## 2. Conceitos

| Conceito | Definição |
|---|---|
| **Comando de estado** | Valor contínuo sobrescrito pela fonte: `Move` (Vector2 plano), `JumpHeld`, `FacingOverride`. Ler não consome. |
| **Comando de borda** | Ocorrência enfileirada com sequência e instante: `Jump`, `Dash` (com direção opcional). Um dono consome. |
| **Plano de movimento** | XZ do mundo. `Move.x` → X, `Move.y` → Z. Conversão relativa à câmera é responsabilidade da fonte (adapter). |
| **Canal de locomoção** | Velocidade planar comandada pelo movimento (aceleração/desaceleração do profile). |
| **Canal externo** | Velocidade de forças externas (knockback, launch, impulsos), com decaimento próprio; a locomoção não a anula. |
| **Velocidade vertical** | Única para pulo, gravidade e launch; a gravidade do profile age sobre ela. |
| **GroundInfo** | Resultado do sensor no passo: chão encontrado, normal, ângulo, distância, borda, Rigidbody/velocidade do chão. |
| **Facing lógico** | Direção plana unitária que o ator "encara" (domínio). Física e visual derivam dela. |

---

## 3. Arquitetura física

### 3.1 APIs da Unity 6000.6.5f1 escolhidas (verificadas no Editor instalado e na documentação 6000.6)
| Necessidade | API escolhida | Justificativa | Evitado |
|---|---|---|---|
| Corpo físico | `Rigidbody` dinâmico, `useGravity = false`, rotação congelada (`constraints`) | Interage com o mundo (empurra/é empurrado, colide) sem um solver próprio; forças externas fazem sentido físico. | `CharacterController` (não é Rigidbody, sem forças); Rigidbody cinemático (exige *collide-and-slide* próprio — ver D1). |
| Ler/alterar velocidade | `Rigidbody.linearVelocity` (leitura) e `AddForce(Δv, ForceMode.VelocityChange)` (escrita) | Unity 6 renomeou `velocity` → `linearVelocity` (`velocity` está `[Obsolete]` na 6000.6.5f1). A doc recomenda não **atribuir** velocidade a cada passo; o motor aplica **variações limitadas por aceleração** acumuladas pelo solver ("acumula a variação e aplica no próximo passo de simulação"). | Atribuir `linearVelocity` todo passo; `MovePosition` (é para cinemático). |
| Gravidade | `AddForce(g, ForceMode.Acceleration)` com `g` do profile | `Acceleration` ignora massa: Δv = g·Δt (fórmula da doc de `AddForce`). Gravidade por personagem permite pulo exato e queda mais rápida, sem mexer em `Physics.gravity` global. | `useGravity` (gravidade global única). |
| Pulo, dash, impulsos | `ForceMode.VelocityChange` (Δv = valor, ignora massa) | Mudança imediata de velocidade é o caso indicado pela doc (exemplo: pulo). Ignorar a massa mantém o mesmo profile para atores de massas diferentes. Impulsos externos podem optar por `Impulse` (usa massa) — ver §6. | `ForceMode.Force` para pulo (depende de Δt e massa). |
| Detecção de chão | `PhysicsScene.SphereCast(..., RaycastHit[] results, ...)` + `PhysicsScene.Raycast(..., out RaycastHit)` | Versão com buffer, sem lixo (equivale a `Physics.SphereCastNonAlloc`); `PhysicsScene` respeita cenas com física local (testes, multi-cena). `QueryTriggerInteraction.Ignore`. | `*All` (aloca); `OnCollisionStay` como fonte do chão (roda após a física, fora das bandas — ADR-0003). |
| Sobreposição/limite | `PhysicsScene.OverlapCapsule(..., Collider[] ...)`, `Physics.ComputePenetration` (validação/diagnóstico) | Detectar início dentro de geometria no debugger e em testes; a doc indica `ComputePenetration` para depenetração de character controllers. | Depenetração manual no runtime do M3 (a física dinâmica já resolve). |
| Atrito | `PhysicsMaterial` (Unity 6; `PhysicMaterial` está obsoleto) com atrito 0 e `frictionCombine = Minimum` no collider do personagem | O motor controla a desaceleração; atrito do PhysX faria o personagem grudar em paredes e frear de forma diferente por material. | Atrito padrão. |
| Suavização visual | `Rigidbody.interpolation = Interpolate` | A doc recomenda interpolação para o personagem principal e objetos seguidos pela câmera; física em passo fixo × render variável causa *jitter*. Transform passa a ser controlado pela física (mudanças diretas exigem `Physics.SyncTransforms`). | Mover o Transform do personagem; extrapolação. |
| Colisão em alta velocidade | `collisionDetectionMode` configurável; padrão a decidir (D8) | Dash e launch podem atravessar paredes finas em `Discrete`. | — |
| Momento da física | `Physics.simulationMode = FixedUpdate` (padrão do projeto) | A doc 6000.6 descreve o passo de física logo após `FixedUpdate`; o motor escreve forças no `FixedUpdate` e o solver as aplica no mesmo passo. | Modos `Update`/`Script` (fora do contrato ADR-0003). |

Valores do host verificados: `Time.fixedDeltaTime = 0,02`, `maximumDeltaTime = 0,333`, `Physics.gravity = (0, -9,81, 0)`,
`defaultContactOffset = 0,01`, Input System 1.20.0, *Active Input Handling* = Input System.

### 3.2 Separação de responsabilidades
| Peça | Tipo | Responsabilidade | Testável sem física |
|---|---|---|---|
| `MovementProfileDefinition` | SO | Parâmetros de movimento (§4); valores derivados (g, v0) calculados uma vez. | Sim |
| `CharacterCommandBuffer` | MB | Comandos de estado e bordas (§5). Sem lógica de movimento. | Sim (núcleo puro `CommandEdgeQueue`) |
| `MovementModel` | Pure | Domínio: canais de velocidade, pulo, coyote, buffer, dash, quedas, eventos. Entrada: profile, comandos, `GroundInfo`, Δt, velocidade medida. Saída: Δv a aplicar e eventos. | **Sim** |
| `GroundSensor` | Pure (consulta `PhysicsScene`) | Casts e classificação de chão (§7). Não move nada. | Parcial (EditMode com colliders) |
| `FacingModel` / `FacingController` | Pure + MB | Facing lógico (§8). | Sim |
| `CharacterMotor` | MB (banda `CHARACTER`) | Integração física: lê o Rigidbody, chama sensor e modelo, aplica forças, entrega eventos, implementa `IExternalForceReceiver`. | Não (PlayMode) |
| `SpriteFacingPresenter`, `TransformFacingPresenter` | MB (banda `PRESENTATION`) | Facing visual (flip de sprite, rotação de um transform visual). | Parcial |

### 3.3 Fluxo de um passo fixo
```
FixedUpdate (banda CHARACTER, 0..N vezes por frame)
 1. measured = rigidbody.linearVelocity
 2. ground = GroundSensor.Sense(pose, profile)                       // casts com buffer pré-alocado
 3. reconciliar canais com measured (colisões bloquearam o quê?)     // §6.3
 4. MovementModel.Step(dt, commands, ground)
      timers (coyote, cooldowns) → bordas (pulo/dash, conforme o dono D3) → locomoção → externo → vertical
 5. Δv = alvo − measured;  rigidbody.AddForce(Δv, VelocityChange);  rigidbody.AddForce(g·escala, Acceleration)
 6. FacingController atualiza o facing lógico (banda CHARACTER + 10)
 7. eventos do passo entregues (FIFO) depois do passo 6
→ passo de física (PhysX integra velocidade e depois posição; resolve colisões)
Update/LateUpdate (banda PRESENTATION): presenters leem o facing lógico; interpolação suaviza o Transform.
```

---

## 4. `MovementProfileDefinition` (ScriptableObject)
Sem valores de jogo no package: os padrões abaixo são neutros (escala de humano em metros) e servem ao sample.

| Grupo | Campo | Unidade | Padrão | Regra |
|---|---|---|---|---|
| Solo | `MaxSpeed` | m/s | 6 | > 0 |
| | `AccelerationTime` | s (0 → MaxSpeed) | 0,1 | ≥ 0 (0 = instantâneo) |
| | `DecelerationTime` | s (MaxSpeed → 0) | 0,08 | ≥ 0 |
| | `TurnAccelerationMultiplier` | × | 2 | ≥ 1; aplicado quando o comando aponta contra a velocidade |
| Ar | `AirAccelerationMultiplier` | × aceleração do solo | 0,6 | [0, 1] |
| | `AirDecelerationMultiplier` | × desaceleração | 0,3 | [0, 1] |
| | `AirMaxSpeedMultiplier` | × MaxSpeed | 1 | > 0 |
| Pulo | `JumpHeight` | m | 2 | > 0 |
| | `TimeToApex` | s | 0,4 | > 0 |
| | `MinJumpHeight` | m | 0,6 | (0, JumpHeight] |
| | `FallGravityMultiplier` | × | 1,6 | ≥ 1 |
| | `MaxFallSpeed` | m/s | 20 | > 0 |
| | `CoyoteTime` | s | 0,1 | ≥ 0; < tempo de ar |
| | `JumpBufferTime` | s | 0,12 | ≥ 0 |
| | `MaxAirJumps` | quantidade | 0 | ≥ 0 (D7) |
| | `AirJumpHeight` | m | = JumpHeight | > 0 |
| Dash | `DashDistance` | m | 3 | > 0 |
| | `DashDuration` | s | 0,18 | > 0 |
| | `DashCooldown` | s | 0,4 | ≥ 0 |
| | `DashBufferTime` | s | 0,12 | ≥ 0; janela da borda de dash (dash pedido durante o cooldown ainda vale se o cooldown acabar dentro dela) |
| | `DashGravityScale` | × | 0 | [0, 1] |
| | `DashExitSpeed` | m/s | = MaxSpeed | ≥ 0; velocidade planar ao terminar |
| | `MaxAirDashes` | quantidade | 1 | ≥ 0 (D7) |
| | `DashDirection` | enum | `MoveOrFacing` | `MoveOrFacing`, `Facing`, `Command` |
| Inclinação | `MaxSlopeAngle` | graus | 45 | [0, 89] |
| | `SteepSlopeSlideAcceleration` | m/s² | = g·sin(θ) | derivado; sem campo (D5) |
| | `GroundSnapDistance` | m | 0,3 | ≥ 0 |
| | `MaxSnapSpeed` | m/s | = MaxSpeed | > 0 (acima disso não "gruda" no chão) |
| Externo | `ExternalGroundDeceleration` | m/s² | 30 | ≥ 0; decaimento do canal externo no chão |
| | `ExternalAirDeceleration` | m/s² | 4 | ≥ 0 |
| | `ControlDuringExternal` | × aceleração | 0,25 | [0, 1]; controle enquanto o canal externo > limiar |
| Facing | `FacingMode` | enum | `MovementDirection` | `MovementDirection`, `HorizontalOnly` (2.5D), `Command` |
| | `FacingDeadZone` | 0..1 | 0,1 | magnitude mínima do comando para mudar o facing |
| Plano (2.5D) | `PlanarAxisScale` | (x, z) | (1, 1) | (0, ∞); ex.: profundidade mais lenta (D9) |

Valores **derivados** (somente leitura, exibidos no Inspector e usados em runtime): `g_up`, `g_down`, `v0`,
`v_min` (pulo variável), tempo de ar, distância horizontal máxima do pulo, velocidade de dash.

O que **não** fica no profile (é da cena/prefab): `LayerMask` de chão, geometria do collider, massa, interpolação.
Esses ficam em `CharacterMotor`/`GroundSensor` (componente), validados no Editor.

---

## 5. Command Buffer (`CharacterCommandBuffer`) — fecha as decisões do M3 no ADR-0004

### 5.1 Comandos
| Comando | Tipo | Escrita | Leitura |
|---|---|---|---|
| `Move` | estado, `Vector2` (magnitude ≤ 1, clamp na escrita) | `SetMove(v)` | `Move` |
| `JumpHeld` | estado, `bool` | `SetJumpHeld(b)` | `JumpHeld` (pulo variável) |
| `FacingOverride` | estado, `Vector2` + `HasFacingOverride` | `SetFacingOverride(v)` / `ClearFacingOverride()` | `FacingMode.Command` |
| `Jump` | borda | `EnqueueJump()` | `HasPending` / `TryPeek` / `TryConsume` |
| `Dash` | borda com payload `Vector2 direction` (zero = usar regra do profile) | `EnqueueDash(dir)` | idem |

`CharacterCommands` (struct `readonly`) é um retrato dos comandos de estado para o modelo.

### 5.2 Garantias (ADR-0004, uma a uma)
| Garantia | Mecanismo | Teste |
|---|---|---|
| Sem perda entre `Update` e `FixedUpdate` | Bordas ficam *latched* em um ring buffer até consumo ou expiração; frame sem passo fixo não as descarta. | Frame sem `FixedUpdate` → consumida no próximo passo. |
| Sem duplicação | `TryConsume` remove a ocorrência; o 2º `FixedUpdate` do frame não a vê. | 3 passos fixos num frame → 1 pulo. |
| Sem execução tardia | Idade = `agora − instante`; o **consumidor** passa a janela (`JumpBufferTime`, `DashBufferTime`). Bordas mais velhas são descartadas e contadas como expiradas. | Borda de 0,2 s com janela de 0,12 s → não pula. |
| Um dono por tipo | `JumpOwner`/`DashOwner` declarados (D3): `Motor` (padrão) ou `External` (estados da HFSM). Executores externos espiam e consomem por **token** (claims da HFSM). | Dois consumidores → só o dono consome. |
| Troca de controle limpa | `Clear()` zera estado e bordas; `SetSource(object owner)` limpa quando o dono muda. Fonte desligada volta os estados ao neutro. | Troca Player→AI → sem pulo residual. |
| Sem alocação, overflow contado | Ring buffer de capacidade fixa por tipo (padrão 4, Inspector 1..16). Cheio: descarta a **mais antiga**, incrementa `DroppedCount`. | 6 bordas em capacidade 4 → 2 descartadas, ordem mantida. |

### 5.3 Relógio
O buffer carimba bordas com o **tempo de jogo** (`Time.timeAsDouble` no componente; dentro de `FixedUpdate` a Unity
devolve o tempo fixo). O domínio nunca lê `Time`: recebe `now`. Idade negativa (carimbo de `Update` mais novo que o
tempo fixo do passo seguinte) é tratada como 0. Com `timeScale = 0` o relógio para e nenhuma borda expira.

### 5.4 Onde fica o código genérico (D4)
O ring buffer tipado (`CommandEdgeQueue<TPayload>`) nasce **interno ao Character**. Pela regra de ≥ 2 consumidores, a
extração para o Core é avaliada no M4, quando o `CombatCommandBuffer` precisar da mesma estrutura (com ADR).

---

## 6. Velocidade, forças externas e `IExternalForceReceiver`

### 6.1 Locomoção (canal planar)
```
alvo = clamp(Move, 1) · MaxSpeed · PlanarAxisScale · multiplicadores (Stats)          [m/s]
a    = MaxSpeed / AccelerationTime   (ou ∞ se 0)        d = MaxSpeed / DecelerationTime
se Move ≈ 0:            v_loc ← MoveTowards(v_loc, 0, d·dt)
se Move · v_loc < 0:    v_loc ← MoveTowards(v_loc, alvo, a·TurnAccelerationMultiplier·dt)
senão:                  v_loc ← MoveTowards(v_loc, alvo, a·dt)
no ar: a·AirAccelerationMultiplier, d·AirDecelerationMultiplier, alvo limitado por MaxSpeed·AirMaxSpeedMultiplier
com canal externo ativo: aceleração × ControlDuringExternal
```
Tempo até a velocidade máxima = `AccelerationTime` (exato em passos discretos: erro ≤ 1 passo). Teste PlayMode.

### 6.2 Canal externo (o que impede o Combat de ser "anulado")
- `v_ext` (3D) acumula impulsos e forças externas. Ele **não** passa pela aceleração de locomoção e **não** é zerado
  pelo motor no passo seguinte; decai com `ExternalGroundDeceleration`/`ExternalAirDeceleration` (MoveTowards até 0).
- Componente vertical de impulsos (launch) vai para a velocidade vertical (sujeita à gravidade do profile).
- Saída planar do passo: `v_planar = v_loc + v_ext_planar`. Assim um knockback de 10 m/s com decaimento de 30 m/s²
  percorre ~1,67 m no chão, **independentemente** de o jogador segurar o direcional contra.
- Enquanto `|v_ext_planar| > ExternalControlThreshold` (0,5 m/s, constante), a locomoção acelera com
  `ControlDuringExternal`: o jogador influencia mas não cancela.
- Dash ativo é encerrado por impulso externo acima do limiar (configurável no componente: `ExternalForceEndsDash`).

### 6.3 Reconciliação com colisões (sem callbacks)
Depois do passo de física, a velocidade medida pode ser menor que a aplicada (parede, outro corpo). No passo
seguinte, antes do modelo:
```
perda = saída_planar_anterior − medida_planar
se |perda| > ε:  remover a perda primeiro de v_ext_planar (projeção na direção da perda, até zerar), o resto de v_loc
vertical: se medida_y difere da saída_y além de ε (bateu no teto/aterrissou), adotar medida_y
```
Isso evita "empurrar a parede" acumulando velocidade invisível, sem depender de `OnCollisionStay` (que roda fora das
bandas, ADR-0003).

### 6.4 Contrato `IExternalForceReceiver` (implementado por `CharacterMotor`)
```csharp
public interface IExternalForceReceiver
{
    void AddImpulse(Vector3 velocityChange, ExternalForceMode mode = ExternalForceMode.VelocityChange); // knockback, launch
    ExternalForceHandle AddForceOverTime(Vector3 acceleration, float duration);                         // vento, esteira, sucção
    bool RemoveForce(ExternalForceHandle handle);
    void ClearExternalForces();                                                                          // zera v_ext e forças ativas
    Vector3 ExternalVelocity { get; }
}
public enum ExternalForceMode { VelocityChange, Impulse }   // Impulse divide pela massa do Rigidbody
```
- Chamado de qualquer loop: impulsos ficam pendentes e entram no **próximo** passo fixo (nunca perdidos, nunca
  aplicados duas vezes) — mesma semântica de borda do ADR-0004.
- Ao receber impulso com componente vertical > 0 no chão, o personagem deixa o chão (o snapping é suspenso por
  `GroundSnapDistance / v_y` segundos — impede que o snapping "cole" o launch).
- Forças ao longo do tempo: até 8 simultâneas (capacidade fixa, sem GC); excedente rejeitado com `false` e aviso
  único no console.
- O Character **não** decide hitstun, invencibilidade ou direção do golpe: isso é do Combat (M4), que só chama o
  contrato. `Combat.Integration.Character` (no M4) traduz `HitOutcome` em impulsos.

---

## 7. Pulo preciso

### 7.1 Fórmulas (contínuas)
Entrada: altura `h` (`JumpHeight`) e tempo até o ápice `tₐ` (`TimeToApex`).
```
g_up   = 2h / tₐ²                      gravidade na subida
v0     = 2h / tₐ = g_up · tₐ          velocidade inicial
g_down = g_up · FallGravityMultiplier gravidade na descida (v_y ≤ 0)
t_fall = √(2h / g_down)               queda do ápice ao chão de partida
t_air  = tₐ + t_fall                   tempo total de ar
x_max  = MaxSpeed · t_air              alcance horizontal à velocidade máxima
```
Exemplo (padrões): h = 2, tₐ = 0,4 → g_up = 25 m/s², v0 = 10 m/s, g_down = 40 m/s², t_fall = 0,316 s, t_air = 0,716 s.

### 7.2 Correção discreta (passo fixo)
O PhysX integra velocidade antes da posição (Euler semi-implícito); a gravidade do passo do pulo já é aplicada no
mesmo passo. Altura após N passos: `y_N = N·v0·Δt − g·Δt²·N(N+1)/2`. O ápice discreto fica `≈ v0²/2g − v0·Δt/2`
(com h = 2 e Δt = 0,02: −0,10 m, 5%). Para cumprir a altura no passo fixo usado:
```
v0_Δt = g·Δt/2 + √((g·Δt/2)² + 2·g·h)
```
O valor é recalculado se `Δt` mudar (o motor compara o `Δt` do passo com o usado no cache). A mesma correção vale para
`v_min` e para pulos aéreos.

### 7.3 Pulo variável (early release) — corte de velocidade
```
v_min = √(2 · g_up · MinJumpHeight)            (com a correção discreta acima)
ao soltar JumpHeld enquanto subindo:  v_y ← min(v_y, v_min)
```
Altura final ao soltar na altura `y_r` com velocidade `v_r`: `H = y_r + min(v_r, v_min)² / 2g_up`.
- Soltar na decolagem → `H = MinJumpHeight` (exato).
- Segurar até o ápice → `H = JumpHeight`.
- Qualquer instante entre os dois → `H ∈ [MinJumpHeight, JumpHeight]`, monotônico no tempo de pressionamento.

Alternativa considerada: multiplicador de gravidade ao soltar (comum, mas a altura mínima não é garantida) — D2.

### 7.4 Coyote time, jump buffer, pulos aéreos
```
pode_pular = chão  ou  (tempo_desde_saída_sem_pular ≤ CoyoteTime)  ou  (pulos_aéreos_usados < MaxAirJumps)
borda Jump com idade ≤ JumpBufferTime e pode_pular → pula e consome;  idade > JumpBufferTime → descarta (expirada)
```
- Coyote não vale se o personagem saiu do chão **pulando** ou por impulso externo vertical.
- Pulo de coyote usa `v0` do chão (não o do pulo aéreo) e não gasta pulo aéreo.
- Aterrissar zera pulos aéreos e dashes aéreos usados.
- Evento `Jumped` informa: aéreo?, coyote?, vindo do buffer?, `v0` aplicado.

### 7.5 Movimento aéreo e queda
- Locomoção no ar conforme §6.1 (multiplicadores do profile).
- Gravidade: `g_up` com `v_y > 0`, `g_down` com `v_y ≤ 0`; `DashGravityScale` durante o dash.
- `v_y` limitado a `−MaxFallSpeed`.
- Teto: se a velocidade medida subindo cair a ~0 (bateu), a reconciliação adota a medida e a descida começa.

### 7.6 Tolerâncias e testes PlayMode de altura real
Medição: altura da base do collider no ápice − altura da base no chão de partida, amostrada por passo fixo.
| Caso | Esperado | Tolerância |
|---|---|---|
| Pulo segurado, Δt = 0,02 / 0,0166 / 0,01 | `JumpHeight` | `max(0,02 m; 1% de h)` (amostragem do ápice ≤ g·Δt²/8 ≈ 1 mm; contact offset 0,01 m) |
| Soltar na decolagem | `MinJumpHeight` | idem |
| Tempo até o ápice | `TimeToApex` | ± 1 passo fixo |
| Tempo total de ar (chão plano) | `t_air` | ± 1 passo fixo |
| `timeScale = 0,5` | mesma altura | idem (trajetória em tempo de jogo não muda) |
| Dash em chão plano | `DashDistance` | ± `DashSpeed·Δt` + 0,02 m |
| Coyote | pula até `CoyoteTime` após sair da borda; não pula depois | ± 1 passo |
| Jump buffer | pressionar até `JumpBufferTime` antes de aterrissar pula ao tocar; antes disso, não | ± 1 passo |

---

## 8. GroundSensor

### 8.1 Consulta
- **Sphere cast** para baixo a partir do centro da esfera inferior da cápsula, elevado por `skin` (0,05 m), raio =
  raio da cápsula × 0,95, distância = `skin + GroundProbeDistance` (≥ `GroundSnapDistance`), `LayerMask` do
  componente, `QueryTriggerInteraction.Ignore`, via `PhysicsScene.SphereCast(..., RaycastHit[] buffer, ...)`
  (buffer de 8 pré-alocado). Ignora os próprios colliders (comparação com o array de colliders do personagem, sem
  `GetComponent` no passo).
- Escolhe o acerto mais próximo cuja normal seja caminhável; se nenhum, o mais próximo (inclinação íngreme).
- **Raio de confirmação** (`PhysicsScene.Raycast`) a partir do ponto de contato para obter a normal **da superfície**:
  o sphere cast em uma quina devolve a normal da aresta (arredondada), o raio devolve a da face. Se o raio não
  encontra face sob o centro da cápsula → `IsOnEdge = true`.
- Hits com `distance == 0` (início sobreposto) são tratados como chão com a normal do raio de confirmação.

### 8.2 Classificação
| Situação | Regra | Efeito no motor |
|---|---|---|
| Chão caminhável | ângulo(normal, up) ≤ `MaxSlopeAngle` e distância ≤ `skin + ε` | `IsGrounded`; locomoção projetada no plano do chão; sem gravidade ao longo da rampa |
| Rampa íngreme | ângulo > `MaxSlopeAngle` | não é chão: desliza (gravidade projetada), não pode pular (D5) |
| Borda | sphere cast acerta, raio central não | conta como chão (permite andar até a borda); expõe `IsOnEdge` para estados/animação |
| Aterrissagem | estava no ar, encontra chão caminhável e `v_y ≤ v_chão_y + 0,1` | `Landed` (uma vez), zera contadores aéreos |
| Saída do chão | sem chão caminhável no passo | inicia coyote (se não pulou) |
| Snapping | estava no chão, não pulou, sem impulso vertical, hit dentro de `GroundSnapDistance` e velocidade planar ≤ `MaxSnapSpeed` | adiciona componente descendente para fechar o vão: `v_y = −gap/Δt` (limitado) — evita "decolar" no topo de rampas |

### 8.3 Escopo de degraus e plataformas móveis
- **Degraus:** sem sistema de *step-up* no M3. A base arredondada da cápsula + snapping sobe ressaltos até
  ≈ `raio·(1 − cos(MaxSlopeAngle))` (≈ 0,09 m com raio 0,3 e 45°). O gizmo mostra esse limite. Degraus maiores
  são geometria de rampa ou recurso futuro (adiado).
- **Plataformas móveis:** `GroundInfo` expõe `GroundRigidbody` e `GroundPointVelocity`
  (`Rigidbody.GetPointVelocity`) para quem precisar; o motor **não** herda velocidade no M3 (D6, recomendação:
  adiar até haver caso de uso; o seam já existe).

---

## 9. Facing — lógico, físico e visual
| Camada | Onde | O que é | Quem muda |
|---|---|---|---|
| **Lógico** | `FacingModel` (puro), exposto por `FacingController` | Direção plana unitária + `Sign` (−1/+1 em X, útil em 2.5D) | Regra do profile (`MovementDirection`, `HorizontalOnly`, `Command`); `LockFacing()` devolve handle (ataques no M4 travam o facing sem conhecer o motor) |
| **Físico** | `CharacterMotor` (opcional `RotateBodyToFacing`) | Rotação Y do Rigidbody via `MoveRotation`, com velocidade angular máxima | Só para colliders assimétricos; padrão desligado (cápsula é simétrica) |
| **Visual** | `SpriteFacingPresenter` / `TransformFacingPresenter` (banda `PRESENTATION`) | Flip de `SpriteRenderer.flipX` (ou escala X) / rotação suavizada de um transform filho | Leem o facing lógico; nunca alteram física nem comandos |

O motor e o modelo de movimento não conhecem sprite nem rig: trocar a representação é trocar o filho visual e o
presenter. O dash usa o facing lógico como direção padrão.

---

## 10. Integrações opcionais (ADR-0002)
| Assembly | Requer | Fornece | Sem ciclo porque |
|---|---|---|---|
| `RamiresTechGames.Character.Integration.HierarchicalStateMachine` | HFSM ≥ 0.1.0 | Comportamentos: `LocomotionBehaviour`, `AirborneBehaviour`, `JumpBehaviour` (consome a borda pelo token da claim e chama `motor.Jump()`), `DashBehaviour`. Condições: `IsGrounded`, `IsFalling`, `IsOnEdge`, `HasMoveCommand`, `HasJumpCommand` (espia + `Claim`), `HasDashCommand`, `CanDash`, `JustLanded`. Definição de exemplo "Locomotion" (sub-máquina reutilizável). | Character (L2) depende de HFSM (L1); HFSM não conhece Character. |
| `RamiresTechGames.Character.Integration.Stats` | Stats ≥ 0.1.0 | `StatMovementModifierSource`: implementa `IMovementModifierSource` (extension point do Character) lendo stats escolhidas no Inspector (velocidade, aceleração, altura de pulo) com cache por evento `StatChanged`. | Character (L2) depende de Stats (L1). |
| `RamiresTechGames.Character.Integration.InputSystem` | Input System ≥ 1.0 | `PlayerInputCharacterSource` (banda `COMMAND_SOURCES`, `Update`): `InputActionReference` para Move/Jump/Dash/Facing; direção relativa à câmera opcional; bordas por `WasPressedThisFrame()`/`performed` **só em `Update`**; `Clear()` ao desabilitar. | Pacote externo; nada do framework depende dele. |

**Estados compartilháveis:** os comportamentos de locomoção leem só o command buffer e o motor do próprio ator
(`StateContext.Get<CharacterMotor>()`). Player e NPC usam a mesma definição de HFSM; muda apenas a fonte de comandos
(Input System × BT/script).

**Extension point do Character:** `IMovementModifierSource` (`[SerializeReference]` ou componente) com
`MovementModifiers Get()` (multiplicadores de velocidade, aceleração, altura de pulo; neutro = 1). Altura modificada
segue D11 (proposta: mantém `TimeToApex` e recalcula `g` e `v0`, para o ritmo do pulo não mudar).

---

## 11. Execução, tempo e desempenho (fecha as decisões do M3 no ADR-0003)
| Decisão | Proposta |
|---|---|
| Valor do passo fixo | O package **não** altera `Time.fixedDeltaTime` (projeto decide; host = 0,02). Fórmulas compensam Δt (§7.2) e os testes rodam com 0,02/0,0166/0,01. Validator avisa acima de 0,0334 (precisão do pulo). |
| Loop do motor | `FixedUpdate`, banda `CHARACTER` (−500). Fontes em `Update` (`COMMAND_SOURCES`), HFSM em `Update` (`STATE_MACHINE`). |
| Ordem dos comandos | Borda escrita no `Update` do frame N → consumida no 1º `FixedUpdate` do frame N+1 (latência máx. 1 frame + 1 passo, ADR-0003). Estados (Move) usam o último valor. |
| Aplicação de forças | Todas no `FixedUpdate` do motor, como `AddForce` (acumuladas e aplicadas no passo de física imediatamente seguinte). Impulsos externos chamados em `Update` ficam pendentes até o próximo passo fixo. |
| Interpolação visual | `Rigidbody.interpolation = Interpolate` no prefab do personagem (validator avisa se `None` em atores marcados como "câmera segue"); presenters rodam em `LateUpdate`. `Teleport(position)` zera canais, ajusta `Rigidbody.position` e chama `Physics.SyncTransforms`. |
| Coyote e jump buffer | Em **segundos** de tempo de jogo, medidos no relógio do motor (soma dos Δt fixos). Erro ≤ 1 passo, coberto pelas tolerâncias. |
| `timeScale` | Escala o número de passos fixos por segundo real; Δt fixo constante → trajetórias e alturas idênticas em tempo de jogo. `timeScale = 0`: nenhum passo, bordas não expiram. Hitstop local por ator fica para o M4 (adiado). |
| Eventos | `Jumped`, `Landed`, `GroundedChanged`, `DashStarted`, `DashEnded`, `ExternalImpulseApplied` — `readonly struct`, enfileirados no passo e entregues em ordem no fim do `FixedUpdate` do motor; listeners não alteram o passo corrente. |
| Zero GC em regime | Buffers de `RaycastHit`/`Collider` pré-alocados; ring buffers de capacidade fixa; sem LINQ/closures/boxing; `ListenerList` local (mesma dívida técnica do Stats/HFSM, não extraída). Teste com 50 personagens em `FixedUpdate`. |

---

## 12. Ferramentas de Editor
| Ferramenta | Conteúdo |
|---|---|
| Inspector do `MovementProfileDefinition` | Campos agrupados; valores derivados (g_up, g_down, v0 ajustado ao Δt atual, v_min, t_air, alcance); **prévia da trajetória** (curva y × x do pulo segurado e do mínimo, à velocidade máxima, com marcação do ápice e do coyote) |
| Inspector do `CharacterMotor` | Referências, validação inline, em Play Mode: velocidade por canal (locomoção, externo, vertical), estado de chão, timers (coyote, buffer, cooldown de dash), contadores aéreos |
| Gizmos | Sphere cast e acerto do sensor, normal da superfície × normal do cast, limite de degrau, faixa de snapping, vetores de velocidade por canal, arco previsto do próximo pulo a partir da posição atual (selecionado) |
| Character Debugger (janela) | Lista de personagens ativos; gráficos curtos (2 s) de velocidade e altura; bordas pendentes, expiradas e descartadas do buffer; últimos 20 eventos; botões de teste (pular, dash, impulso) em Play Mode |
| Validators | Profile: limites da §4, `MinJumpHeight ≤ JumpHeight`, coyote/buffer < tempo de ar, dash coerente. Componente: Rigidbody dinâmico, `useGravity` desligado (o motor também desliga no `Awake`), rotação congelada, `CapsuleCollider` presente, material sem atrito, `LayerMask` de chão não vazio e sem a camada do próprio ator, interpolação, passo fixo alto |
| Menus | `Assets/Create/RamiresTech Games/Character/Movement Profile`; `Tools/RamiresTech Games/Character/Debugger` |

---

## 13. Sample — "Movement Playground"
- **Cena:** chão, rampa caminhável (30°), rampa íngreme (60°), plataforma elevada com borda (coyote), parede (dash
  contra parede), placas de **knockback** e **launch** (componente do sample chama `IExternalForceReceiver`), e um
  painel com os valores derivados do profile.
- **Um prefab, duas representações:** prefab base `Character` (Rigidbody, cápsula, motor, buffer, facing) e duas
  variantes: `Character_Sprite` (filho com `SpriteRenderer` em billboard + `SpriteFacingPresenter`) e
  `Character_Model` (filho com malha simples e "nariz" + `TransformFacingPresenter`). Nenhuma diferença no motor.
- **Duas fontes de comando:** Player (sprite) por **teclado via eventos IMGUI** (funciona com qualquer *Active Input
  Handling*; o host usa só Input System, onde a classe `Input` legada lança exceção) e NPC (modelo 3D) por
  `ScriptedCharacterSource` (patrulha, pula a cada N s, dash periódico).
- **Sample opcional "Input System Player"**: só compila com o Input System instalado (asmdef com `defineConstraints`),
  mostra o `PlayerInputCharacterSource` (D10).
- Demonstra: aceleração/desaceleração, pulo segurado × curto, coyote na borda, jump buffer, dash no chão e no ar,
  knockback que o direcional não cancela, launch, rampas, facing de sprite e de modelo.

---

## 14. API pública (proposta)
| Tipo | Categoria | Estabilidade inicial |
|---|---|---|
| `CharacterPackageInfo` | static | Stable |
| `MovementProfileDefinition` (+ enums `FacingMode`, `DashDirectionMode`) | SO | Experimental |
| `CharacterCommandBuffer`, `CharacterCommands`, `CommandEdge`, `CommandOwner` (enum `Motor`/`External`) | MB / struct | Experimental |
| `CharacterMotor` (`Velocity`, `LocomotionVelocity`, `ExternalVelocity`, `Ground`, `IsGrounded`, `IsDashing`, `Jump()`, `Dash(dir)`, `CanJump`, `CanDash`, `Teleport(pos)`, eventos) | MB | Experimental |
| `IExternalForceReceiver`, `ExternalForceMode`, `ExternalForceHandle` | interface / tipos | Experimental até o Combat (M4) validar o uso (§19.3) |
| `GroundInfo` | readonly struct | Experimental |
| `FacingController` (`Facing`, `Sign`, `LockFacing()` → `FacingLockHandle`, `Unlock`) | MB | Experimental |
| `SpriteFacingPresenter`, `TransformFacingPresenter` | MB | Experimental |
| `IMovementModifierSource`, `MovementModifiers` | extension point | Experimental |
| `JumpedArgs`, `LandedArgs`, `GroundedChangedArgs`, `DashStartedArgs`, `DashEndedArgs`, `ExternalImpulseArgs` | readonly struct | Experimental |
| `MovementModel`, `GroundSensor`, `FacingModel`, `CommandEdgeQueue<T>` | — | Internal (testados via `InternalsVisibleTo`) |

---

## 15. Qualidade

### 15.1 Testes
**EditMode (domínio puro):** aceleração/desaceleração/virada em N passos; ar × chão; fórmulas do pulo (g, v0, v_min,
correção discreta para vários Δt); corte de early release (altura final simulada por integração numérica);
coyote; jump buffer (dentro/fora da janela); pulos e dashes aéreos; dash (distância, cooldown, gravidade, saída);
canal externo (decaimento, controle reduzido, não cancelado pela locomoção, impulso pendente aplicado uma vez);
reconciliação com perda; facing (modos, dead zone, lock); command buffer (6 garantias do ADR-0004); validators;
GroundSensor com colliders reais em EditMode (plano, rampa, quina/borda, início sobreposto, camada ignorada).

**PlayMode (física real):** altura do pulo e do pulo mínimo nos três Δt (§7.6); tempo de ápice e de ar; distância do
dash; aterrissagem e evento `Landed` uma vez; coyote e buffer na borda real; rampa ≤ máx. sobe, > máx. desliza;
snapping no topo de rampa (não decola); parede encerra dash e não acumula velocidade; knockback percorre a distância
esperada mesmo com direcional contrário; launch sai do chão apesar do snapping; `timeScale` 0,5; mesmo prefab com as
duas fontes; presenters de sprite e de modelo; zero GC com 50 personagens.

**Integrações:** HFSM (estados de locomoção com claims; Player e NPC com a mesma definição), Stats (multiplicador de
velocidade aplicado e atualizado por evento), Input System (bordas em `Update` não duplicam com vários passos fixos).

### 15.2 Critérios de aceite
| # | Critério |
|---|---|
| AC1 | Altura real do pulo dentro da tolerância (§7.6) em 3 valores de Δt e com `timeScale` 0,5. |
| AC2 | Mesmo prefab controlado por Player e por script de comandos, sem alteração de código. |
| AC3 | Funciona com sprite e com modelo 3D (apenas o filho visual/presenter muda). |
| AC4 | Command buffer cumpre as 6 garantias do ADR-0004 (testes dedicados). |
| AC5 | Knockback/launch via `IExternalForceReceiver` não são anulados no passo seguinte; o motor nunca é controlado pelo emissor. |
| AC6 | Rampas, bordas, coyote, buffer e snapping conforme §8. |
| AC7 | Zero GC em regime (50 personagens). |
| AC8 | Package isolado (QG12) sem HFSM, Stats, Combat, AI ou Input System; integrações compilam só quando o outro package existe. |
| AC9 | Caso comum montado só pelo Inspector (QG11): profile + prefab + fonte de comando. |

### 15.3 Quality Gates
| QG | Como será verificado |
|---|---|
| QG1 | Compila sem erros/warnings no host (CLI `recompile`). |
| QG2 | Sem dependência de jogo: `check_dependencies --package character`. |
| QG3 | EditMode + PlayMode verdes, duas rodadas consecutivas no host. |
| QG4 | Sample "Movement Playground" executado em Play Mode (evidência registrada). |
| QG5 | README, CONTRACTS, ARCHITECTURE, TESTING, ROADMAP, CHANGELOG, ADRs. |
| QG6 | API pública da §14 listada em CONTRACTS com estabilidade. |
| QG7 | Header/Tooltip, menus padronizados, ferramentas da §12. |
| QG8 | Validators da §12 com testes. |
| QG9 | Debugger e gizmos verificados em Play Mode. |
| QG10 | `check_dependencies.py` verde (integrações com `defineConstraints`/`versionDefines`). |
| QG11 | Montagem só pelo Inspector (AC9). |
| QG12 | `verify_isolated_install.py character` em projeto vazio (sem HFSM/Stats/Input System); testes de pasta temporária usam `TemporaryAssetFolder` (R19). |

---

## 16. Decisões para aprovação
| # | Decisão | Proposta | Alternativas |
|---|---|---|---|
| D1 | Modelo físico | **Rigidbody dinâmico com controle por variação de velocidade** (`VelocityChange` limitado por aceleração), gravidade própria, canais locomoção/externo/vertical, reconciliação por velocidade medida (ADR-0010) | Rigidbody cinemático + *collide-and-slide* próprio (controle total, mas sem empurrões/forças físicas e com mais código de colisão) |
| D2 | Pulo variável | **Corte de velocidade em `v_min`** (altura mínima exata, faixa [Min, Max] garantida) | Multiplicador de gravidade ao soltar (sensação ajustável, mínimo não garantido) |
| D3 | Dono das bordas Jump/Dash | **Configurável por motor:** `Motor` (padrão, standalone) ou `External` (estados da HFSM consomem por token e chamam `Jump()`/`Dash()`); um dono por tipo (ADR-0011) | Sempre o motor (HFSM só observa) · sempre externo (exige HFSM para pular) |
| D4 | Ring buffer genérico | **No Character agora**; avaliar extração para o Core no M4 com o `CombatCommandBuffer` | Já no Core (um único consumidor hoje) |
| D5 | Rampas | **Projeção no plano do chão + snapping**; íngreme = não é chão, desliza, sem pulo | Permitir pulo em rampa íngreme; aderência por atrito |
| D6 | Plataformas móveis | **Adiar**; `GroundInfo` já expõe Rigidbody/velocidade do chão | Herdar velocidade linear no M3 |
| D7 | Pulos aéreos e dash aéreo | **Incluídos com contadores** (`MaxAirJumps` = 0, `MaxAirDashes` = 1 por padrão) | Fora do M3 |
| D8 | Detecção de colisão padrão | **`ContinuousDynamic`** no prefab do sample, validator recomenda para atores com dash/launch; package não força | `ContinuousSpeculative` (mais barato, pode gerar contatos fantasmas) · `Discrete` |
| D9 | 2.5D | **`PlanarAxisScale` genérico** no profile (ex.: profundidade mais lenta) e `FacingMode.HorizontalOnly`; nada de "lanes" no package | Lanes discretas (específico de jogo → fica no WOR) |
| D10 | Input do sample | **Eventos IMGUI** no sample principal + sample opcional com Input System | Exigir Input System no sample principal |
| D11 | Modificador de altura de pulo (Stats) | **Mantém `TimeToApex`** (recalcula g e v0) | Mantém g (recalcula v0; tempo de ar muda) |
| D12 | Unidade de coyote/buffer | **Segundos** de tempo de jogo (erro ≤ 1 passo) | Passos fixos (dependente de Δt) |

## 17. ADRs propostos
| ADR | Título | Fecha |
|---|---|---|
| [ADR-0010](../adr/0010-character-motor-physics-model.md) | Motor de personagem: Rigidbody dinâmico, controle por variação de velocidade e canais | D1, D5, D8 |
| [ADR-0011](../adr/0011-character-command-buffer.md) | `CharacterCommandBuffer`: comandos, dono das bordas, relógio, capacidade | Pendências do M3 no ADR-0004; D3, D4, D12 |
| [ADR-0012](../adr/0012-character-timing.md) | Decisões temporais do Character | Pendências do M3 no ADR-0003 (passo fixo, interpolação, segundos, `timeScale`) |

## 18. Estrutura do package (proposta)
```
Runtime/  Definitions/ (MovementProfileDefinition, enums)   Commands/ (CharacterCommandBuffer, CharacterCommands, CommandEdgeQueue)
          Movement/ (MovementModel, JumpMath, eventos)        Ground/ (GroundSensor, GroundInfo)
          Facing/ (FacingModel, FacingController, presenters)  Forces/ (IExternalForceReceiver, tipos)
          Components/ (CharacterMotor)
          Integration/HierarchicalStateMachine/ · Integration/Stats/ · Integration/InputSystem/
Editor/   Inspectors/ · Gizmos/ · Debugger/ · Validation/
Tests/    Editor/ (domínio, buffer, sensor, validators) · Runtime/ (PlayMode + fixtures) · Editor/Integration/<Outro>/
Samples~/ MovementPlayground/ · InputSystemPlayer/ (opcional)
```

---

## 19. Ajustes da revisão (v2) — normativos

### 19.1 Integração vertical: uma única equação, gravidade aplicada uma vez
- **Dono da gravidade: `MovementModel`.** O `CharacterMotor` não aplica `ForceMode.Acceleration` nem usa
  `Rigidbody.useGravity` (desligado no `Awake` e cobrado pelo Validator). A §3.3 passo 5 passa a ser **uma única**
  chamada `AddForce(alvo − medida, ForceMode.VelocityChange)` por passo.
- Equação do passo (Δt = passo fixo; `v_y⁰` = velocidade vertical medida e reconciliada no início do passo):
  ```
  v_y = v_y⁰ + Δv_pulo_ou_launch − g_ef · Δt,     v_y ≥ −MaxFallSpeed
  g_ef = g_up se v_y⁰ > 0 · g_down se v_y⁰ ≤ 0 · (× DashGravityScale durante o dash)
  posição (PhysX, Euler semi-implícito): x ← x + v · Δt
  ```
  É a integração assumida na correção discreta da §7.2 (o primeiro passo do pulo já desconta `g·Δt`).
- **Chão caminhável:** sem gravidade; a velocidade é a locomoção projetada no plano do chão (o `y` vem da projeção)
  mais o termo de snapping. **Rampa íngreme:** a única aceleração é a gravidade **projetada** no plano da rampa,
  aplicada pela mesma equação — nunca a gravidade vertical somada à projetada.
- Testes que detectam duplicação: queda livre (`v_y` após N passos = `−g·N·Δt` ± ε; o dobro falha), altura e tempo de
  ápice do pulo, queda da borda, aceleração ao longo da rampa íngreme = `g·sen θ` (nem `2g·sen θ`, nem `g + g·sen θ`).

### 19.2 GroundSensor: sobreposição inicial, buffer cheio, múltiplas superfícies
- **Sobreposição inicial:** antes do sweep, `PhysicsScene.OverlapSphere(..., Collider[] buffer, ...)` (NonAlloc) na
  esfera de prova. Para cada collider sobreposto (exceto os do próprio ator e triggers), `Physics.ComputePenetration`
  entre o collider do personagem e o outro dá direção e profundidade; direção caminhável → chão com distância
  **negativa** (afundado) e essa normal. Não se depende de `distance == 0` do sweep (esses hits são ignorados: já
  cobertos pela sobreposição).
- **Ordem e saturação** (o tratamento de buffer cheio foi substituído na §19.8): a documentação 6000.6 não garante ordenação dos resultados nem quais hits entram quando há
  mais que a capacidade. O sensor percorre os `n` resultados e escolhe pelo critério (menor distância entre os
  caminháveis; senão a menor distância). Se `n == capacidade` (saturado), faz também a consulta de **um único hit**
  (`PhysicsScene.SphereCast(..., out RaycastHit, ...)`, que devolve o acerto mais próximo) para garantir o mais próximo
  e incrementa `SaturationCount` (debugger). A camada do próprio ator fora da máscara é exigida pelo Validator; ainda
  assim os próprios colliders são filtrados.
- **Múltiplas superfícies:** entre caminhável e íngreme no alcance, vence a caminhável mais próxima; teto nunca é chão
  (normal com componente `y ≤ 0`).
- Testes (EditMode, colliders reais): começar afundado no chão; saturação com mais colliders que a capacidade (o
  escolhido é o mesmo de uma busca exaustiva); caminhável × íngreme × teto; trigger e camada ignorados; borda.

### 19.3 Forças externas
- **Distância ideal × deslocamento:** `d_ext = |v_ext|² / (2 · desaceleração externa)` é a distância do **canal externo**
  sem colisões. O deslocamento do personagem é `∫(v_loc + v_ext + v_y) dt` e inclui a locomoção (com controle
  reduzido). Testes: comando neutro → deslocamento ≈ `d_ext`; comando contrário → o canal externo ainda percorre `d_ext`
  e o deslocamento total fica ≥ `d_ext − d_loc_max` (o máximo que a locomoção com `ControlDuringExternal` contrapõe no
  mesmo intervalo).
- **Contrato:**
  ```csharp
  void AddImpulse(Vector3 amount, ExternalForceMode mode = ExternalForceMode.VelocityChange);
  bool TryAddForceOverTime(Vector3 amount, float duration, ExternalForceMode mode, out ExternalForceHandle handle);
  bool RemoveForce(ExternalForceHandle handle);
  void ClearExternalForces();
  Vector3 ExternalVelocity { get; }
  ```
- **Unidades:** impulso `VelocityChange` = Δv em **m/s**; `Impulse` = **N·s** (Δv = J / massa). Força ao longo do tempo:
  `VelocityChange` = aceleração em **m/s²** (ignora massa); `Impulse` = força em **N** (a = F / massa).
- **Falha explícita:** `TryAddForceOverTime` devolve `false` (handle inválido) com as 8 vagas ocupadas, `duration ≤ 0`
  ou valor não finito.
- **Handles seguros:** `ExternalForceHandle` = (vaga, geração); cada reutilização da vaga incrementa a geração;
  `RemoveForce` com geração antiga devolve `false` e não remove a força nova. `default` é inválido.
- `IExternalForceReceiver` é **Experimental** na 0.1.0 (Stable após o Combat validá-lo no M4).

### 19.4 Ownership de comandos (D3): requisição em duas fases
- Dono `Motor`: o motor, no `FixedUpdate`, consome a borda **somente quando consegue executar**; senão a borda espera
  até a janela expirar.
- Dono `External` (ex.: estado da HFSM): o executor **não consome**; chama `motor.RequestJump(token)` /
  `motor.RequestDash(token)`. Resultado imediato (`ActionRequestStatus`): `Accepted` (pendente), `NotOwner` (motor não
  está em modo External para esse tipo), `UnknownToken` (borda não está pendente no buffer), `AlreadyPending` (mesmo
  token já pedido — não duplica).
- No `FixedUpdate`, com pedido pendente: pode executar → `TryConsume(token)` e executa → `Executed`; não pode → segue
  pendente enquanto a borda estiver na janela (ex.: aterrissar dentro do jump buffer executa no passo da aterrissagem);
  janela expirou → `Rejected` (a borda expira, **não** é consumida); token já consumido/expirado → `Rejected`. O
  desfecho sai no evento `ActionResolved` (tipo, token, resultado) e em `LastJumpRequest`/`LastDashRequest`.
- Consumo acontece **uma vez e só junto com a execução**; vários `FixedUpdate` no frame executam no primeiro possível;
  frame sem `FixedUpdate` mantém o pedido.
- Testes: dono errado; token desconhecido; mesmo token duas vezes; 3 passos fixos no frame → 1 execução; frame sem
  passo fixo; pedido no ar sem pulo aéreo → executa ao aterrissar dentro da janela / rejeitado fora dela; estado da HFSM
  com claim → um único pulo e uma única remoção da borda.

### 19.5 Movimento 2.5D (D9)
- `PlanarAxisScale` aceita **0** em um eixo (≥ 0 em ambos, não ambos 0). Locomoção e direção de dash são projetadas nos
  eixos com escala > 0; dash resultante nulo usa o facing (também projetado).
- Restrição física opcional no componente: `FreezeZeroScaleAxes` (padrão desligado) acrescenta
  `RigidbodyConstraints.FreezePositionX/Z` nos eixos com escala 0 (forças externas também não movem nesse eixo).
  Desligado, forças externas e colisões ainda podem mover no eixo bloqueado. Sem lanes.

### 19.6 Stats durante o salto (D11)
- `g_up`, `g_down`, `v0` e `v_min` (com os multiplicadores do `IMovementModifierSource` e a correção do Δt) são
  **capturados na decolagem**. Mudanças de buff durante o voo valem a partir do **próximo** salto. Ao sair do chão sem
  pular, os parâmetros de queda são capturados no momento da saída.
- Teste: alterar o multiplicador de altura durante a subida não muda o ápice em andamento e muda o salto seguinte.

### 19.7 Relógios
- **Instantes:** um único referencial — `Time.timeAsDouble` (tempo de jogo escalado). O buffer carimba a borda com ele
  no `Enqueue` (normalmente em `Update`); o motor calcula a idade com o mesmo `Time.timeAsDouble` lido no `FixedUpdate`
  (dentro do `FixedUpdate` a Unity devolve o tempo do passo fixo).
- **Durações** (coyote, cooldown, duração do dash, tempo no ar) são acumuladas pelo modelo somando o Δt fixo e nunca
  comparadas com instantes. O relógio acumulado do motor não é usado para idade de bordas.
- Casos: vários `FixedUpdate` no frame → cada passo vê um tempo maior (Δt a Δt); a borda é consumida no primeiro em que
  puder executar. Idade negativa (não esperada) vale 0. `timeScale` < 1 → tempo de jogo e passos escalam juntos (janela
  em segundos de jogo preservada); `timeScale = 0` → sem passos e sem expiração. `maximumDeltaTime` limita os passos de
  recuperação após um frame longo; bordas valem pelo tempo de jogo, não pelo número de passos.

### 19.8 Revisão final da implementação (0.1.0) — normativo
- **Decisões aprovadas:** (1) `RequestJump/RequestDash(token)` em duas fases; `AlreadyPending` = **qualquer** pedido
  pendente da mesma ação (mesmo token ou outro); (2) o último passo do dash cobre só o tempo restante (distância
  nominal exata sem obstrução); (3) dependência hard de `com.unity.modules.physics`; (4) jump buffer via HFSM com
  latência de até um frame mais um passo fixo após a aterrissagem.
- **Notificações:** `ActionResolved` entra na fila de eventos do passo (FIFO com `Jumped`, `DashStarted` etc.) e é
  entregue no fim do `FixedUpdate`, depois de `Integrate` e do `AddForce`. A entrega usa uma cópia da fila: pedidos,
  impulsos, cancelamentos ou `Teleport` feitos por listeners só valem nos passos seguintes; notificações geradas
  durante a entrega ou fora de um passo saem no fim do próximo passo. `LastJumpRequest/LastDashRequest` mudam na hora.
- **Cancelamento:** `CancelJumpRequest/CancelDashRequest(token)` → `true` só para o pedido pendente daquele token.
  A ação não executa, a borda é descartada do buffer (não pode executar depois, nem por outra claim),
  `ActionResult.Cancelled` é registrado na hora e notificado uma vez. Token antigo não cancela pedido novo; pedido já
  executado devolve `false` e a física não é desfeita. `JumpBehaviour`/`DashBehaviour` cancelam o próprio pedido no
  `OnExit` (interrupção por Dead, Hitstun…); `Teleport` cancela pedidos pendentes. A HFSM não muda e não passa a
  conhecer o Character.
- **GroundSensor saturado (substitui o fallback de hit único da §19.2):** buffer primário (8) cheio → a mesma
  consulta é repetida num buffer estendido pré-alocado (32), no sweep e na sobreposição, e a seleção (caminhável antes
  de íngreme, depois menor gap) roda sobre todos os resultados. Com menos de 32 resultados a escolha é a de uma busca
  exaustiva. Com 32 ou mais (saturação residual, contada em `GroundQueryResidualSaturationCount`) **não há garantia de
  exaustividade**: o sweep também considera o acerto único mais próximo, mas uma caminhável fora do buffer pode ser
  perdida. Sem alocação recorrente.
