# M6 — AI / Behaviour Trees · Especificação técnica

- **Status:** **v2 — aprovada para implementação** (arquitetura geral aprovada na auditoria do v1; ajustes 1–5 e
  refinamentos incorporados como contratos normativos nas seções indicadas na §25; ADRs 0026–0032 aceitas) ·
  **Data:** 2026-10-13
- **Histórico:** v1 (proposta, WOR `5a0ffee`) → v2 (revisão parcial formal dos ADRs 0003/0004, BT com ramos
  paralelos e orçamento de trabalho, escopo de engajamento, rastreamento de comandos por token, saturação e
  identidades)
- **Unity:** 6000.6.5f1 · **Package:** `com.ramirestechgames.ai` (L5, `RamiresTechGames.AI`, define
  `RAMIRESTECHGAMES_AI`)
- **Dependências (grafo, sem mudança):** hard `core`; opcionais `stats` (ADR-0009), `character`, `combat`,
  `abilities` (ADR-0002). **HFSM não entra no grafo do AI** (ADR-0004); a integração com a HFSM é exclusivamente pelos
  command buffers (§12.5). Módulo nativo `com.unity.modules.physics` (linha de visão e desvio).
- **Base contratual:** Core 0.4.0, Stats 0.1.1, HFSM 0.1.0, Character 0.2.0, Combat 0.1.0, Abilities 0.1.0
  (candidatas, sem tags). **Nenhuma API pública de package existente é alterada**; pontos que só seriam resolvidos
  com mudança em outro package estão na §20.2, com a alternativa adotada.
- **Fora do package:** comportamento de personagens de um jogo. No Whiskers of Rage, inimigos, chefes e qualquer IA
  de aliado são **assets e glue do jogo** (§19). Nenhum nome de personagem (Ryu ou outro) em código, assets ou samples.

---

## 1. Objetivo e escopo

Um **sistema de decisão genérico, data-driven e determinístico por passo fixo** para atores controlados por IA, em
escala indie: Behaviour Trees como assets, Blackboard tipado por agente, sensores e memória de percepção, seleção de
alvo por pontuação, coordenação de engajamento, movimento por um contrato de locomoção e **saída exclusivamente por
command buffers** dos packages de execução. A BT **decide**; HFSM, Character, Combat e Abilities **executam**
(ADR-0004). O mesmo prefab funciona com jogador ou IA trocando só a fonte de comandos.

### 1.1 Escopo do M6 (fechado)

| Obrigatório no M6 | Adiado (fora do M6) |
|---|---|
| `BehaviourTreeDefinition` (nós SR, subárvores), instância por agente com conjunto ativo e memória por nó | GOAP, HTN, planejadores, ML |
| Composites (Sequence, Selector, Parallel com políticas explícitas, Random e Utility determinísticos), decorators, services, aborts | NavMesh (`com.unity.ai.navigation`) — exige ADR de grafo |
| `BlackboardDefinition`/`Blackboard` com escrita externa coalescida por chave | Blackboard compartilhado de esquadrão, formações, cobertura, crowd |
| `AIAgent`, `AIWorld` (registro, snapshot P0, estímulos, arbitragem), identidades e handles | Salvar/carregar estado de IA; rede/rollback (R14) |
| Sensores (visão 2.5D com linha de visão, proximidade, audição, estímulos), caixa de estímulos por agente, memória top-K, detectabilidade | Pausa da IA por hitstop como padrão (opcional, `IAITimeSource`) |
| `ScoredTargetSelector` com histerese; `UtilitySelector` com escopo fechado | Editor dedicado de curvas de utility |
| Escopo de engajamento (tokens e vagas) com handles | Animação/VFX/áudio de IA (só eventos) |
| `IAgentLocomotion` + ações de movimento no plano XZ, `DirectNavigation` e desvio local | Depurador remoto/em build |
| Saída por command buffers com rastreamento por token (três caminhos de execução, §10) | Retirada de borda individual pela fonte (exigiria API nova nos buffers, §20.2) |
| Integrações Stats, Character, Combat, Abilities; padrão HFSM pelos buffers | |
| Graph Editor (GraphView sobre modelo de edição), AI Debugger, gizmos, validators; samples AI Basics e AI Combat Arena | |

---

## 2. Conceitos

| Termo | Definição |
|---|---|
| **Agente** | Ator controlado por IA: `AIAgent` + BT + Blackboard + sensores + memória + caixa de estímulos. Também é um percebível. |
| **Percebível** | `AIPerceivable`: algo detectável, com `AIEntityId`, time, pontos de mira, facing e detectabilidade. |
| **`AIEntityId`** | Identidade lógica de 64 bits, estável e única por `AIWorld`; chave de todas as ordens totais. |
| **`AIEntityHandle`** | Referência de runtime a um percebível: (`WorldId`, vaga, geração). Inválido quando o percebível sai ou é de outro mundo. |
| **Nó / ativação** | `BehaviourNode` com `OnStart`/`OnTick`/`OnStop`; uma **ativação** vai de `OnStart` até `OnStop`. |
| **Conjunto ativo** | Nós com ativação aberta. Sem `Parallel` é um caminho; com `Parallel`, uma subárvore com vários ramos ativos. |
| **Abort condicional** | Decorator de condição com `Self`, `LowerPriority` ou `Both` (§5.4). |
| **Unidade de trabalho** | Operação contada no orçamento do tick: iniciar, tickar ou parar nó, avaliar condição, executar service (§5.6). |
| **Canal de saída** | Destino de comando com um escritor por tick: movimento, facing, borda de ação de combate, guarda, borda de habilidade por tag, segurar habilidade por tag, pulo, dash (§5.7). |
| **Estímulo** | Ocorrência pontual publicada no `AIWorld` e entregue na caixa de estímulos dos agentes interessados. |
| **Escopo de engajamento** | Nó que pede, aguarda e retém um token (e vaga) enquanto o filho executa (§9). |
| **Rastreio de comando** | Registro de uma borda enviada (token, canal, instante) e do seu desfecho (`CommandOutcome`, §10). |

---

## 3. Arquitetura

### 3.1 Camadas e assemblies

| Camada | Assembly | Conteúdo |
|---|---|---|
| Domain | `RamiresTechGames.AI` (`Runtime/Trees`, `Runtime/Blackboards`, `Runtime/Perception`, `Runtime/Selection`, `Runtime/Engagement`, `Runtime/Steering`, `Runtime/Commands`) | `BehaviourTreeInstance`, `TreeLayout`, `Blackboard`, `StimulusInbox`, `PerceptionMemory`, `TargetScoring`, `EngagementArbiter`, `SteeringModel`, `CommandTracker` — puros, recebem Δt e passo |
| Unity Integration | `RamiresTechGames.AI` (`Runtime/Components`, `Runtime/Definitions`) | `AIAgent`, `AIWorld` (+ helpers de fase ocultos), `AIPerceivable`, `AIPatrolRoute`, `AIEngagementTarget`, `AITeam`, definições (SO) |
| Contratos de extensão | `RamiresTechGames.AI` (`Runtime/Extensibility`) | `BehaviourNode`/`BehaviourNode<TMemory>`, `BehaviourDecorator`, `BehaviourService`, `BehaviourCondition`, `ISensor`, `ITargetSelector`, `ITargetConsideration`, `IUtilityConsideration`, `IAgentLocomotion`, `INavigationProvider`, `IAITeamSource`, `IDetectabilitySource`, `IAITimeSource`, `IAgentCommandBridge` |
| Integration | `RamiresTechGames.AI.Integration.Stats` | Condições e considerações sobre stats/recursos (ADR-0009) |
| Integration | `RamiresTechGames.AI.Integration.Character` | `CharacterAgentLocomotion` (`IAgentLocomotion` + rastreio de pulo/dash), condições |
| Integration | `RamiresTechGames.AI.Integration.Combat` | `CombatAgentBridge`, ações `PerformCombatAction`/`HoldGuard`, condições, estímulo de dano, time |
| Integration | `RamiresTechGames.AI.Integration.Abilities` | `AbilityAgentBridge`, ações `ActivateAbility`/`HoldAbilityInput`, condições, detectabilidade por tags, estímulos de Gameplay Events, time |
| Editor | `RamiresTechGames.AI.Editor` | Graph Editor (GraphView sobre `BehaviourTreeGraphModel`), inspectors, AI Debugger, gizmos, validators |

