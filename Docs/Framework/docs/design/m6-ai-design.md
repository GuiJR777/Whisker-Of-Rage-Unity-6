# M6 — AI / Behaviour Trees · Especificação técnica

- **Status:** **v1 — proposta para auditoria arquitetural** (nenhum código; decisões D1–D16 e ADRs 0026–0032
  propostas) · **Data:** 2026-10-13
- **Unity:** 6000.6.5f1 · **Package:** `com.ramirestechgames.ai` (L5, `RamiresTechGames.AI`, define
  `RAMIRESTECHGAMES_AI`)
- **Dependências (grafo, sem mudança):** hard `core`; opcionais `stats` (ADR-0009), `character`, `combat`,
  `abilities` (ADR-0002). **HFSM não entra no grafo do AI** (ADR-0004: "AI não depende da HFSM"); a integração com a
  HFSM é pelos command buffers (§12.5, D9). Módulo nativo `com.unity.modules.physics` (linha de visão e desvio).
- **Base contratual:** Core 0.4.0, Stats 0.1.1, HFSM 0.1.0, Character 0.2.0, Combat 0.1.0, Abilities 0.1.0
  (candidatas, sem tags). **Nenhuma API aprovada é alterada**; mudanças opcionais futuras estão isoladas na §20.
- **Fora do package:** comportamento de personagens de um jogo. No Whiskers of Rage, os inimigos das fases, os chefes e
  qualquer IA de aliado são **assets e glue do jogo** montados com os mecanismos genéricos daqui (§19). Nenhum nome de
  personagem (Ryu ou outro) aparece em código, assets ou samples do package.

---

## 1. Objetivo e escopo

Um **sistema de decisão genérico, data-driven e determinístico por passo fixo** para atores controlados por IA, em
escala indie: Behaviour Trees como assets, Blackboard tipado por agente, sensores e memória de percepção, seleção de
alvo por pontuação, coordenação de engajamento (quantos atacam ao mesmo tempo e de onde), movimento por um contrato
de locomoção, e **saída exclusivamente por command buffers** dos packages de execução. A BT **decide**; HFSM, Character,
Combat e Abilities **executam** (ADR-0004). O mesmo prefab funciona com jogador ou IA trocando só a fonte de comandos.

### 1.1 Obrigatório no M6 · pendente de decisão · adiado

| Obrigatório no M6 | Pendente de decisão (§21) | Adiado (fora do M6) |
|---|---|---|
| `BehaviourTreeDefinition` (nós SR, subárvores), instância por agente com memória por nó | Loop e relógio (D1) | GOAP, HTN, planejadores, ML |
| Composites (Sequence, Selector, Parallel com política explícita, Random e Utility determinísticos) | Modelo de tick e aborts (D2) | NavMesh (`com.unity.ai.navigation`) — exige ADR de grafo |
| Decorators (Condition com abort, Inverter, ForceResult, Repeat, Cooldown, TimeLimit) e Services | Decorators anexados × nós (D3) | Blackboard compartilhado de esquadrão e táticas além do engajamento |
| `BlackboardDefinition`/`Blackboard`: chaves tipadas, referências por id, versões | Blackboard (D4), identidade (D5) | Sistema de cobertura, formações, crowd simulation |
| `AIAgent` (banda `DECISION`, intervalo e escalonamento por passos) e `AIWorld` (registro, estímulos, passo) | Percepção (D6), movimento e navegação (D7) | Salvar/carregar estado de IA; rede/rollback (R14) |
| Sensores (visão 2.5D com linha de visão, proximidade, audição por estímulos, dano), memória de percepção, detectabilidade | Engajamento (D8), HFSM (D9) | Editor de "utility curves" dedicado (curvas no Inspector bastam) |
| `ITargetSelector` com pontuação e histerese; coordenação de engajamento (tokens e posições) | Rastreamento de resultados (D10) | Animação/VFX/áudio de IA (só eventos) |
| Contrato `IAgentLocomotion` + ações de movimento (MoveTo, Patrol, Chase, KeepDistance, Flee, Orbit, Wander, Face) com desvio local | Editor de grafo (D11, D12) | Pausa da IA por hitstop do Combat como padrão (fica opcional, D15) |
| Integrações Stats, Character, Combat, Abilities; padrão HFSM sem dependência | Utility e pontuação (D13), samples (D14), Parallel (D16) | Depurador remoto/em build |
| Graph Editor, AI Debugger, gizmos, validators | | |

---

## 2. Conceitos

| Termo | Definição |
|---|---|
| **Agente** | Ator controlado por IA: `AIAgent` + BT + Blackboard + sensores + memória. Também é um percebível. |
| **Percebível** | `AIPerceivable`: algo que sensores podem detectar (ator, objeto de interesse), com identidade, time, pontos de mira e detectabilidade. |
| **Behaviour Tree (BT)** | `BehaviourTreeDefinition`: árvore de nós SR compartilhada; a instância por agente guarda o caminho ativo e a memória dos nós. |
| **Nó** | `BehaviourNode` com `OnStart`/`OnTick`/`OnStop`; `NodeStatus` ∈ {`Running`, `Success`, `Failure`}; parada com `StopReason` ∈ {`Completed`, `Aborted`, `TreeStopped`}. |
| **Composite / Decorator / Service** | Composite ordena filhos; decorator anexado a um nó altera entrada/resultado ou aborta; service anexado a um composite roda periodicamente enquanto ele está ativo. |
| **Abort condicional** | Decorator de condição que, reavaliado a cada tick, interrompe o próprio ramo (`Self`), ramos de menor prioridade (`LowerPriority`) ou ambos (`Both`). |
| **Blackboard** | Memória chave-valor tipada **do agente** (definição compartilhada, instância por agente). |
| **Estímulo** | Ocorrência pontual (ruído, dano, evento de jogo) publicada no `AIWorld` com ordem total e entregue no passo seguinte. |
| **Memória de percepção** | Entidades percebidas pelo agente (última posição conhecida, sentidos, ameaça, instantes), capacidade fixa com top-K. |
| **Engajamento** | Coordenação por alvo: **tokens** (quantos podem atacar ao mesmo tempo, por tipo) e **posições** (anel de vagas ao redor do alvo, no frame do facing dele). |
| **Locomoção** | Contrato `IAgentLocomotion` (ler pose/velocidade, escrever direção, facing, pulo/dash); o Character o implementa por adaptador. |

---

## 3. Arquitetura

### 3.1 Camadas e assemblies

