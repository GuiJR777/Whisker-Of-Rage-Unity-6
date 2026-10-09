# ADR-0003 — Ordem de execução por bandas definidas no Core

- **Status:** Aceita (M0) · **Revisada** na revisão do M0: contrato e limitações explicitados
- **Relacionada:** [ADR-0004](0004-command-buffers.md) (ciclo de vida de comandos)

## Contexto
Bugs de ordem (input lido depois da decisão, motor antes do knockback) são difíceis de reproduzir.
A recomendação de Jonas Tyroller é ter ordem de execução explícita e previsível. O framework precisa disso sem
criar um runtime comum que acople os packages.

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

**As bandas não são um scheduler.** Elas são uma convenção de prioridade sobre o *Script Execution Order* da Unity.
O framework não tem um loop de simulação próprio nesta fase.

## O que as bandas garantem
1. **Dentro de uma mesma função de evento** (`Update`, `FixedUpdate`, `LateUpdate`, e `Awake`/`OnEnable`/`Start`
   de objetos carregados juntos), componentes de banda menor rodam antes dos de banda maior.
2. A garantia vale entre packages diferentes sem que eles se conheçam (o valor vem do Core).
3. Em uma mesma passada de `FixedUpdate`, a ordem relativa é a mesma de `Update`.

## O que as bandas **não** garantem
| Não garante | Consequência para quem escreve código |
|---|---|
| Ordem **entre** loops. `FixedUpdate` roda **antes** de `Update` no frame, e 0..N vezes por frame. | Um comando escrito no `Update` do frame N só é visto por `FixedUpdate` no frame N+1 (se houver passo fixo). |
| Ordem entre instâncias da mesma classe ou de classes com o mesmo valor. | Não dependa da ordem entre dois atores; resolva interações simultâneas explicitamente (M4). |
| Callbacks de física (`OnTriggerEnter`, `OnCollisionEnter`...), que rodam após o passo de física, depois de todos os `FixedUpdate`. | Não use callbacks de física para lógica que precisa de banda; use queries (`Overlap*NonAlloc`) dentro do tick. |
| Coroutines, `async`/`Awaitable`, `UnityEvent`, eventos de animação, `StateMachineBehaviour`. | Não use para simulação; só para apresentação/glue. |
| O momento em que o Input System processa eventos (depende do *Update Mode* dele). | Comandos de borda são *latched* nos buffers (ADR-0004), então isso não causa perda. |
| Objetos instanciados no meio do frame: o primeiro `Update` deles pode ser no frame seguinte. | Spawns não participam do tick do frame em que nasceram. |
| Determinismo entre máquinas/frame rates. | Não-objetivo (R14). |

## Update × FixedUpdate
| | `FixedUpdate` | `Update` |
|---|---|---|
| Frequência | 0..N vezes por frame, passo fixo (`Time.fixedDeltaTime`) | 1 vez por frame, passo variável |
| Uso no framework | Tudo que depende de física: motor, forças/knockback, queries de hitbox/hurtbox | Amostragem de input, decisão (BT), lógica não física, timers de UI |
| Delta | `Time.fixedDeltaTime` passado ao domínio | `Time.deltaTime` passado ao domínio |
| Apresentação | Nunca | `LateUpdate`/banda `PRESENTATION` |

Regra: o domínio nunca lê `Time.*`; o componente passa o delta do loop em que está.
Um componente pode tickar nos dois loops (ex.: estado da HFSM com `Tick` e `FixedTick`), cada um com seu delta.

## Decisões temporais adiadas (com dono)
| Milestone | Decisão |
|---|---|
| M2 (HFSM) | Em qual loop as transições são avaliadas; se estados têm `Tick` e `FixedTick`; se há no máximo uma transição por tick. |
| M3 (Character) | Valor do passo fixo; interpolação de Rigidbody; motor consumindo comandos no `FixedUpdate`; coyote time e jump buffer medidos em segundos ou em passos fixos; efeito de `timeScale`. |
| M4 (Combat) | Fases de ataque em segundos ou em passos fixos; hitstop (time scale local × global); amostragem da janela ativa da hitbox por passo + sweep para golpes rápidos; resolução de acertos simultâneos (trades); janela do input buffer de combo; em qual loop o combate tica. |
| M5 (Abilities) | Relógio de cooldowns e efeitos periódicos (escalado ou não), em qual loop os efeitos tickam. |
| M8 (Integração) | Reavaliar um scheduler central **somente** se houver problema medido que as bandas + buffers não resolvem. |

## Alternativas
- **Manager central que tica todos os sistemas:** máxima previsibilidade, mas acopla packages a um runtime comum
  e dificulta uso isolado. Reavaliado no M8 com dados.
- **Script Execution Order nas Project Settings:** depende de cada projeto configurar.

## Consequências
- Ordem previsível entre packages **dentro de cada loop**, sem dependência entre eles.
- Latência máxima de comando de um frame mais um passo fixo, documentada e aceita até medição contrária (M3/M4).
- Interações entre loops são tratadas por contrato de dados (buffers, ADR-0004), não por ordem.