O domínio **não referencia** Character, Combat, Abilities, Stats nem HFSM. Cada integration assembly (e a assembly de
testes correspondente) tem `defineConstraints`/`versionDefines` próprios (ADR-0002): com **somente o Core** instalado
nenhuma delas compila e o QG12 roda apenas o domínio e o Editor do AI (refinamento da auditoria; AC12).

### 3.2 Componentes de runtime

| Componente | Responsabilidade |
|---|---|
| `AIWorld` | Um por cena, **componente explícito** (sem singleton estático). `WorldId` de sessão, contador de passos, registro de percebíveis (capacidade declarada), snapshot P0, barramento e entrega de estímulos, arbitragem de engajamento. |
| `AIAgent` | BT, Blackboard, sensores (SR), memória, caixa de estímulos, seletor de alvo, RNG, fila de escritas externas coalescida, bridges de comando, intervalo/escalonamento. Fase `DECISION + 20`. |
| `AIPerceivable` | `AIEntityId` (serializado ou atribuído antes de habilitar), time, pontos de mira, detectabilidade, facing. Registro no `AIWorld` ao habilitar. |
| `AIEngagementTarget` | Opcional no alvo: `EngagementProfileDefinition` (pools de tokens, anel de vagas). |
| `AIPatrolRoute` | Pontos de patrulha (loop/ida-e-volta), gizmos. |
| `CharacterAgentLocomotion`, `CombatAgentBridge`, `AbilityAgentBridge` (integrações) | Escrevem nos buffers, mantêm os rastreios de comando (§10) e registram eventos públicos dos executores em anéis de capacidade fixa. |

### 3.3 Fluxo de um passo fixo (ADR-0026)

```
FixedUpdate do passo n
DECISION        (−900) AIWorld   P0: passo++; snapshot dos percebíveis (pose, time, detectabilidade, flags);
                                 estímulos emitidos em n−1 → top-K global → caixas de estímulos dos agentes
DECISION + 20   (−880) AIAgent   para cada agente com avaliação devida neste passo:
                                 escritas externas coalescidas → bridges (rastreios, eventos) → sensores devidos
                                 (+ caixa de estímulos acumulada) → memória → services → tick da BT (orçamento) →
                                 saídas: Blackboard, canais de comando (bordas e estado), pedidos de engajamento,
                                 estímulos (n+1)
DECISION + 60   (−840) AIWorld   arbitragem de engajamento (ordem total) → concessões/revogações no tick seguinte
STATE_MACHINE   (−800)           HFSM com EvaluationLoop = FixedUpdate: claim das bordas da IA no mesmo passo
ABILITIES … COMBAT … CHARACTER   executores consomem as bordas no mesmo passo n
```

Regras de determinismo (§13): um agente só **escreve** no próprio estado, nos próprios canais (pelos bridges), na fila
de pedidos e no barramento; só **lê** outros atores pelo snapshot P0 ou por estado somente-leitura dos packages, que
na banda `DECISION` reflete o fim do passo *n − 1*; tudo que um agente produz para outros vale na fase +60 ou no passo
seguinte; toda lista tem ordem total e saturação explícita com diagnóstico.

---

## 4. Definições (ScriptableObjects, imutáveis em runtime)

### 4.1 `BehaviourTreeDefinition`
| Campo | Conteúdo |
|---|---|
| `Blackboard` | `BlackboardDefinition` exigido. |
| `Nodes` | Lista plana de `NodeData` (`Id` GUID, `ParentId`, `ChildOrder`, `Name`, `Node` SR, `Decorators` SR, `Services` SR, `EditorPosition`, `Comment`). Raiz única. |
| `Subtrees` | `SubtreeNode` referencia outra definição com o mesmo Blackboard ou um ancestral; ciclo = erro do validator e nó sem filhos em runtime. |
| `TickBudget` | Unidades de trabalho por tick (padrão 256, §5.6). |
| `RestartPolicy` | `RestartNextTick` (padrão) ou `StopOnComplete` quando a raiz termina. |

Compilada uma vez (`TreeLayout`: índices em pré-ordem, filhos contíguos, decorators/services, canais declarados,
chaves resolvidas) e compartilhada entre agentes.

### 4.2 `BlackboardDefinition`
Chaves `{Id (GUID), Name, Type, Default, Description}`; tipos `Bool`, `Int`, `Float`, `Vector3`, `GameplayTag`,
`Entity` (`AIEntityHandle`) e `Object` (referência Unity com tipo esperado). `Parent` opcional (herança; ciclo = erro;
nomes únicos na cadeia). Referências por id (`BlackboardKeyReference` com cache).

### 4.3 Outras definições
| SO | Conteúdo |
|---|---|
| `SensorProfileDefinition` | Sensores SR, `MemoryCapacity` (padrão 16), `ForgetTime`, `StimulusInboxCapacity` (padrão 16). |
| `EngagementProfileDefinition` | Pools de tokens por `GameplayTag` (capacidade, `MaxHoldTime`), anel de vagas (quantidade, raio, ângulos no frame do facing do alvo, tags de preferência). |
| `TargetSelectionDefinition` | Considerações SR com pesos e curvas, `SwitchMargin`, filtros. |

---

## 5. Behaviour Tree (ADR-0027) — normativo

### 5.1 Ciclo de vida por ativação
1. `OnStart` exatamente **uma vez** por ativação, antes de qualquer `OnTick` dela.
2. `OnTick` **no máximo uma vez por tick da árvore** e só entre `OnStart` e `OnStop`.
3. `OnStop(reason)` exatamente **uma vez** por ativação, com `StopReason` ∈ {`Completed`, `Aborted`, `TreeStopped`},
   inclusive quando a ativação é abortada antes do primeiro `OnTick`. Paradas em cascata vão **da folha para a raiz**;
   entre irmãos ativos de um `Parallel`, em ordem reversa de declaração.
4. Um nó pode completar no tick em que começou; o pai continua no mesmo tick, dentro do orçamento.
5. Exceção em `OnStart`/`OnTick` vale `Failure` (logada com o id do nó); exceção em `OnStop` é logada e a parada
   conta como feita. Memória por agente e por nó criada uma vez e reiniciada por `OnStart`.

### 5.2 Conjunto ativo e tick
Cada nó guarda `IsActive`, `LastStatus` e, nos composites, o cursor (filho atual) e, no `Parallel`, o resultado de
cada filho. O tick da árvore:
1. **Fase de aborts** (§5.4): avalia as condições observadas do conjunto ativo; aplica no máximo um abort.
2. **Fase de execução**: percorre o conjunto ativo em **pré-ordem** (pais antes dos filhos, filhos em ordem de
   declaração); cada folha ativa recebe `OnTick`; resultados sobem aos pais, que avançam o cursor e podem iniciar o
   próximo filho no mesmo tick.
3. **Fase de services** devidos dos composites ativos (antes da execução dos filhos, na mesma pré-ordem).

### 5.3 Composites
| Composite | Semântica |
|---|---|
| `Sequence` | Filhos em ordem; `Failure` no primeiro `Failure`; `Success` quando todos terminam. |
| `Selector` | Filhos em ordem de prioridade; `Success` no primeiro `Success`; `Failure` quando todos falham. Único pai válido para aborts `LowerPriority/Both`. |
| `Parallel` | Todos os filhos iniciam na ativação e ficam **ativos simultaneamente**, cada um com estado e memória próprios; filho que termina guarda o resultado e não reinicia na mesma ativação. Decisão após a execução de todos os filhos no tick: `SuccessPolicy`/`FailurePolicy` ∈ {`RequireOne`, `RequireAll`}; se as duas forem satisfeitas no mesmo tick, vale `ConflictPolicy` ∈ {`FailureWins` (padrão), `SuccessWins`}; ao decidir, os filhos ainda ativos param com `Aborted`. Abort `Self` de um filho conta como `Failure` dele. |
| `RandomSelector`/`RandomSequence` | Ordem embaralhada com pesos na ativação, pela sequência aleatória do agente. Não aceitam `LowerPriority` nos filhos. |
| `UtilitySelector` | Maior pontuação (`IUtilityConsideration`), empate pela ordem declarada; avaliado na ativação. Não aceita `LowerPriority` nos filhos. |
| `SubtreeNode` | Executa outra definição como filho; o conjunto ativo atravessa a subárvore. |
| `EngagementScope` | Nó de um filho que pede, aguarda e retém token/vaga (§9). |