| Camada | Assembly | Conteúdo |
|---|---|---|
| Domain | `RamiresTechGames.AI` (`Runtime/Trees`, `Runtime/Blackboards`, `Runtime/Perception`, `Runtime/Selection`, `Runtime/Engagement`, `Runtime/Steering`) | `BehaviourTreeInstance`, `TreeLayout` (compilado), `Blackboard`, `PerceptionMemory`, `TargetScoring`, `EngagementArbiter`, `SteeringModel` — puros, recebem Δt e passo |
| Unity Integration | `RamiresTechGames.AI` (`Runtime/Components`, `Runtime/Definitions`) | `AIAgent`, `AIWorld` (+ helpers de fase ocultos), `AIPerceivable`, `AIPatrolRoute`, `AIEngagementTarget`, definições (SO) |
| Contratos de extensão | `RamiresTechGames.AI` (`Runtime/Extensibility`) | `BehaviourNode`/`BehaviourNode<TMemory>`, `BehaviourDecorator`, `BehaviourService`, `BehaviourCondition`, `ISensor`, `ITargetSelector`, `ITargetConsideration`, `IAgentLocomotion`, `INavigationProvider`, `IAITeamSource`, `IDetectabilitySource` |
| Integration | `RamiresTechGames.AI.Integration.Stats` | Condições e considerações sobre stats/recursos (ADR-0009) |
| Integration | `RamiresTechGames.AI.Integration.Character` | `CharacterAgentLocomotion` (→ `CharacterCommandBuffer`), condições de chão/dash |
| Integration | `RamiresTechGames.AI.Integration.Combat` | Ações `PerformCombatAction`/`HoldGuard`, condições de estado (próprio e do alvo), estímulo de dano e ameaça, time pelo perfil |
| Integration | `RamiresTechGames.AI.Integration.Abilities` | Ação `ActivateAbility`/`HoldAbilityInput`, condições (pode ativar, cooldown, tags possuídas), detectabilidade por tags, estímulos de Gameplay Events, time |
| Editor | `RamiresTechGames.AI.Editor` | Behaviour Tree Graph Editor (GraphView sobre modelo de edição), inspectors, AI Debugger, gizmos, validators |

O código de domínio **não referencia** Character, Combat, Abilities, Stats nem HFSM (verificado pelas asmdefs e por
`check_dependencies`). Ações que tocam motor, combate ou habilidades existem **só** nas integration assemblies
(critério do roadmap: "nenhuma chamada direta a motor/combat fora dos adapters").

### 3.2 Componentes de runtime

| Componente | Responsabilidade |
|---|---|
| `AIWorld` | Um por cena, **componente explícito** (sem singleton estático, como o `ProjectileDriver`). Contador de passos, registro de percebíveis e agentes, snapshot P0, barramento de estímulos, arbitragem de engajamento. |
| `AIAgent` | Ponto único do agente: BT, Blackboard, sensores (SR), memória, seletor de alvo, RNG determinístico, fila de entradas diferidas, intervalo/escalonamento. Banda `DECISION + 20`. |
| `AIPerceivable` | Identidade (`AIEntityId`), time (`IAITeamSource`), pontos de mira (centro, olhos), detectabilidade (`IDetectabilitySource`), facing. Registrado no `AIWorld` ao habilitar. |
| `AIEngagementTarget` | Opcional no alvo: `EngagementProfileDefinition` (pools de tokens por tag, anel de posições). Sem ele, o alvo não limita atacantes. |
| `AIPatrolRoute` | Pontos de patrulha (loop/ida-e-volta), gizmos. |
| `CharacterAgentLocomotion` (integração) | Implementa `IAgentLocomotion` escrevendo no `CharacterCommandBuffer` e lendo `CharacterMotor`/`FacingController`. |
| `CombatAgentBridge`, `AbilityAgentBridge` (integrações) | Escrevem nos buffers do Combat/Abilities e registram, em anéis de capacidade fixa, os eventos que as ações observam (§10). |

### 3.3 Fluxo de um passo fixo (D1, ADR-0026)

```
FixedUpdate do passo n
DECISION        (−900) AIWorld   P0: passo++; snapshot dos percebíveis (pose, time, detectabilidade, flags das
                                 integrações); publica os estímulos emitidos até n−1 em ordem total (top-K)
DECISION + 20   (−880) AIAgent   para cada agente com avaliação devida neste passo:
                                 entradas diferidas (eventos dos bridges, SetValue externos) → sensores devidos →
                                 memória → services/seleção de alvo → tick da BT → saídas:
                                 Blackboard próprio, command buffers (via adaptadores), pedidos de engajamento,
                                 estímulos emitidos (valem em n+1)
DECISION + 60   (−840) AIWorld   arbitragem de engajamento em ordem total → concessões visíveis no tick seguinte
STATE_MACHINE   (−800)           HFSM com EvaluationLoop = FixedUpdate: condições espiam/claim as bordas da IA
ABILITIES … COMBAT … CHARACTER   executores consomem as bordas no mesmo passo n
```

Regras de determinismo (normativas, §13): (1) um agente só **escreve** no próprio Blackboard, na própria memória, nos
próprios buffers (pelos adaptadores), na fila de pedidos do `AIWorld` e no barramento de estímulos; (2) só **lê**
outros atores pelo snapshot P0 ou por estado somente-leitura dos packages, que na banda `DECISION` reflete o fim do
passo *n − 1* (ninguém muta o mundo antes dela); (3) tudo que um agente produz para outros (estímulos, pedidos) vale
a partir do passo seguinte ou da fase +60; (4) toda lista tem ordem total e saturação top-K contada.

---

## 4. Definições (ScriptableObjects, imutáveis em runtime)

### 4.1 `BehaviourTreeDefinition`
| Campo | Conteúdo |
|---|---|
| `Blackboard` | `BlackboardDefinition` exigido (chaves usadas pelos nós). |
| `Nodes` | Lista plana de `NodeData` (`Id` GUID, `ParentId`, `ChildOrder`, `Name`, `Node` SR, `Decorators` SR, `Services` SR, `EditorPosition`, `Comment`). Raiz única (`ROOT_ID`). |
| `Subtrees` | `SubtreeNode` referencia outra definição; o Blackboard da subárvore deve ser o mesmo ou um ancestral (D4). Ciclo de inclusão = erro do validator e nó sem filhos em runtime (como sub-máquinas da HFSM). |
| `TickBudget` | Máximo de nós iniciados por tick (padrão 128); excedido → tick encerra como `Running`, contador `BudgetExceededCount`. |

Compilada uma vez por definição (`TreeLayout`: índices, filhos contíguos, decorators/services por nó, chaves
resolvidas), compartilhada entre agentes. Estado por agente vive na instância (status, memória por nó, caminho ativo).

### 4.2 `BlackboardDefinition`
Chaves `{Id (GUID), Name, Type, Default, Description}` com tipos `Bool`, `Int`, `Float`, `Vector3`, `GameplayTag`,
`Entity` (`AIEntityHandle`: registro + geração, inválido quando o percebível some) e `Object` (referência Unity
com tipo esperado). `Parent` opcional (herança de chaves, ciclo = erro, nomes únicos na cadeia). Referências por id
(`BlackboardKeyReference` com cache, renomear não quebra), como os parâmetros da HFSM.