### 5.4 Decorators e aborts
Decorators anexados: `ConditionDecorator` (`BehaviourCondition` + `Abort` + `Invert`), `BlackboardDecorator`,
`Cooldown`, `TimeLimit`, `Repeat` (no máximo **um reinício por tick**), `Inverter`, `ForceResult`. Um decorator de
condição falso na entrada impede a ativação (`Failure`, sem `OnStart` do nó).

| Abort | Regra |
|---|---|
| `Self` | Enquanto o nó decorado está ativo, a condição é reavaliada a cada tick; ao ficar falsa, o nó (e sua subárvore) para com `Aborted` e o pai recebe `Failure` dele. |
| `LowerPriority` | Enquanto um irmão **de menor prioridade** (filho posterior do mesmo `Selector`, ou descendente dele) está ativo, a condição do nó decorado é reavaliada; ao ficar verdadeira, o ramo inferior para com `Aborted` e o `Selector` ativa o nó decorado no mesmo tick. |
| `Both` | As duas regras. |

**Ordem e limites (normativos):**
1. Candidatos a abort avaliados em pré-ordem do conjunto ativo; para cada `Selector` ativo, os decorators
   `LowerPriority/Both` dos filhos anteriores ao cursor, em ordem; para cada nó ativo, os `Self/Both`.
2. O **primeiro** candidato verdadeiro vence; **no máximo um abort por tick**. As demais condições não são avaliadas
   naquele tick.
3. Após o abort, a execução recomeça no `Selector` (LowerPriority) ou no pai (Self) **no mesmo tick**; nós ativados
   nesse recomeço só são candidatos a abort a partir do **tick seguinte** — sem cascata de reinícios no mesmo tick.
4. Condições de abort são somente-leitura (não escrevem Blackboard nem canais).

### 5.5 Services
`BehaviourService` anexado a composite: `Interval` em segundos convertido em passos, primeira execução na ativação
(configurável), executa antes dos filhos do composite; escreve só no Blackboard do agente.

### 5.6 Orçamento de trabalho
1. `TickBudget` limita **unidades de trabalho**: cada `OnStart`, `OnTick`, avaliação de condição (decorator, folha
   de condição), execução de service e `OnStop` custa 1.
2. A fase de aborts e as **paradas em cascata** são atômicas: são contadas, mas nunca interrompidas pelo orçamento
   (ambas são limitadas pelo tamanho do conjunto ativo).
3. Esgotado o orçamento, o tick termina **só entre operações**: nenhum `OnStart`/`OnStop` fica pela metade; nós
   iniciados ficam ativos; cursores permanecem onde estão; filhos de `Parallel` não tickados neste tick serão tickados
   no próximo, na ordem normal. O estado é idêntico ao de um tick interrompido naquele ponto por qualquer outra razão.
4. Diagnósticos: `BudgetExceededCount`, `LastTickWork`, nó em que parou (debugger).

### 5.7 Canais de saída e conflitos
1. Cada ação declara os canais que escreve. Por tick, o **primeiro escritor em pré-ordem** reivindica o canal; escrita
   posterior no mesmo canal e tick é recusada (`OutputConflictCount`) e a ação recusada fica `Running` e tenta no tick
   seguinte (ou falha, se `FailOnConflict`).
2. Canais de estado (movimento, facing, guarda, segurar habilidade) persistem entre ticks com o último valor do
   detentor; quando a ação detentora para, ela devolve o canal ao neutro **se ainda for a detentora**.
3. O validator avisa quando dois ramos de um mesmo `Parallel` declaram o mesmo canal.
4. Escritas no Blackboard dentro do tick são sequenciais em pré-ordem (a última vence, determinístico).

### 5.8 Reinício e parada
`RestartNextTick`: quando a raiz termina, `TreeCompleted` é notificado e a árvore reinicia no tick seguinte (nunca no
mesmo). `AIAgent.Restart()` e `Stop()` param o conjunto ativo (`TreeStopped`, folha → raiz), liberam escopos de
engajamento e canais, resolvem rastreios (§10.4) e reiniciam memórias; o Blackboard só é reiniciado com
`ResetBlackboardOnRestart`.

### 5.9 Ações e condições genéricas (domínio)
- Ações: `Wait`, `SetBlackboardValue`, `ClearBlackboardValue`, `CopyBlackboardValue`, `SelectTarget`, `EmitStimulus`,
  `Fail`/`Succeed`, `Log` (Editor). Engajamento só por `EngagementScope` (§9).
- Locomoção (§11): `MoveTo` (entidade, posição, rota, **vaga do escopo**), `Patrol`, `Chase`, `KeepDistance`, `Flee`,
  `Orbit`, `Wander`, `FaceTarget`, `Stop`, `Jump`, `Dash`.
- Condições: comparação de chaves, entidade válida/hostil/percebida, distância e ângulo, linha de visão, tempo desde
  percepção, chance, token concedido e vaga atribuída (no escopo mais próximo).

---

## 6. Blackboard (ADR-0028) — normativo

1. Arrays tipados por tipo, **sem boxing**; acesso por `BlackboardKeyReference` resolvida; `Version` por chave.
2. Durante o tick do agente, escritas imediatas e sequenciais (§5.7.4).
3. **Escritas externas** (`AIAgent.SetValue(key, value[, priority])`, de scripts, ferramentas ou glue) são
   **coalescidas por chave**: a fila guarda no máximo uma escrita pendente por chave (capacidade = número de chaves, a
   fila **não satura**); entre escritas da mesma chave no mesmo intervalo, vence a de maior `priority`; empate → a
   chamada mais recente. Aplicadas em ordem de índice da chave no início do próximo tick avaliado. Escritas de
   fontes cuja ordem de chamada não é determinística (ex.: `Update` de scripts diferentes) só são determinísticas com
   prioridades distintas — documentado; bridges de integração usam estímulos, não escritas externas.
4. `Entity` guarda `AIEntityHandle`; percebível que sai lê como inválido.
5. Sem Blackboard compartilhado no M6.

---

## 7. Percepção (ADR-0029) — normativo

### 7.1 Identidades e handles
1. `AIEntityId` (64 bits, `0` inválido): `SerializedEntityId` gerado pelo Editor (único por cena, validator) ou
   atribuído por código **antes de habilitar** (`AssignEntityId`, com `Combine(spawnIndex)` para clones de prefab, como
   `CombatantId`); integração pode espelhar o `CombatantId`.
2. **Duplicata ou id inválido no registro → registro recusado**, erro logado uma vez e `DuplicateIdCount`/
   `InvalidIdCount`; nunca renomeação automática (dependeria da ordem de habilitação). Clones de prefab sem id
   atribuído caem nessa regra.
3. `AIEntityHandle` = (`WorldId`, vaga, geração). `WorldId` é único por sessão; handle de outro mundo, de vaga
   reciclada ou de percebível que saiu é **inválido** e nunca resolve outro percebível.

### 7.2 Registro e snapshot P0
1. Capacidade **declarada** (`AIWorld.RegistryCapacity`, padrão 256). Registro com o registro cheio é **recusado**,
   contado (`RegistrySaturationCount`) e logado; o validator de cena acusa cenas com mais percebíveis que a capacidade.
   Garantia de independência de ordem **somente até a capacidade**; acima dela, quem fica de fora depende da ordem de
   habilitação (limite declarado, sem garantia).
2. P0 copia, em ordem de `AIEntityId`, pose, pontos de mira, facing plano, time e máscara hostil, detectabilidade
   por sentido e flags das integrações. A descoberta não usa física; a física só responde **linha de visão**
   (booleano).

### 7.3 Estímulos
1. `AIWorld.EmitStimulus(in AIStimulus)` — `Tag`, `Position`, `Radius`, `Strength`, `Source`/`Target` (`AIEntityId`),
   `Magnitude`. Cada emissão recebe (passo, `Source`, sequência por fonte); emissões sem fonte usam `Source = 0` e
   sequência global (ordem de chamada, documentada).
2. Em P0 de *n + 1*, os estímulos de *n* passam pela **seleção top-K global** (`StimulusCapacity`, padrão 128) pela
   chave de **seleção** (força ↓, `Source` ↑, sequência ↑); excedentes contados (`StimulusOverflowCount`).
3. Cada estímulo mantido é entregue à **caixa de estímulos** de cada agente interessado (alvo direto ou dentro do
   alcance máximo dos sensores de estímulo do agente), percorrendo agentes por `AIEntityId`.
4. A caixa é **por agente** e acumula entre avaliações: estímulos emitidos durante passos em que o agente não avalia
   (intervalo > 1) **não se perdem**; ela tem capacidade fixa (padrão 16) com top-K pela mesma chave de seleção e
   contagem (`InboxOverflowCount`).
5. Na avaliação, o agente consome a caixa em **ordem de entrega** (passo ↑, `Source` ↑, sequência ↑) — cronológica —,
   independente da ordem de seleção.

### 7.4 Sensores e memória
1. Sensores SR (`Sight` 2.5D com linha de visão e `DetectionDelay` opcional, `Proximity`, `Hearing` e `Stimulus` pela
   caixa) com `Interval` em passos escalonado por `AIEntityId`.
2. Memória por agente com capacidade fixa (padrão 16): ao atualizar, todos os candidatos (memória atual + percebidos
   agora) são avaliados e mantém-se o top-K por (percebido agora ↓, hostil ↓, ameaça ↓, última percepção ↓, distância
   ↑, `AIEntityId` ↑), **independente da ordem de chegada**; excedentes contados (`MemoryOverflowCount`);
   esquecimento após `ForgetTime`.
3. `IDetectabilitySource` (multiplicador por sentido; 0 = invisível) e `IAITeamSource` no percebível.
4. Invariância: com a mesma cena e criação de atores embaralhada (dentro das capacidades), snapshot, caixas, memória e
   alvo selecionado são idênticos passo a passo (AC5).

---

## 8. Seleção de alvo

`ScoredTargetSelector`: score = Σ peso·curva(consideração) / Σ peso; considerações `Distance`, `AngleFromFacing`,
`Threat`, `Recency`, `IsCurrentTarget`, `AttackersOnTarget`, `TargetFacingAway`, mais `TargetResourceRatio` (Stats) e
`TargetHasTags` (Abilities). Desempate por `AIEntityId`. Histerese: troca só se outro superar por `SwitchMargin` ou se
o atual ficar inválido/esquecido. **Troca de alvo** é publicada como mudança da chave de alvo (consumida pelo escopo
de engajamento, §9).

---

## 9. Engajamento (ADR-0031) — normativo

### 9.1 Pedido, concessão e retenção
O engajamento tem três etapas separadas, todas dentro do nó `EngagementScope` (um filho):

| Etapa | Regra |
|---|---|
| **Pedido** | Na ativação, o escopo pede um token do pool (`PoolTag`) do alvo lido de uma chave `Entity`, com prioridade e, opcionalmente, uma vaga (`SlotPreference`). Enquanto pendente, o escopo fica `Running` **sem ativar o filho**; `WaitTimeout` opcional → `Failure` (pedido retirado). |
| **Concessão** | Na fase +60, o arbitro concede ou não; a concessão é vista no tick seguinte do agente, que então ativa o filho. |
| **Retenção** | O token (e a vaga) ficam retidos **durante toda a ativação do filho** — aproximação, ataque e recuperação. Ações descendentes (ex.: `MoveTo` vaga, `HasEngagementToken`) consultam o escopo ancestral mais próximo. |

**Liberação** (exatamente uma vez por concessão): filho termina (`Success`/`Failure`); escopo abortado ou parado
(`Aborted`, `TreeStopped`); `MaxHoldTime` do pool (revogação → filho abortado no tick seguinte, escopo `Failure`);
troca do alvo na chave observada (abort do escopo); alvo inválido, morto ou desregistrado; agente desabilitado,
pausado, sem controle (§10.5) ou reiniciado. Nenhuma ação "liberar token" avulsa existe: o token nunca é liberado
enquanto o filho que o usa está ativo.

### 9.2 Arbitragem (fase `DECISION + 60`)
1. Alvos processados por `AIEntityId`; por alvo e pool: liberações e revogações primeiro, depois pedidos ordenados
   por (prioridade ↓, score ↓, `AIEntityId` do agente ↑); score padrão = −distância no snapshot P0.
2. Detentores não são preemptados no M6.
3. Vagas: atribuição gulosa em ordem total (pedidos por prioridade ↓, id ↑; vaga livre compatível mais próxima,
   índice ↑); a posição da vaga é recalculada a cada passo pela pose do alvo no P0.
4. Capacidades fixas (pedidos por passo, detentores, vagas) com saturação contada; pedido além da capacidade é
   recusado e o escopo vê `Denied` (falha ou nova tentativa, configurável).

### 9.3 Handles
`EngagementTokenHandle` = (dono: `WorldId` + `AIEntityId` do alvo + pool, vaga, geração). Handle estrangeiro,
antigo ou de vaga reciclada nunca libera nem consulta o token de outro agente.

---

## 10. Saída de comandos e rastreamento (ADR-0032) — normativo

### 10.1 Escrita
A IA escreve **somente** estado e bordas nos buffers (`CharacterCommandBuffer`, `CombatCommandBuffer`,
`AbilityCommandBuffer`) pelos bridges das integration assemblies. Ao assumir o controle, o agente chama
`SetSource(agent)` em cada buffer (que limpa o buffer, ADR-0004); a IA **nunca** chama `TryConsume` (garantia 4 do
ADR-0004).

### 10.2 Caminhos de execução
Cada bridge declara o caminho do seu executor, verificado em runtime (diagnóstico se não bater):

| Caminho | Quem consome | Configuração do executor |
|---|---|---|
| `HfsmOwner` | Estado da HFSM (claim → `RequestAction`/`RequestActivation`/`RequestJump/Dash`) | posse `External` |
| `AgentOwner` | O próprio bridge chama `RequestAction(token)`/`RequestActivation(token)`/`RequestJump/Dash(token)` | posse `External`, **sem HFSM** reivindicando o mesmo buffer |
| `PackageOwner` | Combo runner, ASC ou motor | posse do package |

### 10.3 Correlação (sem presumir que um evento corresponde à última borda)

| Caminho | Início (borda → execução) | Término |
|---|---|---|
| `HfsmOwner` / `AgentOwner`, Combat | `ActionResolved` com `Request.Token` = token rastreado: `Executed` → iniciado; `Rejected` → `Rejected`; `Cancelled` → `Cancelled`. Instância do ataque: o `AttackStarted` do mesmo controller, com `Action` igual, entregue no mesmo passo **antes** do `Resolved(Executed)` (ordem observada no Combat 0.1.0, verificada por teste de contrato, §20.2) | `AttackEnded` com a mesma `Instance`: `Completed` ou `Interrupted` conforme `AttackEndReason` |
| `HfsmOwner` / `AgentOwner`, Abilities | `ActivationResolved` com `Request.Token`: `Activated` → iniciado (handle no request; geração pelo `AbilityActivated` do mesmo handle no mesmo passo); `Expired`/`Cancelled` → idem | `AbilityEnded` com o mesmo (handle, geração): `Completed`/`Interrupted`/`Cancelled` por `AbilityEndReason` |
| `HfsmOwner` / `AgentOwner`, Character | `ActionResolved` do motor com `Request.Token` | Pulo: instantâneo (`Completed` ao executar); dash: `DashEnded` |
| `PackageOwner` (todos) | **Uma borda rastreada pendente por buffer** (pedidos seguintes esperam no bridge, `Running`). Token ausente + **exatamente um** evento de início compatível desde o envio (`AttackStarted` com a `Action`; `AbilityActivated` de concessão com a mesma tag de entrada; `Jumped`/`DashStarted` com `FromBuffer`) → iniciado, instância/geração do evento. Token ausente, nenhum evento e idade da borda ≥ janela do consumidor (`InputBufferTime` do perfil de combate ou do ASC, `JumpBufferTime`/`DashBufferTime`) → `Expired`. Qualquer outro caso (nenhum evento antes da janela, mais de um evento compatível) → **`Unconfirmed`** | Como acima, pela instância/geração do evento de início |