### 4.3 Outras definições
| SO | Conteúdo |
|---|---|
| `SensorProfileDefinition` | Lista SR de `ISensor` com parâmetros (alcance, ângulo, intervalo, máscara de obstáculos), `MemoryCapacity`, `ForgetTime`. Compartilhável entre arquétipos. |
| `EngagementProfileDefinition` | Pools de tokens por `GameplayTag` (capacidade, tempo máximo de posse), anel de posições (quantidade, raio, ângulos no frame do facing do alvo, tags de preferência como "frente"/"costas"). |
| `TargetSelectionDefinition` | Considerações SR com pesos e curvas, histerese, filtros (hostil, sentidos exigidos). |

---

## 5. Behaviour Tree

### 5.1 Ciclo de vida dos nós
- `OnStart(context)` uma vez ao entrar; `OnTick(context)` → `NodeStatus`; `OnStop(context, reason)` exatamente uma vez
  ao sair (`Completed`, `Aborted`, `TreeStopped`), **da folha para a raiz** quando um ramo é interrompido.
- Um nó pode completar no mesmo tick em que começou; o pai continua no mesmo tick (sem esperar um passo), dentro do
  `TickBudget`.
- Memória por agente e por nó (`BehaviourNode<TMemory>`), criada na primeira montagem e reutilizada (zero GC).
- Exceção num nó é logada com o id do nó; o nó vale `Failure` e a árvore continua (como a HFSM).

### 5.2 Composites
| Composite | Semântica |
|---|---|
| `Sequence` | Filhos em ordem; falha no primeiro `Failure`; sucesso quando todos terminam. |
| `Selector` | Filhos em ordem de prioridade; sucesso no primeiro `Success`; falha quando todos falham. Base dos aborts `LowerPriority`. |
| `Parallel` | Todos os filhos no mesmo tick, em ordem; **política explícita** `SuccessPolicy`/`FailurePolicy` ∈ {`RequireOne`, `RequireAll`}; ao decidir, os filhos ainda ativos param com `Aborted` (D16). |
| `RandomSelector` / `RandomSequence` | Ordem embaralhada com pesos, pela sequência aleatória do agente (determinística). |
| `UtilitySelector` | Escolhe o filho de maior pontuação (`IUtilityConsideration` por filho); empate pela ordem declarada; reavalia ao reiniciar ou por abort (D13). |
| `SubtreeNode` | Executa outra definição como filho (mesmo Blackboard ou ancestral). |

### 5.3 Decorators anexados (D3)
| Decorator | Semântica |
|---|---|
| `ConditionDecorator` | `BehaviourCondition` SR + `Abort` ∈ {`None`, `Self`, `LowerPriority`, `Both`} + `Invert`. Falso ao entrar → o nó não roda (`Failure`). |
| `BlackboardDecorator` | Atalho de condição sobre uma chave (definida/comparação) com os mesmos aborts. |
| `Cooldown` | Após o nó terminar, falha por `Duration` s (relógio do agente). |
| `TimeLimit` | Aborta o nó após `Duration` s (`Failure`). |
| `Repeat` | Reinicia o filho `Count` vezes ou indefinidamente; **no máximo um reinício por tick** quando o filho termina instantaneamente; `StopOnFailure`. |
| `Inverter`, `ForceResult` | Invertem ou fixam o resultado. |

**Ordem dos aborts (normativa):** a cada tick, antes de retomar a folha ativa, o caminho ativo é percorrido da raiz
para a folha; em cada `Selector`, os decorators `LowerPriority/Both` dos filhos **de maior prioridade** que o ativo
são avaliados em ordem; em cada nó do caminho, os `Self/Both` são avaliados. O **primeiro** abort verdadeiro nessa
ordem vence (no máximo um por tick), o ramo é parado (folha → raiz, `Aborted`) e a execução recomeça no ponto
indicado no mesmo tick. Condições só leem (contexto somente-leitura no abort).

### 5.4 Services
`BehaviourService` anexado a um composite: `Interval` em segundos (convertido em passos), primeira execução ao
ativar o composite (configurável), escalonado pelo agente; uso típico: selecionar alvo, atualizar distância, pedir
token. Services escrevem só no Blackboard do agente.

### 5.5 Ações e condições genéricas (Domain, sem outros packages)
- Ações: `Wait` (s, com variação aleatória determinística), `SetBlackboardValue`, `ClearBlackboardValue`,
  `CopyBlackboardValue`, `SelectTarget` (§8), `RequestEngagementToken`/`ReleaseEngagementToken` (§9),
  `EmitStimulus`, `RunSubtree`, `Fail`/`Succeed`, `Log` (Editor/desenvolvimento).
- Locomoção (via `IAgentLocomotion`, §11): `MoveTo` (entidade, posição, ponto de patrulha ou **posição de
  engajamento**), `Patrol`, `Chase`, `KeepDistance` (faixa mín./máx.), `Flee`, `Orbit`, `Wander`, `FaceTarget`, `Stop`,
  `Jump`, `Dash`.
- Condições: comparação de chaves, entidade válida/hostil/percebida agora, distância e ângulo até entidade/posição,
  linha de visão, tempo desde percepção, chance (sequência do agente), token concedido, posição atribuída.

---

## 6. Blackboard (D4, ADR-0028)

- Armazenamento tipado em arrays por tipo, **sem boxing**; leitura/escrita por `BlackboardKeyReference` resolvida.
- `Version` por chave e versão global; aborts podem pular condições cujas chaves observadas não mudaram (otimização,
  semântica idêntica).
- Escrita **somente** durante o tick do próprio agente. Escritas de fora (scripts, bridges) entram na **fila de
  entradas diferidas** (capacidade fixa, FIFO por (passo, origem, sequência), saturação contada) e são aplicadas no
  início do próximo tick avaliado.
- `Entity` guarda handle com geração: percebível destruído/desregistrado lê como inválido (nunca uma referência morta).
- Sem Blackboard compartilhado no M6 (adiado); cooperação entre agentes por engajamento e estímulos.

---

## 7. Percepção (D6, ADR-0029)

### 7.1 Registro e snapshot P0
O `AIWorld` mantém o registro de `AIPerceivable` (capacidade configurável, padrão 256; saturação recusada e contada).
Em P0, copia para o snapshot: `AIEntityId`, posição, pontos de mira, facing plano, time e máscara hostil,
detectabilidade por sentido, flags publicadas pelas integrações. **A descoberta não usa física**: sensores percorrem o
snapshot com pré-filtro de distância; a física só é usada para **linha de visão** (resultado booleano, independente da
ordem de colliders). Ordem total por `AIEntityId`.

### 7.2 Sensores (`ISensor`, SR)
| Sensor | Regra |
|---|---|
| `SightSensor` | Alcance × detectabilidade de visão; ângulo horizontal no plano XZ em torno do facing do agente; tolerância vertical; linha de visão (`RaycastNonAlloc` ou `Linecast` contra a máscara de obstáculos, olhos → ponto de mira); `DetectionDelay` opcional (visão contínua acumulada antes de "detectado"). |
| `ProximitySensor` | Raio 360°, sem linha de visão (opcional). |
| `HearingSensor` | Estímulos de ruído publicados em P0 dentro de `raio do estímulo × sensibilidade`, com linha de visão opcional. |
| `StimulusSensor` | Estímulos direcionados ao agente (dano recebido, eventos de jogo) por tag. |