Duas bordas iguais: nos caminhos `HfsmOwner`/`AgentOwner` cada uma é correlacionada pelo seu token (prioridades e
ordem de consumo vêm do executor); no `PackageOwner` são serializadas (a segunda só é enviada após o desfecho da
primeira). `Unconfirmed` vale `Failure` por padrão (configurável) e é contado.

### 10.4 Desfechos, tempo limite e abandono
`CommandOutcome` ∈ {`Pending`, `Executing`, `Completed`, `Expired`, `Rejected`, `Cancelled`, `Interrupted`,
`Unconfirmed`, `TimedOut`}; `CompleteOn` = `Started` ou `Ended` define quando a ação vira `Success`. `MaxWait` sem
desfecho → `TimedOut`. Quando a ação é abortada/parada com rastreio aberto:
- `AgentOwner`: o bridge chama `CancelActionRequest`/`CancelActivationRequest`/`CancelJumpRequest`/`CancelDashRequest`
  (descarte garantido pelo executor) → `Cancelled`.
- `HfsmOwner`/`PackageOwner`: a IA **não pode** retirar uma borda individual sem consumi-la (ADR-0004). A borda é
  **abandonada**: deixa de ser rastreada e expira pela janela do consumidor, podendo ainda executar dentro dela
  (limitação documentada, R41). Opção `OnAbandon = ClearChannel` limpa as bordas de ação daquele buffer
  (`ClearActions`/`Clear`), permitido porque o agente é a fonte exclusiva; todos os rastreios daquele buffer viram
  `Cancelled`.
- Execução já iniciada (ataque/habilidade) **não** é interrompida pela IA; o rastreio é abandonado e o fim é ignorado.

### 10.5 Troca jogador ↔ IA
1. O bridge confere `Source` a cada tick; se outra fonte assumiu (`SetSource` limpou as bordas da IA), todos os
   rastreios abertos viram `Cancelled (SourceChanged)`, os canais de estado não são mais escritos e o agente entra em
   **sem controle** (escopos liberados, BT pausada ou parada, configurável).
2. `AIAgent.TakeControl()` chama `SetSource(agent)` em todos os buffers (limpando bordas do jogador) e retoma.
3. O validator e um diagnóstico de runtime acusam duas fontes ativas escrevendo no mesmo buffer.

---

## 11. Movimento e navegação (ADR-0030)

- `IAgentLocomotion`: leitura `Position`, `Velocity`, `PlanarFacing`, `IsGrounded`, `CanMove`, `MaxSpeed`; escrita
  `SetMove(Vector2 planar XZ, |v| ≤ 1)`, `Stop()`, `SetFacing(dir)`, `ClearFacing()`, `RequestJump()`,
  `RequestDash(dir)` (rastreados como em §10). O Character o implementa por `CharacterAgentLocomotion`; o domínio é
  testado com fixture.
- `SteeringModel` puro: seek/arrive, separação pelo snapshot, desvio por sonda `SphereCastNonAlloc` (usa só a menor
  distância), travamento (`StuckTime`) → `Failure`.
- `INavigationProvider` com padrão `DirectNavigation`; **NavMesh fora do M6**.
- Plano XZ; `ApproachStyle` = `Direct` ou `AlignDepthFirst` (eixo de profundidade do `AIWorld`).

---

## 12. Integrações e autoridade

| Domínio | Autoridade | AI faz | Nunca faz |
|---|---|---|---|
| Decisão, percepção, alvo, engajamento | **AI** | tudo | — |
| Movimento, pulo, dash, facing | **Character** | estado e bordas no `CharacterCommandBuffer`; `Request/Cancel` só no caminho `AgentOwner` | integrar o motor, forças, `Teleport` |
| Ataques, guarda, reações | **Combat** | bordas e `GuardHeld`; lê estado; `RequestAction/CancelActionRequest` só no `AgentOwner` | `StartAttack`, `InterruptAttack`, decidir golpes, `TryConsume` |
| Habilidades, efeitos, tags | **Abilities** | bordas por tag e `SetHeld`; lê `CanActivate`/cooldown/tags; `Request/CancelActivationRequest` só no `AgentOwner` | `TryActivateAbility`, `CancelAbility`, aplicar efeitos |
| Estados do personagem | **HFSM** | nada direto: a HFSM consome as mesmas bordas do jogador | trocar estados, ler parâmetros ou estado ativo |
| Atributos | **Stats** | lê valores e razões | modificar stats |

### 12.1 Stats (ADR-0009)
`StatCondition` (próprio/alvo; valor, base ou razão de recurso), considerações `SelfResourceRatio` e
`TargetResourceRatio`.

### 12.2 Character
`CharacterAgentLocomotion` (`IAgentLocomotion` + rastreio de pulo/dash nos três caminhos), condições `IsGrounded`,
`CanDash`, `IsSuspended`.

### 12.3 Combat
`CombatAgentBridge` (time pelo perfil, `AIEntityId` espelhado do `CombatantId` opcional, estímulo `Damage` a partir de
`HitReceived`, rastreios); ações `PerformCombatAction` (`CompleteOn`, `MaxWait`, direção para o alvo/facing) e
`HoldGuard`; condições `SelfCombatState`/`TargetCombatState`, `CanExecuteCombatAction`, `WasHitRecently`.

### 12.4 Abilities
`AbilityAgentBridge` (time pelo `ITeamRelation`, estímulos de `GameplayEventReceived`, rastreios); ações
`ActivateAbility` e `HoldAbilityInput`; condições `CanActivateAbility`, `AbilityCooldown`, `HasOwnedTags` (próprio ou
alvo pelo snapshot), `IsAbilityActive`; `OwnedTagDetectability`.

### 12.5 HFSM — exclusivamente pelos command buffers
1. O ator usa a **mesma** `StateMachineDefinition` do jogador (estados das integrações com posse `External`); a fonte
   é o adaptador de input **ou** o `AIAgent`; trocar de fonte limpa os buffers (§10.5).
2. `EvaluationLoop = FixedUpdate`: a borda escrita em `DECISION + 20` é reivindicada e consumida **no mesmo passo**.
   `EvaluationLoop = Update`: a claim ocorre no `Update` seguinte e a execução no próximo passo fixo — a latência entre
   loops do ADR-0003 permanece (§13).
3. Bridges em `HfsmOwner` (§10.2). A IA lê o estado de execução pelo Combat/Abilities/Character, nunca pela HFSM;
   fases de chefe são ramos da BT. Testes cross-package na Sandbox do host.

---

## 13. Tempo, determinismo e latências (ADR-0026)

| Item | Regra |
|---|---|
| Loop | `FixedUpdate`, banda `DECISION` (+0/+20/+60). **Revisão parcial** dos ADRs 0003/0004 (notas nos próprios ADRs): a IA decide no passo fixo; fontes de input do jogador continuam em `Update` (`COMMAND_SOURCES`); bandas continuam convenção de execução, não scheduler; nenhum código do Core ou da HFSM muda. |
| Relógio | Segundos de jogo acumulando o Δt fixo; `Wait`, `Cooldown`, `TimeLimit`, services e `MaxWait` pelo relógio do agente. |
| Intervalo | `EvaluationInterval` (padrão 1) em passos, escalonado por `AIEntityId % intervalo`; `IdleInterval` opcional. Δt do tick = passos desde a última avaliação × Δt fixo. A caixa de estímulos e os anéis dos bridges acumulam entre avaliações. |
| Hitstop | Não pausa a IA por padrão; `IAITimeSource` opcional. |
| Aleatoriedade | XorShift por agente semeado por `AIEntityId` (+ semente opcional), consumido na ordem da árvore. |
| Física | Só `NonAlloc`; usada de forma independente da ordem (booleano, menor distância). |
| Equivalência | Testes de determinismo exigem **equivalência lógica** (mesmas decisões, saídas e desfechos por passo), não igualdade bit a bit da PhysX. |