Cada sensor tem `Interval` (passos) escalonado pelo `AIEntityId`; sensores de estímulo processam todos os
estímulos do passo (nada se perde entre intervalos).

### 7.3 Memória de percepção
Capacidade fixa (padrão 16, 1..64) por agente. Entrada: handle, id, hostil, última posição conhecida, primeira/última
percepção, sentidos (bitmask), percebido agora, consciência (0..1), **ameaça** (acumulada por estímulos de dano,
decaimento configurável). Saturação **top-K** por (percebido agora ↓, hostil ↓, ameaça ↓, última percepção ↓,
distância ↑, id ↑), independente da ordem de chegada; esquecimento após `ForgetTime` sem percepção. Contadores
`MemoryOverflowCount`, `RegistrySaturationCount`, `StimulusOverflowCount`.

### 7.4 Detectabilidade e times
- `IDetectabilitySource` no percebível: multiplicador por sentido (0 = invisível para aquele sentido). Padrão 1. A
  integração Abilities oferece `OwnedTagDetectability` (ex.: uma tag concedida por um efeito zera a visão).
- `IAITeamSource`: time (0..31) e máscara hostil. Padrão `AITeam` (campos próprios); integrações leem o perfil do
  Combat (`IsHostileTo`) ou o `ITeamRelation` do Abilities, para um único time por ator.

### 7.5 Barramento de estímulos
`AIWorld.EmitStimulus(in AIStimulus)` — `Tag`, `Position`, `Radius`, `Strength`, `Source`/`Target` (`AIEntityId`),
`Magnitude`. Emissões em qualquer momento do passo *n* ficam pendentes e são publicadas em P0 de *n + 1*, ordenadas por
(passo, origem, sequência), capacidade fixa por passo (padrão 128) com top-K por (força ↓, origem ↑, sequência ↑).

---

## 8. Seleção de alvo

`ITargetSelector` (SR) devolve a melhor entidade da memória e grava `Entity` + última posição no Blackboard.
Implementação incluída `ScoredTargetSelector`: score = Σ peso·curva(consideração) / Σ peso; considerações
`Distance`, `AngleFromFacing`, `Threat`, `Recency`, `IsCurrentTarget` (histerese), `AttackersOnTarget` (tokens
ocupados no alvo), `TargetFacingAway` (ataque pelas costas), mais `TargetResourceRatio` (Stats) e `TargetHasTags`
(Abilities) nas integrações. Desempate por `AIEntityId`. **Histerese:** o alvo atual só é trocado se outro superar o
score por `SwitchMargin` ou se ficar inválido/esquecido. Último detalhamento de scores fica no debugger.

---

## 9. Engajamento (D8, ADR-0031)

Coordenação típica de beat'em up, genérica: limitar quantos agentes atacam um alvo ao mesmo tempo e distribuí-los
ao redor dele.

- **Tokens:** pools por `GameplayTag` no `EngagementProfileDefinition` do alvo (ex.: um pool "corpo a corpo" com 2 e
  um "distância" com 1 — tags do jogo). `RequestEngagementToken(target, pool, priority)` enfileira um pedido;
  na fase +60 o `AIWorld` processa os alvos por `AIEntityId` e, por alvo, os pedidos por (prioridade ↓, score ↓,
  id do agente ↑) — score padrão = −distância no snapshot P0. Detentores mantêm o token até liberar, até
  `MaxHoldTime`, até o nó parar, ou até o agente/alvo sair. Concessão visível no tick seguinte do agente.
- **Posições:** anel de vagas no frame do facing do alvo (ângulos e raio do perfil, tags de preferência). Atribuição
  gulosa em ordem total: pedidos por (prioridade ↓, id ↑), cada um na vaga livre compatível mais próxima (distância ↑,
  índice ↑). `MoveTo(EngagementSlot)` segue a vaga recalculada a cada passo pela pose do alvo.
- Capacidades fixas (pedidos por passo, detentores por pool, vagas) com saturação contada; zero GC.

---

## 10. Saída de comandos e resultados (D10, ADR-0032)

- A IA **nunca** chama motor, controller ou ASC para executar: escreve **bordas e estado** nos command buffers pelos
  adaptadores (`IAgentLocomotion`, `CombatAgentBridge`, `AbilityAgentBridge`). Ao habilitar, o adaptador chama
  `SetSource(this)` no buffer; ao desabilitar/pausar, `Clear()` e estado neutro (garantia 5 do ADR-0004).
- **Rastreamento por observação** (sem mudar contratos): a ação guarda o token da borda; o bridge registra, em anel de
  capacidade fixa e ordem total, os eventos públicos do executor (`AttackStarted`/`AttackEnded`/`ActionResolved` do
  Combat; `AbilityActivated`/`AbilityEnded`/`ActivationResolved` do Abilities). No tick seguinte a ação decide:
  borda ainda pendente → `Running`; consumida e execução observada → `Running` até `CompleteOn` (`Started` ou
  `Ended`) → `Success`; sumiu sem execução (expirada, descartada, rejeitada) → `Failure`. Funciona com posse do
  próprio package (combo runner/ASC consome) e com posse `External` (HFSM consome).
- `OnStop` de ação abortada: **não** apaga a borda já executada; remove a borda ainda pendente que ela criou
  (`TryConsume(token)` como descarte, garantido pelo ADR-0004) e devolve o estado (guarda, movimento) ao neutro.

---

## 11. Movimento e navegação (D7, ADR-0030)

- `IAgentLocomotion` (componente): leitura `Position`, `Velocity`, `PlanarFacing`, `IsGrounded`, `CanMove`,
  `MaxSpeed`; escrita `SetMove(Vector2 planar, |v| ≤ 1)`, `Stop()`, `SetFacing(dir)`, `ClearFacing()`,
  `RequestJump()`, `RequestDash(dir)` (tokens). O Character o implementa por `CharacterAgentLocomotion`
  (`SetMove`/`SetFacingOverride`/`EnqueueJump`/`EnqueueDash`); testes do domínio usam um fixture próprio.
- `SteeringModel` (puro): seek/arrive, separação entre agentes pelo snapshot P0 (sem física), desvio de obstáculos
  por sonda (`SphereCastNonAlloc`, usa só a menor distância, independente da ordem), detecção de travamento (sem
  progresso por `StuckTime` → `Failure`).
- `INavigationProvider` (SR): `TryGetNextPoint(from, to, out point)`; padrão `DirectNavigation` (linha reta). Arenas de
  beat'em up são abertas; NavMesh fica para um ADR futuro (novo opcional no grafo).
- 2.5D: tudo no plano XZ; `MoveTo` tem `ApproachStyle` = `Direct` ou `AlignDepthFirst` (alinha a profundidade
  configurada no `AIWorld` antes de avançar, típico para entrar na linha de ataque).

---

## 12. Integrações e autoridade