| Latência (passo *n*) | Valor |
|---|---|
| Decisão → executor (HFSM em `FixedUpdate`, `AgentOwner` ou `PackageOwner`) | mesmo passo *n* |
| Decisão → executor (HFSM em `Update`) | claim no `Update` seguinte, execução no próximo passo fixo |
| Estado do mundo visto pela decisão | fim de *n − 1* |
| Estímulo emitido em *n* (inclusive dano da fase 4 do Combat) → caixa do agente | P0 de *n + 1*; consumo na próxima avaliação |
| Pedido de token em *n* → concessão visível | tick seguinte do agente |
| Evento do executor → desfecho do rastreio | tick seguinte do agente |

---

## 14. Execução, desempenho e capacidades

Zero GC em regime aquecido com 50 agentes; pré-alocação de layouts, memórias, Blackboard, caixas, anéis, buffers de
física e engajamento. Custo de percepção O(agentes × percebíveis) com pré-filtro e intervalos; medido e registrado.

| Capacidade (padrão) | Política de saturação | Diagnóstico |
|---|---|---|
| Registro de percebíveis (256) | Recusa ao registrar; limite declarado (§7.2) | `RegistrySaturationCount`, log |
| Estímulos por passo (128) | Top-K global pela chave de seleção | `StimulusOverflowCount` |
| Caixa de estímulos por agente (16) | Top-K pela chave de seleção; entrega cronológica | `InboxOverflowCount` |
| Memória por agente (16) | Top-K pela chave total (§7.4) | `MemoryOverflowCount` |
| Escritas externas no Blackboard (= nº de chaves) | Coalescidas por chave; não satura | `CoalescedWriteCount` |
| Orçamento de trabalho por tick (256) | Para entre operações (§5.6) | `BudgetExceededCount` |
| Eventos por bridge entre avaliações (32) | Top-K por (passo ↑, ordem de entrega ↑) — os mais antigos ficam; rastreios afetados → `Unconfirmed` | `BridgeEventOverflowCount` |
| Rastreios abertos por bridge (4) | Pedido além → ação `Running` aguardando vaga | `TrackerFullCount` |
| Pedidos de engajamento por passo (64), detentores por pool, vagas | Recusa → `Denied` | `EngagementOverflowCount` |

---

## 15. Ferramentas de autoria

| Ferramenta | Conteúdo |
|---|---|
| Graph Editor | GraphView como **vista** sobre `BehaviourTreeGraphModel` (sem UI, testável, Undo/Redo), como o ADR-0001 da HFSM; de cima para baixo; ordem dos filhos explícita; decorators/services como faixas; canais declarados visíveis; painel lateral com `PropertyField` + seletor do Core; painel de Blackboard; busca; abrir subárvore; validação inline. |
| Inspector de `BlackboardDefinition` | Chaves, tipos, padrões, herança, uso. |
| AI Debugger | Agentes; status por nó (Running/Success/Failure/Aborted, idade em passos) no grafo, conjunto ativo, último abort, trabalho do tick; Blackboard ao vivo (edição pela escrita externa coalescida); caixa de estímulos e memória; scores da seleção; escopos, tokens e vagas; rastreios de comando e desfechos; contadores; histórico (64). |
| Gizmos | Visão, alcances, linhas de visão, últimas posições, anel de engajamento, steering, rota. |
| Validators | Composite sem filhos; `Parallel` sem políticas; `LowerPriority` fora de filho de `Selector` (inclui filhos de Random/Utility/Parallel); dois ramos de `Parallel` com o mesmo canal (aviso); chave ausente/tipo errado; subárvore com Blackboard incompatível; ciclos; intervalo ≤ 0; `TickBudget` menor que o necessário para iniciar o caminho mais profundo; cena: agente sem `AIWorld`, ids duplicados/zero, percebíveis acima da capacidade, caminho do bridge incompatível com a posse do executor, duas fontes no mesmo buffer. |

GraphView aprovado como tecnologia inicial (gatilhos do Graph Toolkit conferidos em 2026-10-13, não disparados);
casca de grafo comum **não** extraída para o Core no M6.

---

## 16. Samples
- **AI Basics** (AI + Character): patrulha, visão com obstáculos, audição, perseguição, busca na última posição, retorno
  e fuga; painel com Blackboard, caixa e memória.
- **AI Combat Arena** (AI + Character + Combat + HFSM + Abilities + Stats): jogador e o **mesmo prefab** controlado por
  IA (troca de fonte); arquétipos genéricos *Brawler*, *Skirmisher*, *Caster*, *Bruiser*; escopos de engajamento (2
  corpo a corpo + 1 distância); *Champion* com fases por razão de vida. Só assets.

---

## 17. API pública (proposta)
| Tipo | Categoria | Estabilidade |
|---|---|---|
| `BehaviourTreeDefinition`, `BlackboardDefinition`, `SensorProfileDefinition`, `EngagementProfileDefinition`, `TargetSelectionDefinition` | SO | Experimental |
| `AIAgent` (`Tree`, `Blackboard`, `Memory`, `Inbox`, `CurrentTarget`, `HasControl`, `TakeControl`, `Pause/Resume/Restart/Stop`, `SetValue`, `AssignEntityId`, `EvaluationInterval`, contadores, eventos `TargetChanged`, `EntityPerceived`, `EntityForgotten`, `TreeCompleted`, `ControlLost`) | MB | Experimental |
| `AIWorld` (`WorldId`, `StepIndex`, `EmitStimulus`, `RegistryCapacity`, `TryResolve(handle)`, consultas de engajamento, contadores), `AIPerceivable`, `AIEngagementTarget`, `AIPatrolRoute`, `AITeam` | MB | Experimental |
| `BehaviourTreeInstance`, `Blackboard`, `PerceptionMemory`, `StimulusInbox` (leitura) | class | Experimental |
| `BehaviourNode`/`<TMemory>`, `BehaviourDecorator`, `BehaviourService`, `BehaviourCondition`, `ISensor`, `ITargetSelector`, `ITargetConsideration`, `IUtilityConsideration`, `IAgentLocomotion`, `INavigationProvider`, `IAITeamSource`, `IDetectabilitySource`, `IAITimeSource`, `IAgentCommandBridge` | extension points | Experimental |
| `NodeStatus`, `StopReason`, `AbortMode`, `ParallelPolicy`, `ParallelConflictPolicy`, `OutputChannel`, `BlackboardKeyType`, `BlackboardKeyReference`, `AIEntityId`, `AIEntityHandle`, `AIStimulus`, `EngagementTokenHandle`, `EngagementState`, `ExecutionPath`, `CommandOutcome`, `CommandTrackHandle`, `PerceivedEntity` | struct/enum | Experimental |

Handles: dono + vaga + geração; handle estrangeiro nunca age.

---

## 18. Qualidade

### 18.1 Estratégia de testes
**EditMode (domínio):** ciclo de vida (uma vez por ativação, inclusive abort antes do primeiro tick); cada composite e
decorator; **vários ramos `Running` em `Parallel`** com memória própria; **abort dentro de `Parallel`** (Self conta
como falha do filho; validator recusa LowerPriority); **saída conflitante** (sucesso e falha no mesmo tick, cada
`ConflictPolicy`; dois ramos no mesmo canal: primeiro em pré-ordem vence, segundo tenta no tick seguinte); aborts
`Self`/`LowerPriority`/`Both` na ordem normativa, um por tick, sem cascata no recomeço; **orçamento esgotado** em
início, tick, condição e service, com estado consistente e continuação no tick seguinte; **reinício** (raiz termina →
tick seguinte; `Restart`/`Stop` com paradas folha → raiz e liberações); `Repeat` um reinício por tick; subárvores;
Blackboard (tipos, herança, escrita externa coalescida por chave e prioridade); identidades (duplicata, zero, clone sem
id, handle de outro mundo/antigo); caixa de estímulos (acúmulo com intervalo > 1, top-K e ordem de entrega); memória
top-K embaralhada; seleção com histerese; arbitragem; rastreador de comandos (tabela da §10.3 com eventos sintéticos);
validators; modelo do Graph Editor.