| Domínio | Autoridade | AI faz | Nunca faz |
|---|---|---|---|
| Decisão, percepção, alvo, engajamento | **AI** | tudo | — |
| Movimento, pulo, dash, facing | **Character** | estado `Move`/facing e bordas no `CharacterCommandBuffer` | integrar o motor, aplicar forças, `Teleport` |
| Ataques, guarda, parry, reações | **Combat** | bordas de ação e `GuardHeld` no `CombatCommandBuffer`; lê estado | `StartAttack`, decidir golpes, aplicar dano |
| Habilidades, efeitos, tags | **Abilities** | bordas por tag de entrada e `SetHeld` no `AbilityCommandBuffer`; lê `CanActivate`/cooldown/tags | `TryActivateAbility` direto, aplicar efeitos |
| Estados do personagem | **HFSM** | nada direto: a HFSM consome as mesmas bordas do jogador | trocar estados, ler parâmetros da HFSM |
| Atributos | **Stats** | lê valores e razões de recurso | modificar stats |

### 12.1 Stats (`AI.Integration.Stats`, ADR-0009)
`StatCondition` (próprio/alvo; valor, base ou razão de recurso; comparação), considerações `SelfResourceRatio` e
`TargetResourceRatio`. Leitura do `StatsComponent` do percebível resolvida no registro.

### 12.2 Character (`AI.Integration.Character`)
`CharacterAgentLocomotion` (`IAgentLocomotion`), condições `IsGrounded`, `CanDash`, `IsSuspended` (agarrado/congelado
pelo motor). Exige `CharacterCommandBuffer`; com motor em posse `Motor` ou `External` (HFSM), sem diferença para a IA.

### 12.3 Combat (`AI.Integration.Combat`)
- `CombatAgentBridge`: `IAITeamSource` pelo perfil; `AIEntityId` espelhado do `CombatantId` (opcional); estímulo
  `Damage` (alvo = este agente, origem = atacante, magnitude = dano) a partir de `HitReceived`; registro de eventos
  para rastreamento (§10).
- Ações: `PerformCombatAction` (`CombatActionDefinition`, direção para o alvo/facing, `CompleteOn`, `MaxWait`),
  `HoldGuard` (duração ou enquanto condição; solta no `OnStop`).
- Condições: `SelfCombatState`/`TargetCombatState` (`CanAct`, `IsAttacking`, `IsInHitstun`, `IsInBlockstun`,
  `IsKnockedDown`, `IsGuarding`, `IsGrabbed`, `HealthDepleted`, `IsComboContinuationOpen`), `CanExecuteCombatAction`,
  `WasHitRecently`.

### 12.4 Abilities (`AI.Integration.Abilities`)
- `AbilityAgentBridge`: time pelo `ITeamRelation`; registro de `AbilityActivated`/`AbilityEnded`/`ActivationResolved`;
  estímulos a partir de `GameplayEventReceived` (tag → estímulo/Blackboard).
- Ações: `ActivateAbility` (tag de entrada, mira na entidade/posição/facing, `CompleteOn`), `HoldAbilityInput`.
- Condições: `CanActivateAbility` (`FindAbilityByInputTag` + `CanActivateAbility`), `AbilityCooldown`,
  `HasOwnedTags` (próprio ou alvo, pelo `AbilityActorSnapshot`), `IsAbilityActive`; `OwnedTagDetectability`.

### 12.5 HFSM — sem dependência (D9)
"A BT decide; a HFSM executa" (ADR-0004). Padrão suportado e testado na Sandbox do host:
1. O ator usa a **mesma** `StateMachineDefinition` do jogador (estados das integrações Character/Combat/Abilities com
   posse `External`). A fonte de comandos é o adaptador de input **ou** o `AIAgent`; trocar de fonte limpa os buffers.
2. As condições da HFSM espiam as bordas escritas pela IA e fazem claim; os estados consomem. Com
   `EvaluationLoop = FixedUpdate`, a borda escrita em `DECISION + 20` é consumida no **mesmo passo**; com `Update`, no
   primeiro `Update` seguinte (latência documentada).
3. A IA lê o que precisa do estado de execução pelo Combat/Abilities/Character (`CanAct`, `IsAttacking`, tags), nunca
   pelo estado ativo da HFSM; fases de chefe são ramos da própria BT com aborts sobre chaves do Blackboard (§19).

---

## 13. Tempo, determinismo e latências (D1, ADR-0026)

| Item | Regra |
|---|---|
| Loop | `FixedUpdate`, banda `DECISION` com sub-fases +0/+20/+60 (§3.3). Revisa a linha "BT em `Update`" do ADR-0003 e a regra "AI escreve em `Update`" do ADR-0004 para o caso da IA. |
| Relógio do agente | Segundos de jogo acumulando o Δt fixo; `timeScale` respeitado pelo passo fixo; `Wait`, `Cooldown`, `TimeLimit` e services pelo relógio do agente. |
| Intervalo | `EvaluationInterval` em passos (padrão 1), escalonado por `AIEntityId % intervalo`; `IdleInterval` opcional quando não há alvo. Δt do tick = passos desde a última avaliação × Δt fixo. |
| Hitstop | Não pausa a IA por padrão; `IAITimeSource` opcional (integração Combat oferece pausa pelo hitstop do próprio ator) — D15. |
| Aleatoriedade | Sequência XorShift por agente semeada por `AIEntityId` (+ semente opcional); consumo na ordem da árvore. |
| Física | Só `NonAlloc`; resultados usados de forma independente da ordem (booleano, menor distância). |

| Latência (passo *n*) | Valor |
|---|---|
| Decisão → executor (HFSM em `FixedUpdate`, posse do package) | mesmo passo *n* |
| Estado do mundo visto pela decisão | fim de *n − 1* |
| Estímulo emitido em *n* (inclusive dano da fase 4 do Combat) → sensor | P0 de *n + 1* |
| Pedido de token em *n* → concessão visível | tick seguinte do agente (*n + 1* com intervalo 1) |
| Evento de execução (ataque iniciado/terminado) → resultado da ação | tick seguinte do agente |

---

## 14. Execução e desempenho

- Pré-alocação: layouts compilados por definição, memória de nós, Blackboard tipado, memória de percepção, filas de
  entradas, anéis dos bridges, buffers de física, pedidos e detentores de engajamento.
- **Zero GC em regime aquecido** com 50 agentes (sensores, BT com aborts, seleção de alvo, steering, engajamento,
  integrações). Exceções (criação de conteúdo, primeira montagem) listadas como no M5.
- Custo de percepção O(agentes × percebíveis) com pré-filtro de distância e intervalos; medido e registrado (não é
  gate; grade espacial só com medição que a justifique, R35).
- Capacidades fixas, todas com contador de saturação e top-K pela chave canônica (lição da auditoria do M5).

---

## 15. Ferramentas de autoria (D11, D12)

| Ferramenta | Conteúdo |
|---|---|
| Behaviour Tree Graph Editor | GraphView como **vista** sobre `BehaviourTreeGraphModel` (modelo sem UI, testável, Undo/Redo), como o ADR-0001 da HFSM; layout de cima para baixo; ordem dos filhos explícita (reordenar por arraste, opção de ordenar pela posição X); decorators/services como faixas dentro do nó; painel lateral com `PropertyField` + seletor do Core; painel de Blackboard; busca para criar nós; abrir subárvore; validação inline. |
| Inspector de `BlackboardDefinition` | Chaves com tipo, padrão e herança; uso por árvore. |
| AI Debugger (Play Mode) | Agentes; status ao vivo por nó no grafo (Running/Success/Failure/Aborted com idade em passos), caminho ativo e último abort; Blackboard ao vivo (edição pela fila diferida); memória de percepção; scores da última seleção; tokens e vagas; estímulos do passo; contadores; histórico (anel de 64). |
| Gizmos | Cones de visão, alcances, linhas de visão, últimas posições conhecidas, anel de engajamento, vetores de steering, rota de patrulha. |
| Validators | Composite sem filhos; `Parallel` sem política; abort `LowerPriority` fora de filho de `Selector`; chave ausente/tipo errado; Blackboard da subárvore incompatível; ciclo de subárvore ou de herança de Blackboard; intervalo ≤ 0; sensor sem máscara de obstáculos; `TickBudget` menor que a profundidade; cena: agente sem `AIWorld`, sem locomoção quando a árvore usa movimento, ação de Combat fora do moveset (aviso), `AIEntityId` repetido. |

Reavaliação do Graph Toolkit (gatilhos do spike do M2, conferidos no Unity 6000.6.5f1 em 2026-10-13): GraphView com
0 de 90 tipos marcados `[Obsolete]`; o Graph Toolkit (`UnityEditor.GraphToolkitModule`) continua com o mesmo modelo de
opções (`INodeOption`/`IOptionBuilder`), sem suporte demonstrado a valores `[SerializeReference]`. **Gatilhos não
disparados** → GraphView (D11).

---

## 16. Samples (D14)
- **AI Basics** (AI + Character): guardas com rota de patrulha, visão com obstáculos, audição (emissor de ruído),
  perseguição, busca na última posição conhecida e retorno; fuga com raio; painel com Blackboard e memória.
- **AI Combat Arena** (AI + Character + Combat + HFSM + Abilities + Stats): jogador pelo teclado e o **mesmo prefab**
  controlado por IA; quatro arquétipos genéricos — *Brawler* (corpo a corpo com guarda ocasional), *Skirmisher*
  (bate e recua, prefere a vaga das costas), *Caster* (mantém distância e usa um projétil por habilidade), *Bruiser*
  (lento, armadura do ataque, sem guarda) — tokens de engajamento (2 corpo a corpo + 1 distância) e um *Champion* com
  fases por razão de vida. Tudo por assets; atende ao critério Patrol/Chase/Attack/Retreat do roadmap.

---

## 17. API pública (proposta)
| Tipo | Categoria | Estabilidade |
|---|---|---|
| `BehaviourTreeDefinition`, `BlackboardDefinition`, `SensorProfileDefinition`, `EngagementProfileDefinition`, `TargetSelectionDefinition` | SO | Experimental |
| `AIAgent` (`Tree`, `Blackboard`, `Memory`, `CurrentTarget`, `Pause/Resume/Restart`, `SetValue` diferido, `EvaluationInterval`, contadores, eventos `TargetChanged`, `EntityPerceived`, `EntityForgotten`, `TreeCompleted`) | MB | Experimental |
| `AIWorld` (`StepIndex`, `EmitStimulus`, registro, consultas de engajamento, contadores), `AIPerceivable`, `AIEngagementTarget`, `AIPatrolRoute`, `AITeam` | MB | Experimental |
| `BehaviourTreeInstance`, `Blackboard`, `PerceptionMemory` (leitura) | class | Experimental |
| `BehaviourNode`/`BehaviourNode<TMemory>`, `BehaviourDecorator`, `BehaviourService`, `BehaviourCondition`, `ISensor`, `ITargetSelector`, `ITargetConsideration`, `IUtilityConsideration`, `IAgentLocomotion`, `INavigationProvider`, `IAITeamSource`, `IDetectabilitySource`, `IAITimeSource` | extension points | Experimental |
| `NodeStatus`, `StopReason`, `AbortMode`, `ParallelPolicy`, `BlackboardKeyType`, `BlackboardKeyReference`, `AIEntityId`, `AIEntityHandle`, `AIStimulus`, `EngagementTokenHandle`, `PerceivedEntity` | struct/enum | Experimental |

Handles seguem a lição do M4/M5 (dono + vaga + geração; handle estrangeiro nunca age).

---

## 18. Qualidade

### 18.1 Estratégia de testes
**EditMode (domínio):** cada composite e decorator; aborts `Self`/`LowerPriority`/`Both` (ordem, um por tick,
`OnStop` folha → raiz, recomeço no mesmo tick); `Parallel` em todas as combinações de política; `Repeat` (um reinício
por tick), `Cooldown`, `TimeLimit` pelo relógio em 4 Δt; orçamento de tick; subárvores (Blackboard compatível, ciclo);
memória por nó isolada entre agentes; Blackboard (tipos, ids, herança, versões, fila diferida e saturação, handles
inválidos); seleção de alvo (curvas, histerese, desempate); memória de percepção (top-K embaralhado, esquecimento,
ameaça); engajamento (arbitragem, liberação, timeout, vagas); RNG determinístico; validators; modelo do Graph Editor
(criar, conectar, reordenar, apagar em cascata, duplicar, Undo/Redo).

**PlayMode (física real):** visão com obstáculo e ângulo 2.5D, audição, dano via estímulo, detectabilidade zero;
steering (chegada, separação, desvio, travamento); `CharacterAgentLocomotion` (MoveTo chega dentro da tolerância,
pulo/dash por borda); Combat (ação iniciada/terminada/expirada/rejeitada, guarda, estado do alvo); Abilities
(ativação/expiração, cooldown, tags do alvo); Stats (fuga abaixo de 30%); **independência de ordem** (criação de
agentes/percebíveis embaralhada → mesmo traço de comandos por passo); frame rate variável e `timeScale` 0,5 → mesmas
decisões por passo fixo; **zero GC** com 50 agentes. Padrão HFSM (IA → buffers → estados `External`) e troca
jogador ↔ IA no mesmo prefab: testes cross-package na Sandbox do host (`Assets/_FrameworkSandbox`), já que o AI não
depende da HFSM.