**PlayMode (física real e packages reais):** percepção 2.5D com obstáculo, audição e dano em *n + 1*, detectabilidade
zero; steering; **seis agentes com dois tokens em sequências completas** (aproximação + ataque + recuperação), com
aborts, interrupção por hitstun, troca de alvo, morte, desabilitar e `MaxHoldTime` — nunca mais de dois detentores,
token retido do início ao fim do filho, liberação exatamente uma vez; Combat e Abilities nos **três caminhos**: **duas
ações iguais enfileiradas**, **prioridades diferentes**, **expiração e rejeição**, **consumo `External` pela HFSM**
(Sandbox), **interrupção após o início**, **troca jogador ↔ IA com bordas pendentes**, `Unconfirmed` provocado (script
ativando a mesma habilidade); Character (MoveTo, pulo/dash rastreados); Stats; **invariância com criação embaralhada**
(percebíveis, agentes, estímulos) até as capacidades; frame rate variável e `timeScale` 0,5; **zero GC** com 50 agentes.
Testes do padrão HFSM e da troca de fonte na Sandbox do host (`Assets/_FrameworkSandbox`).

### 18.2 Critérios de aceite
| # | Critério |
|---|---|
| AC1 | Inimigo de exemplo faz Patrol/Chase/Attack/Retreat **só com assets**; nenhuma chamada a motor/combat/ASC fora dos bridges (domínio sem essas referências). |
| AC2 | Mesmo prefab e mesma `StateMachineDefinition` controlados por jogador e IA trocando só a fonte; bordas pendentes limpas e rastreios `Cancelled (SourceChanged)`. |
| AC3 | Semântica da BT (§5): ciclo de vida por ativação, `Parallel` com vários ramos e conflitos, aborts na ordem normativa sem cascata, orçamento de trabalho com estado consistente, reinício. |
| AC4 | Determinismo (§13): criação embaralhada → mesmo traço lógico (decisões, saídas e desfechos por passo); frame rate variável e `timeScale` 0,5 → mesmas decisões por passo fixo; equivalência lógica, não bit a bit da PhysX. |
| AC5 | Percepção (§7): identidades e handles, registro cheio com política declarada, estímulos em passos pulados, top-K × ordem de entrega, memória invariante. |
| AC6 | Seleção de alvo com histerese, desempate por id, troca de alvo publicada. |
| AC7 | Engajamento (§9): 6 agentes e 2 tokens em sequências completas com aborts, interrupções e troca de alvo; retenção durante todo o filho; liberação exatamente uma vez; handles estrangeiros inertes; independência de ordem. |
| AC8 | Rastreamento (§10): correlação por token nos caminhos `HfsmOwner`/`AgentOwner`, regra de borda única e `Unconfirmed` no `PackageOwner`, cenários da §18.1 — sem alterar API pública de Character, Combat ou Abilities. |
| AC9 | Zero GC em regime aquecido com 50 agentes; física `NonAlloc`; tempo por passo medido e registrado. |
| AC10 | Nenhum nome de jogo (Ryu, inimigos ou chefes do WOR) em código, assets ou samples. |
| AC11 | Ferramentas: modelo do grafo testado, debugger e gizmos validados, validators sem falso negativo; validação manual com mouse físico documentada (pendente do owner). |
| AC12 | QG1–QG12; **QG12 só com o Core** compila e passa sem nenhuma integration assembly ativa. |

### 18.3 Quality Gates
QG1–QG12; validação das ferramentas com interação real × automação sintética.

---

## 19. Whiskers of Rage — como o jogo usará os mecanismos (sem código no package)

Fonte: GDD 2025 (mobs normal, rápido, longa distância, bruto, especial; chefes por bioma). Tudo é asset/glue do jogo.

| Necessidade do jogo | Mecanismo genérico |
|---|---|
| Mob padrão (corpo a corpo) | `EngagementScope` corpo a corpo → `MoveTo(vaga)` → `PerformCombatAction`; sem token, `Orbit` na borda do anel |
| Mob rápido ("ataca e espreita"; ataque pelas costas) | Escopo com vaga "costas"; `Sequence` ataque → `KeepDistance`; condições `TargetCombatState`/`TargetFacingAway` |
| Mob à distância | Escopo do pool "distância" + `KeepDistance` + linha de visão + `ActivateAbility` (projétil) |
| Mob pesado | Perfil lento (Character), armadura dos ataques (Combat), BT sem guarda, prioridade alta no pool |
| Hordas | Pools e vagas no alvo do jogador; intervalos maiores sem alvo |
| Chefes com fases | Ramos por fase com abort sobre a razão de vida (Stats); habilidades de área/projétil; ataques do Combat |
| Chefe furtivo | Tag de invisibilidade concedida por habilidade; `OwnedTagDetectability` para IA de aliados; apresentação no jogo |
| Reação a stun/parry | Abort `LowerPriority` por `SelfCombatState.CanAct` falso → ramo de espera; escopos liberados no abort |
| Boss Rush | Mesmas BTs como subárvores |

---

## 20. Impacto em packages existentes

### 20.1 Nenhuma mudança necessária

| Package | Mudança |
|---|---|
| Core 0.4.0 | Nenhuma (`ExecutionOrder.DECISION`, `GameplayTag`, `SelectImplementation`, validação). |
| Stats 0.1.1 | Nenhuma. |
| HFSM 0.1.0 | Nenhuma; sem dependência nova. |
| Character 0.2.0 | Nenhuma (`CharacterCommandBuffer`, `CharacterMotor` com `RequestJump/Dash`, `Cancel…`, `ActionResolved`, `Jumped`, `DashStarted/Ended`, `FacingController`). |
| Combat 0.1.0 | Nenhuma (`CombatCommandBuffer`, `RequestAction`/`CancelActionRequest`, `ActionResolved` com token, `AttackStarted/Ended` com `Instance`, estado do `CombatTarget`). |
| Abilities 0.1.0 | Nenhuma (`AbilityCommandBuffer`, `RequestActivation`/`CancelActivationRequest`, `ActivationResolved` com token, `AbilityActivated/Ended` com handle e geração, consultas, snapshot). |

### 20.2 Pontos que só se resolvem por completo com mudança em outro package (não adotada)

| Ponto | Limitação sem mudança | Alternativa adotada no M6 | Mudança que resolveria (fora do M6) |
|---|---|---|---|
| Token no caminho `PackageOwner` (combo runner, ASC, motor) | Os eventos de início não trazem o token da borda consumida (o ASC guarda o token em `AbilityExecution.Request`, mas sem acesso público fora das ações) | Borda única rastreada por buffer + `Unconfirmed`; caminhos `AgentOwner`/`HfsmOwner` exatos | Token nos args de `AttackStarted`/`AbilityActivated`/`Jumped`/`DashStarted` (minor, aditivo) |
| Retirada de uma borda pela fonte (abort) em `HfsmOwner`/`PackageOwner` | ADR-0004 só permite consumo pelo dono; a borda abandonada pode executar dentro da janela | `AgentOwner` cancela por token; nos outros, abandono (R41) ou `ClearChannel` | `Retract(token)` da fonte nos buffers (ADR + minor dos três packages) |
| Instância do ataque no caminho External do Combat | Ordem `AttackStarted` → `ActionResolved(Executed)` no mesmo passo é observada no código do Combat 0.1.0, **não documentada** no CONTRACTS dele | Teste de contrato na integração do AI fixa a ordem para o Combat 0.1.x; sem ela, término `Unconfirmed` | Registrar a ordem como garantia no CONTRACTS do Combat (só documentação) |

### 20.3 Matriz de dependências (sem mudança no grafo)
| Package | Camada | Hard | Opcional |
|---|---|---|---|
| ai | L5 | core | stats, character, combat, abilities |

| AI consome de | Contratos usados (nenhum alterado) |
|---|---|
| Core 0.4.0 | `ExecutionOrder.DECISION`, `GameplayTag`/`GameplayTagSet`, `SelectImplementation`, `IValidatable` |
| Stats 0.1.1 | `StatsComponent`, valores de stats, `ResourcePool` (atual/máximo) |
| Character 0.2.0 | `CharacterCommandBuffer`, `CharacterMotor` (leitura, `Request/Cancel` de pulo/dash, eventos), `FacingController` |
| Combat 0.1.0 | `CombatCommandBuffer`, `CombatController` (leitura, `ActionOwner`, `RequestAction`, `CancelActionRequest`, `CanExecuteAction`, eventos), `CombatTarget` (estado, perfil/time, `HitReceived`), `CombatantId` |
| Abilities 0.1.0 | `AbilityCommandBuffer`, `AbilitySystemComponent` (`CommandOwner`, `RequestActivation`, `CancelActivationRequest`, `FindAbilityByInputTag`, `TryGetAbility`, `CanActivateAbility`, cooldown, tags, snapshot, eventos), `ITeamRelation` |