### 18.2 Critérios de aceite
| # | Critério |
|---|---|
| AC1 | Inimigo de exemplo faz Patrol/Chase/Attack/Retreat **só com assets**; nenhuma chamada a motor/combat/ASC fora dos adapters (asmdefs do domínio sem essas referências). |
| AC2 | Mesmo prefab e mesma `StateMachineDefinition` controlados pelo jogador e pela IA, trocando só a fonte; buffers limpos na troca. |
| AC3 | Semântica da BT (§5) coberta: composites, decorators, aborts na ordem normativa, `Parallel`, subárvores, orçamento. |
| AC4 | Determinismo (§13): ordem de criação embaralhada → mesmo traço de comandos (hash por passo, N passos); frame rate variável e `timeScale` 0,5 → mesmas decisões por passo fixo. |
| AC5 | Percepção: visão (ângulo, alcance, linha de visão), audição e dano por estímulo em *n + 1*, detectabilidade, esquecimento; saturação da memória e do barramento com top-K independente da ordem. |
| AC6 | Seleção de alvo com histerese e desempate por id; troca só acima da margem. |
| AC7 | Engajamento: 6 agentes com 2 tokens → exatamente 2 atacando; concessões independentes da ordem; tokens liberados em abort, morte, desabilitar e alvo perdido; vagas distintas. |
| AC8 | Integrações: Combat (ação e guarda, rastreamento por observação), Abilities (ativação e condições), Stats (condições), Character (locomoção) — sem alterar contratos desses packages. |
| AC9 | Zero GC em regime aquecido com 50 agentes; física `NonAlloc`; tempo por passo medido e registrado. |
| AC10 | Nenhum nome de jogo (Ryu, inimigos ou chefes do WOR) em código, assets do package ou samples. |
| AC11 | Ferramentas: modelo do grafo testado, debugger e gizmos validados, validators sem falso negativo; validação manual com mouse físico documentada (pendente do owner, como no M5). |
| AC12 | QG1–QG12 (QG12 em projeto vazio só com Core). |

### 18.3 Quality Gates
QG1–QG12 como nos milestones anteriores; validação das ferramentas com interação real × automação sintética.

---

## 19. Whiskers of Rage — como o jogo usará os mecanismos (sem código no package)

Fonte: GDD 2025 (tipos de mob: normal, rápido, longa distância, bruto, especial; chefes por bioma) e o design dos
milestones anteriores. Tudo abaixo é **asset ou glue do jogo**.

| Necessidade do jogo | Mecanismo genérico |
|---|---|
| Mob padrão (corpo a corpo com arma curta) | BT: alvo → token corpo a corpo → `MoveTo(vaga)` → `PerformCombatAction`; sem token, `Orbit` na borda do anel |
| Mob rápido ("ataca e espreita procurando oportunidade"; ataque pelas costas) | `Sequence` ataque → `Flee`/`KeepDistance`; condição "alvo atacando ou de costas" (`TargetCombatState`, `TargetFacingAway`); vaga com tag "costas" |
| Mob à distância (shurikens) | `KeepDistance` + linha de visão + `ActivateAbility` (projétil do Abilities) com cooldown; recua se o alvo se aproxima |
| Mob pesado (muita vida, lento, sem defesa) | Perfil de movimento lento (Character), ataques com armadura (Combat), BT sem guarda; prioridade alta de token |
| Hordas | Tokens e vagas no alvo do jogador; intervalos de avaliação maiores para mobs sem alvo |
| Chefes com fases (área, giro, rasante, lasers) | Ramos por fase com abort sobre a razão de vida (Stats); habilidades de área/projétil (Abilities); ataques do Combat |
| Chefe furtivo (invisibilidade, ataques furtivos) | Habilidade do chefe concede a tag de invisibilidade; a **apresentação** e a IA de aliados usam `OwnedTagDetectability` |
| Reação a parry/stun do jogador | Root com abort por `SelfCombatState.CanAct` falso → ramo de espera; bordas pendentes expiram no executor |
| Boss Rush | Mesmas BTs como subárvores, sem código |

---

## 20. Impacto em packages existentes (nada muda sem aprovação)

| Package | Mudança | Necessária? |
|---|---|---|
| Core 0.4.0 | Nenhuma: `ExecutionOrder.DECISION`, `GameplayTag`, `SelectImplementation`, validação | — |
| Core (futuro, com ADR) | Casca de grafo GraphView compartilhada em `Core.Editor` (3.º consumidor: HFSM, Combo, BT) e identidade de ator única | Não (D12, D5) |
| Stats 0.1.1 | Nenhuma | — |
| HFSM 0.1.0 | Nenhuma; nenhuma dependência nova | — |
| Character 0.2.0 | Nenhuma (`CharacterCommandBuffer`, `CharacterMotor`, `FacingController` bastam) | — |
| Combat 0.1.0 | Nenhuma (buffer, estado do `CombatTarget`/`CombatController` e eventos bastam) | — |
| Combat (futuro, opcional) | Token da borda nos args de `AttackStarted` para rastreamento direto | Não (observação cobre, R37) |
| Abilities 0.1.0 | Nenhuma (buffer, consultas, snapshot e eventos bastam) | — |

### 20.1 Matriz de dependências (sem mudança no grafo)
| Package | Camada | Hard | Opcional | Consumo pelo AI |
|---|---|---|---|---|
| ai | L5 | core | stats, character, combat, abilities | — |

| AI consome de | Contratos usados (nenhum alterado) |
|---|---|
| Core 0.4.0 | `ExecutionOrder.DECISION`, `GameplayTag`/`GameplayTagSet`, `SelectImplementation`, `IValidatable` |
| Stats 0.1.1 (integração) | `StatsComponent`, `StatCollection` (valores), `ResourcePool` (atual/máximo) |
| Character 0.2.0 (integração) | `CharacterCommandBuffer` (estado, bordas, `SetSource`, `Clear`), `CharacterMotor` (leitura), `FacingController` |
| Combat 0.1.0 (integração) | `CombatCommandBuffer`, `CombatController` (leitura, `CanExecuteAction`, eventos), `CombatTarget` (estado, perfil/time, `HitReceived`), `CombatantId` |
| Abilities 0.1.0 (integração) | `AbilityCommandBuffer`, `AbilitySystemComponent` (`FindAbilityByInputTag`, `CanActivateAbility`, cooldown, tags, snapshot, eventos), `ITeamRelation` |

---

## 21. Decisões propostas (para auditoria)

| # | Decisão | Recomendação | Alternativas |
|---|---|---|---|
| D1 | Loop e relógio | **`FixedUpdate`, banda `DECISION` com fases P0 (+0) / agentes (+20) / arbitragem (+60), intervalo em passos com escalonamento** (ADR-0026) | `Update` como no ADR-0003 original (decisões dependem do frame rate; testes por passo impossíveis) |
| D2 | Modelo de tick | **Caminho ativo persistido + aborts condicionais reavaliados a cada tick em ordem normativa (§5.3), um abort por tick, orçamento de nós** (ADR-0027) | Reavaliar da raiz a cada tick (reativo puro, mais caro e com reinícios); só eventos/observadores (UE; mais complexo de garantir) |
| D3 | Decorators e services | **Anexados aos nós** (menos nós, aborts claros, badges no grafo) | Nós decoradores de um filho |
| D4 | Blackboard | **Por agente, chaves tipadas por GUID, sem boxing, herança de definição, escrita só no tick (fila diferida)** (ADR-0028) | Blackboard compartilhado/esquadrão já no M6; dicionário de `object` |
| D5 | Identidade | **`AIEntityId` próprio (64 bits), espelhável do `CombatantId` pela integração** | Identidade única no Core (Core 0.5.0 + migração de Combat/Abilities; ADR) |
| D6 | Percepção | **Registro + snapshot P0 sem física para descoberta; física só para linha de visão; estímulos com ordem total em *n + 1*; memória top-K** (ADR-0029) | Overlap de física por sensor (camadas obrigatórias, saturação e ordem de colliders) |
| D7 | Movimento e navegação | **`IAgentLocomotion` no domínio + ações genéricas; Character por adaptador; `DirectNavigation` + desvio local; NavMesh fora** (ADR-0030) | Ações só nas integrações (duplicação por backend); NavMesh agora (muda o grafo) |
| D8 | Engajamento | **Tokens por pool e vagas no alvo, arbitrados na fase +60 em ordem total, no package** (ADR-0031) | Só no jogo (cada jogo reescreve; essencial em beat'em up) |
| D9 | HFSM | **Sem dependência; integração pelos buffers (ADR-0004); fases na própria BT** | Integração opcional `AI.Integration.HierarchicalStateMachine` (BT dentro de estado, condição "estado ativo"): exige ADR de grafo e acopla a IA a nomes de estado |
| D10 | Resultado de comandos | **Observação por token + eventos públicos dos executores, sem mudar contratos** (ADR-0032) | IA como dona `External` (conflita com a HFSM); mudar eventos do Combat/Abilities |
| D11 | Editor de grafo | **GraphView sobre modelo de edição** (gatilhos do Graph Toolkit não disparados, §15) | Graph Toolkit (sem `[SerializeReference]` nas opções) |
| D12 | Código de Editor compartilhado | **Não extrair no M6**; registrar a casca GraphView comum como candidata ao `Core.Editor` em minor futuro, com ADR | Extrair agora (muda o Core durante o M6, Core 0.4.0 ainda sem tag) |
| D13 | Utility | **`UtilitySelector` e `ScoredTargetSelector` com considerações SR e curvas** (escopo fechado) | BT pura (pontuação no jogo); sistema de utility completo |
| D14 | Samples | **AI Basics + AI Combat Arena** (§16) | Um sample só |
| D15 | Hitstop | **IA não pausa por padrão; `IAITimeSource` opcional** | Pausar sempre no hitstop do ator |
| D16 | `Parallel` | **Políticas explícitas; filhos restantes abortados ao decidir; sem "serviço de fundo" implícito** | Parallel simples (principal + fundo) |

---

## 22. ADRs (propostas na v1)
| # | Título |
|---|---|
| [0026](../adr/0026-ai-execution-and-timing.md) | Execução da IA: passo fixo na banda DECISION, fases e intervalos |
| [0027](../adr/0027-behaviour-tree-execution-model.md) | Modelo de execução da Behaviour Tree: caminho ativo, aborts e orçamento |
| [0028](../adr/0028-blackboard.md) | Blackboard tipado por agente com escrita diferida |
| [0029](../adr/0029-ai-perception.md) | Percepção por registro, snapshot e estímulos com ordem total |
| [0030](../adr/0030-agent-locomotion-and-navigation.md) | Contrato de locomoção do agente e navegação sem NavMesh |
| [0031](../adr/0031-engagement-coordination.md) | Coordenação de engajamento por tokens e vagas |
| [0032](../adr/0032-ai-command-output-and-tracking.md) | Saída da IA por command buffers e rastreamento por observação |

---

## 23. Riscos
| # | Risco | Mitigação |
|---|---|---|
| R33 | Escopo (BT + percepção + utility + navegação + coordenação) | Lista fechada da §1.1; entregas pequenas (§24); "adiado" explícito |
| R34 | Determinismo com física, eventos de outros packages e muitos agentes | Snapshot P0, escrita diferida, ordens totais, top-K, física só booleana/menor distância; AC4 |
| R35 | Custo de percepção com hordas (agentes × percebíveis) | Intervalos, escalonamento, `IdleInterval`, pré-filtro de distância; medição; grade espacial só com dado |
| R36 | Terceiro editor GraphView (R1) e código de Editor duplicado | Modelo de edição isolado (ADR-0001 da HFSM); gatilhos verificados; extração com ADR (D12) |
| R37 | Rastreamento por observação ambíguo (outra fonte enfileira a mesma ação) | Agente é a fonte exclusiva do buffer (`SetSource`); token + passo; testes; evento com token no Combat como evolução opcional |
| R38 | Navegação sem NavMesh (agente preso em geometria complexa) | Arenas abertas do gênero; desvio por sonda; detecção de travamento → `Failure`; NavMesh por ADR futuro |
| R39 | Muitos tipos `[SerializeReference]` (nós, decorators, sensores) | `[MovedFrom]`, tipos `sealed`, testes que carregam os assets dos samples (como R4/R25) |
| R40 | Latência de reação (1 passo) e intervalos maiores que 1 | Latências da §13 documentadas e medidas por `StepIndex`; intervalo 1 para agentes em combate |

---

## 24. Entregas (cada uma auditável, com testes e docs)

| # | Entrega | Conteúdo | Saída verificável |
|---|---|---|---|
| 1 | Fundação da BT e do Blackboard | Package pelo template; definições de árvore e Blackboard; layout compilado; instância com memória por nó; composites, decorators, services, aborts, subárvores, orçamento; `AIAgent` (fase +20, intervalo, escalonamento, RNG, fila diferida), `AIWorld` (passo, fases); ações/condições genéricas; validators de asset | EditMode; zero GC do domínio; AC3 |
| 2 | Percepção e alvo | `AIPerceivable`, registro e snapshot P0, times, detectabilidade, sensores, barramento de estímulos, memória top-K, `ScoredTargetSelector` com histerese | PhysX; AC5, AC6, AC4 (percepção) |
| 3 | Movimento e engajamento | `IAgentLocomotion`, steering, `INavigationProvider`, ações de locomoção, `AIPatrolRoute`, engajamento (tokens e vagas, fase +60) | PhysX com fixture de locomoção; AC7, AC4 (engajamento) |
| 4 | Integrações | Stats, Character, Combat, Abilities; testes cross-package do padrão HFSM e da troca jogador ↔ IA na Sandbox | AC1 (parcial), AC2, AC8 |
| 5 | Ferramentas | Graph Editor (modelo + vista), inspectors, AI Debugger, gizmos, validators de cena | Validação de ferramentas documentada; AC11 |
| 6 | Samples e fechamento | 2 samples, regressão completa, zero GC (AC9), QG1–QG12, docs, auditoria | AC1–AC12 |

Cada entrega termina com: suítes do package e do WOR verdes, QG12, `check_dependencies`, commits e relatório de
desvios. Nenhuma tag antes da auditoria final do M6; nenhuma alteração em packages existentes sem decisão aprovada.