---

## 21. Decisões (aprovadas na auditoria do v1)

| # | Decisão final |
|---|---|
| D1 | `FixedUpdate`, banda `DECISION`, fases P0/+20/+60; revisão parcial formal dos ADRs 0003/0004 (ADR-0026). |
| D2 | Conjunto ativo persistido (vários ramos no `Parallel`), aborts em ordem normativa, um por tick sem cascata, orçamento de trabalho com parada consistente (ADR-0027). |
| D3 | Decorators e services anexados; `EngagementScope` como nó de um filho. |
| D4 | Blackboard por agente, tipado, escrita externa coalescida por chave (ADR-0028). |
| D5 | `AIEntityId` próprio, duplicata recusada, `AIEntityHandle` com mundo + vaga + geração. |
| D6 | Registro com capacidade declarada, snapshot P0, física só para linha de visão, estímulos com top-K global e caixa por agente (ADR-0029). |
| D7 | `IAgentLocomotion` + ações genéricas; `DirectNavigation`; NavMesh fora (ADR-0030). |
| D8 | Escopo de engajamento: pedido, concessão e retenção separados; tokens e vagas com handles (ADR-0031). |
| D9 | HFSM exclusivamente pelos command buffers, sem dependência. |
| D10 | Rastreamento por token nos caminhos `HfsmOwner`/`AgentOwner`; borda única + `Unconfirmed` no `PackageOwner` (ADR-0032). |
| D11 | GraphView sobre modelo de edição independente da UI. |
| D12 | Sem extração para o Core no M6. |
| D13 | `UtilitySelector` e `ScoredTargetSelector` com escopo fechado. |
| D14 | Samples AI Basics e AI Combat Arena. |
| D15 | Hitstop não pausa a IA por padrão. |
| D16 | `Parallel` com `SuccessPolicy`, `FailurePolicy` e `ConflictPolicy` explícitas. |

---

## 22. ADRs (aceitas na v2)
| # | Título |
|---|---|
| [0026](../adr/0026-ai-execution-and-timing.md) | Execução da IA: passo fixo na banda DECISION, fases e intervalos |
| [0027](../adr/0027-behaviour-tree-execution-model.md) | Modelo de execução da Behaviour Tree: conjunto ativo, aborts e orçamento |
| [0028](../adr/0028-blackboard.md) | Blackboard tipado por agente com escrita externa coalescida |
| [0029](../adr/0029-ai-perception.md) | Percepção: identidades, registro, snapshot e estímulos com ordem total |
| [0030](../adr/0030-agent-locomotion-and-navigation.md) | Contrato de locomoção do agente e navegação sem NavMesh |
| [0031](../adr/0031-engagement-coordination.md) | Escopo de engajamento por tokens e vagas |
| [0032](../adr/0032-ai-command-output-and-tracking.md) | Saída da IA por command buffers e rastreamento por token |

Revisões parciais registradas nos ADRs [0003](../adr/0003-execution-order-bands.md) e
[0004](../adr/0004-command-buffers.md), sem apagar as decisões históricas.

---

## 23. Riscos
| # | Risco | Mitigação |
|---|---|---|
| R33 | Escopo (BT + percepção + utility + navegação + coordenação) | §1.1 fechada; seis entregas sem ampliação |
| R34 | Determinismo com física, eventos e muitos agentes | Snapshot P0, caixas, coalescência, ordens totais, top-K; AC4 por equivalência lógica |
| R35 | Custo de percepção com hordas | Intervalos, escalonamento, `IdleInterval`, pré-filtro; medição |
| R36 | Terceiro editor GraphView (R1) | Modelo de edição isolado; gatilhos conferidos; extração só com ADR |
| R37 | Correlação no `PackageOwner` limitada (sem token nos eventos) | Borda única, `Unconfirmed` contado; `AgentOwner`/`HfsmOwner` exatos; mudança aditiva futura (§20.2) |
| R38 | Navegação sem NavMesh | Arenas abertas; desvio; travamento → `Failure`; NavMesh por ADR futuro |
| R39 | Muitos tipos `[SerializeReference]` | `[MovedFrom]`, `sealed`, testes que carregam assets dos samples |
| R40 | Latência de reação e intervalos > 1 | Latências documentadas e medidas; intervalo 1 em combate |
| R41 | Borda abandonada executando dentro da janela do consumidor (`HfsmOwner`/`PackageOwner`) | Janelas curtas; `ClearChannel`; `AgentOwner` com cancelamento por token; `Retract` como mudança futura (§20.2) |
| R42 | Ordem `AttackStarted` → `ActionResolved` não documentada no Combat | Teste de contrato na integração; término `Unconfirmed` se a ordem mudar; registrar no CONTRACTS do Combat quando permitido |
| R43 | Registro acima da capacidade depende da ordem de habilitação | Capacidade declarada, recusa contada e logada, validator de cena |

---

## 24. Entregas (cada uma auditável, com testes e docs)

| # | Entrega | Conteúdo | Saída verificável |
|---|---|---|---|
| 1 | Fundação da BT e do Blackboard | Package pelo template; definições; layout; instância com conjunto ativo e memória por nó; composites (incl. `Parallel` com políticas), decorators, services, aborts, subárvores, orçamento de trabalho, canais de saída; reinício; `AIAgent` (fase +20, intervalo, RNG, escrita externa coalescida), `AIWorld` (passo, fases, `WorldId`); validators de asset | EditMode; zero GC do domínio; AC3; QG12 só com Core |
| 2 | Percepção e alvo | Identidades e handles, registro com capacidade declarada, snapshot P0, times, detectabilidade, sensores, barramento + caixas, memória top-K, seleção com histerese | PhysX; AC5, AC6, AC4 (percepção) |
| 3 | Movimento e engajamento | `IAgentLocomotion`, steering, navegação direta, ações de locomoção, rota, `EngagementScope` e arbitragem com handles | PhysX com fixture; AC7, AC4 (engajamento) |
| 4 | Integrações | Stats, Character, Combat, Abilities com rastreamento nos três caminhos; testes cross-package (HFSM, troca de fonte) na Sandbox | AC1 (parcial), AC2, AC8 |
| 5 | Ferramentas | Graph Editor (modelo + vista), inspectors, AI Debugger, gizmos, validators de cena | Validação de ferramentas; AC11 |
| 6 | Samples e fechamento | 2 samples, regressão completa, zero GC (AC9), QG1–QG12, docs, auditoria | AC1–AC12 |

Cada entrega termina com suítes do package e do WOR verdes, QG12, `check_dependencies`, commits e relatório de
desvios. Nenhuma tag antes da auditoria final do M6; nenhuma alteração em packages existentes.

---

## 25. Registro da auditoria (v1 → v2)

Parecer: arquitetura geral aprovada com ajustes obrigatórios antes da implementação. Onde cada ajuste ficou:

| Ajuste | Incorporação |
|---|---|
| 1 — revisão parcial dos ADRs 0003/0004 | ADR-0026; notas de revisão nos ADRs 0003 e 0004; §12.5, §13 |
| 2 — BT e `Parallel` | ADR-0027; §5.1–§5.8; testes §18.1; AC3 |
| 3 — vida útil dos tokens | ADR-0031; §9 (ações avulsas de pedir/liberar token do v1 removidas); AC7 |
| 4 — rastreamento de comandos | ADR-0032; §10 (v1 presumia evento = última borda e descartava bordas com `TryConsume`, o que violava o ADR-0004 — corrigido); §20.2; AC8 |
| 5 — saturação e identidades | ADRs 0028/0029; §6, §7, §14; AC5 |
| Refinamentos | GraphView (§15), NavMesh fora (§11), Utility fechado (§5.3), hitstop (§13), samples (§16), plano XZ (§11), QG12 só com Core (§3.1, AC12), equivalência lógica (§13, AC4) |
