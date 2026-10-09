# M2 — Hierarchical State Machine (HFSM) · Especificação técnica (design para revisão)

- **Package:** `com.ramirestechgames.hfsm` · assembly/namespace raiz `RamiresTechGames.HierarchicalStateMachine` · camada L1
- **Dependência hard:** `com.ramirestechgames.core` (≥ 0.2.0: `ExecutionOrder`, `IValidatable`, `[SelectImplementation]`)
- **Opcionais:** nenhuma (quem integra são Character, Combat e Abilities, nas integration assemblies deles)
- **Status:** **Design em revisão — não implementar antes da aprovação.**
- **Insumos:** Master Prompt §4.2 · ADR-0003 (decisões temporais do M2) · ADR-0004 (command buffers) ·
  [spike de graph editor](m2-graph-editor-spike.md).

---

## 1. Objetivo e escopo
Executar comportamentos e controlar transições de estados hierárquicos, de forma genérica e configurada por assets.
**A HFSM executa; ela não decide estratégia** (isso é da Behaviour Tree, M6). Player, Enemy e NPC usam os mesmos tipos de
estado e até a mesma definição: a origem da decisão (input, AI, cutscene) fica fora da HFSM (command buffers e
parâmetros).

**Dentro do escopo (Master Prompt §4.2):** estados e sub-estados · estados pai · guards/condições · Enter, Tick,
FixedTick, Exit · estado inicial configurável · prioridade e regras de interrupção · tipos de estado compartilhados ·
estados desacoplados da origem das decisões · contexto de runtime · visualização no Editor · debugger de estados e
transições.

**Fora do escopo do M2:** regiões paralelas/ortogonais · histórico profundo (*deep history*) · copiar/colar entre grafos
· estados concretos de jogo (andar, atacar...: ficam nas integrações de Character/Combat) · decisão de IA · rede.

---

## 2. Conceitos e semântica

| Conceito | Definição |
|---|---|
| **Estado** | Nó da hierarquia. Pode ter um **comportamento** (`StateBehaviour`) e/ou **filhos**. |
| **Estado composto** | Estado com filhos. Exatamente **um filho ativo** por vez; um filho é o **inicial**. |
| **Configuração ativa** | Caminho raiz → folha ativa. Todos os estados do caminho estão ativos e recebem Tick. |
| **Transição** | Origem → destino, com condições (todas verdadeiras = AND), prioridade e flag de interrupção. |
| **Transição de estado pai** | Transição cuja origem é um composto: vale enquanto **qualquer descendente** estiver ativo (equivale ao *Any State* daquele nível). A origem `Root` é o *Any State* global. |
| **Sub-máquina** | Estado composto cujos filhos vêm de outro `StateMachineDefinition` (reuso, ex.: "Locomotion" compartilhada). |

### 2.1 Entrada e saída (ordem garantida)
Para uma transição de **S** (folha ativa) para **T**:
1. **LCA** = ancestral comum mais baixo de S e T (se T é ancestral de S, a transição re-entra em T).
2. **Exit** de baixo para cima: da folha ativa até o filho do LCA.
3. **Enter** de cima para baixo: do filho do LCA até T; depois, se T for composto, entra no **filho inicial**
   recursivamente (ou no último filho ativo, se o composto tiver *shallow history*).
4. `TimeInState` zera nos estados entrados.

### 2.2 Avaliação de transições (uma decisão por tick)
1. Percorre a configuração ativa **de cima para baixo** (raiz → folha): transições de estados mais externos têm
   precedência (ex.: "Hit" definido em `Alive` interrompe qualquer sub-estado).
2. Em cada nível, transições ordenadas por **prioridade** (maior primeiro), depois por ordem de declaração.
3. A primeira transição **permitida** (§2.3) com todas as condições verdadeiras é executada.
4. **No máximo `MaxTransitionsPerTick` transições por tick** (padrão 1; limite 8). Cadeias continuam nos ticks seguintes
   (previsível; impede laços).

### 2.3 Prioridade e interrupção
- Cada estado ativo tem um **limiar de interrupção** (`InterruptionThreshold`, padrão 0), definido no asset e alterável
  em runtime pelo próprio comportamento (`context.SetInterruptionThreshold(n)`), e zerado ao (re)entrar.
- Uma transição só pode sair de um estado se `transição.Priority ≥ limiar` de **todos os estados que ela vai sair**.
- Exemplo: ataque define limiar 50 durante a janela ativa; "Hit" (prioridade 100) interrompe; "Move" (prioridade 0) espera.
- `RequestState(id, priority)` (externo, ex.: morte por glue code) entra na fila e é avaliado no próximo tick com as
  mesmas regras de prioridade.

### 2.4 Conclusão de estado
Um comportamento pode chamar `context.Complete()` (ex.: animação/ataque terminou). A condição `StateCompleted` lê essa
flag, que zera ao (re)entrar. Isso evita que estados conheçam seus sucessores.

---

## 3. Modelo de dados (ScriptableObjects imutáveis em runtime)

### 3.1 `StateMachineDefinition` (SO)
| Campo | Tipo | Descrição |
|---|---|---|
| `States` | `List<StateData>` | Lista plana com `ParentId` (hierarquia). |
| `Transitions` | `List<TransitionData>` | Todas as transições. |
| `Parameters` | `List<ParameterDefinition>` | Parâmetros (bool, int, float, trigger) com valor padrão. |
| `TriggerLifetime` | float (s, padrão 0.2) | Validade de um trigger não consumido (mesma lógica de "borda com expiração" do ADR-0004). |
| `MaxTransitionsPerTick` | int (1..8) | Ver §2.2. |

### 3.2 `StateData` (serializável)
| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | string (GUID estável) | Identidade (renomear não quebra transições). |
| `Name` | string | Nome exibido. |
| `ParentId` | string | Vazio = filho da raiz. |
| `IsInitial` | bool | Filho inicial do pai (exatamente um por composto). |
| `Behaviour` | `[SerializeReference, SelectImplementation] StateBehaviour` | Opcional (estados de agrupamento não precisam). |
| `InterruptionThreshold` | int | Limiar inicial (§2.3). |
| `ShallowHistory` | bool | Composto volta ao último filho ativo em vez do inicial. |
| `SubMachine` | `StateMachineDefinition` | Opcional: filhos vêm de outra definição (achatados na compilação). |
| `EditorPosition` | Vector2 | Posição no grafo (só Editor). |

### 3.3 `TransitionData` (serializável)
| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | string (GUID) | Identidade. |
| `FromId` | string | Estado de origem (`Root` = *Any State*). |
| `ToId` | string | Destino. |
| `Conditions` | `[SerializeReference, SelectImplementation] List<TransitionCondition>` | AND. Lista vazia = sempre verdadeira. |
| `Priority` | int | Ordem e permissão de interrupção. |

### 3.4 `ParameterDefinition`
`Name`, `Type` (`Bool`, `Int`, `Float`, `Trigger`), valor padrão. Triggers são consumidos quando uma transição que os lê
dispara, ou expiram após `TriggerLifetime`.

---

## 4. Contratos de código

### 4.1 Comportamentos (extension point)
```csharp
[Serializable]
public abstract class StateBehaviour                       // configuração imutável; NUNCA guarda estado do ator
{
    public virtual void OnEnter(StateContext context) { }
    public virtual void OnTick(StateContext context, float deltaTime) { }
    public virtual void OnFixedTick(StateContext context, float fixedDeltaTime) { }
    public virtual void OnExit(StateContext context) { }
    public virtual void Validate(ValidationReport report, UnityEngine.Object context) { }
}

[Serializable]
public abstract class StateBehaviour<TMemory> : StateBehaviour where TMemory : class, new()
{
    // TMemory: dados por ator, criados UMA vez na construção da instância (sem GC por Enter).
    protected TMemory GetMemory(StateContext context);
}
```
Um mesmo asset é compartilhado por vários atores: o estado de cada ator vive na **memória por estado** (`TMemory`) e no
`StateContext`, nunca em campos do comportamento.

### 4.2 Condições (extension point)
```csharp
[Serializable]
public abstract class TransitionCondition
{
    public abstract bool Evaluate(StateContext context);   // SEM efeitos colaterais (só leitura / peek)
    public virtual void Validate(ValidationReport report, UnityEngine.Object context) { }
}
```
**Regra de command buffers (ADR-0004):** condições apenas **espiam** (`HasPending`); quem **consome** o comando é o
`OnEnter` do estado de destino, chamado exatamente uma vez por transição. Várias condições podem ler o mesmo comando sem
duplicar a ação.

Incluídas (genéricas): `TimeInStateCondition` (≥ s) · `StateCompletedCondition` · `ParameterCondition`
(bool/int/float/trigger com comparador) · `NotCondition` · `AnyCondition` (OR) · `AllCondition` (AND aninhado).
Comportamento incluído: `CompleteAfterTimeBehaviour` (completa após N s; ex.: stun). Estados de jogo **não** ficam aqui.

### 4.3 `StateContext` (um por estado por ator, pré-alocado)
| Membro | Descrição |
|---|---|
| `Owner` (`Component`) / `GameObject` / `Transform` | Ator. |
| `Get<T>()` / `TryGet<T>(out T)` | Componente do ator com cache (sem `GetComponent` por frame). Integrações acham aqui o motor, o command buffer etc. |
| `Parameters` | Leitura/escrita dos parâmetros da instância. |
| `TimeInState` | Tempo desde a entrada deste estado. |
| `Complete()` / `IsComplete` | Conclusão (§2.4). |
| `SetInterruptionThreshold(int)` | Limiar dinâmico (§2.3). |
| `StateId` / `StateName` | Identidade do estado. |

### 4.4 Runtime
```csharp
public sealed class StateMachineInstance                    // Pure, um por ator
{
    public StateMachineInstance(StateMachineDefinition definition, Component owner);
    public void Start();                                    // entra na configuração inicial
    public void Tick(float deltaTime);                      // avalia transições (se modo Update) e tica a configuração ativa
    public void FixedTick(float fixedDeltaTime);            // FixedTick nos estados ativos (+ transições se modo FixedUpdate)
    public void Stop();                                     // sai de toda a configuração
    public bool IsInState(string stateId);                  // verdadeiro para qualquer estado do caminho ativo
    public string ActiveLeafId { get; }
    public void GetActivePath(List<string> results);
    public StateMachineParameters Parameters { get; }
    public void RequestState(string stateId, int priority); // pedido externo, avaliado no próximo tick
    public event Action<StateChangedArgs> StateEntered;     // Notificações enfileiradas e entregues após o tick (FIFO),
    public event Action<StateChangedArgs> StateExited;      // como no Stats: listeners podem chamar a API sem recursão.
    public event Action<TransitionTakenArgs> TransitionTaken;
}

[DefaultExecutionOrder(ExecutionOrder.STATE_MACHINE)]
public sealed class StateMachineRunner : MonoBehaviour     // Definition, modo de avaliação, debug
```
- Definição compilada em **layout imutável compartilhado** (estados em profundidade, pais, filhos, transições por origem
  ordenadas por prioridade, sub-máquinas achatadas), invalidado por versão de definição como no Stats.
- **Sem GC em regime:** contextos, memórias, arrays de caminho e buffer de histórico pré-alocados.

---

## 5. Ciclo de execução (decisões temporais do M2, ADR-0003)

| Decisão | Proposta |
|---|---|
| Loop das transições | **`Update`** (banda `STATE_MACHINE`, depois de `COMMAND_SOURCES` e `DECISION`): comandos de borda do frame já estão nos buffers. Configurável por runner para `FixedUpdate` (personagens muito dependentes de física). |
| Tick dos estados | `OnTick` em `Update` (depois das transições); `OnFixedTick` em `FixedUpdate`, ambos raiz → folha, com o delta do loop. |
| Transições por tick | 1 por padrão (§2.2). |
| Entrada no mesmo frame | O estado que entrou recebe `OnTick` no mesmo `Update`. |
| Notificações | Enfileiradas durante o tick e entregues no fim (FIFO), após toda a troca de estado. |

Sequência de um frame (modo `Update`):
```
FixedUpdate (0..N):  STATE_MACHINE → OnFixedTick(ativos)      CHARACTER → motor consome comandos
Update:              COMMAND_SOURCES → buffers   DECISION → BT escreve intenções
                     STATE_MACHINE → avalia transições (peek) → Exit/Enter (Enter consome) → OnTick(ativos)
                     → entrega notificações (Animator, VFX, debugger)
```

---

## 6. Integração com outros packages (sem dependência da HFSM para cima)

| Package | O que fornece à HFSM | Onde |
|---|---|---|
| Character (M3) | Estados de locomoção (andar, pulo, queda, dash), condições `IsGrounded`, `HasMoveCommand`, `HasJumpCommand` (peek no `CharacterCommandBuffer`). | `Character.Integration.HierarchicalStateMachine` |
| Combat (M4) | Estados de ataque/hit/knockdown/defesa (consomem comandos no `OnEnter`), condições de combo e de hit-confirm; limiares de interrupção durante janelas de ataque. | `Combat.Integration.HierarchicalStateMachine` |
| Abilities (M5) | Estado "casting", condições de ability disponível. | `Abilities.Integration.HierarchicalStateMachine` |
| AI (M6) | **Nada direto.** A BT escreve nos command buffers; a HFSM reage pelos guards (ADR-0004). | — |
| Input System | **Nada direto.** Adapters de input escrevem nos command buffers ou em parâmetros. | — |

Player, Enemy e NPC: mesmo `StateMachineDefinition` (ou mesma sub-máquina "Locomotion"), mudando só a fonte de
comandos.

---

## 7. Editor UX

| Ferramenta | Conteúdo |
|---|---|
| **State Machine Graph** (`Tools/RamiresTech Games/Hierarchical State Machine/Graph` e duplo clique no asset) | GraphView ([spike](m2-graph-editor-spike.md)) como **vista** do asset. Um nível por vez com **breadcrumb** (Root > Alive > Grounded); duplo clique abre composto ou sub-máquina. Nó "Parent/Any" representa as transições do nível. Inicial e *history* marcados. Arestas com rótulo de prioridade. Painel lateral com o Inspector do estado/transição selecionado (comportamento e condições pelo seletor do Core). Busca de tipos de comportamento (`SearchWindow` + `TypeCache`). Minimapa, "Frame all", Undo. Erros do Validator destacados nos nós. |
| **Debugger ao vivo** (no mesmo grafo, em Play Mode) | Seleciona um ator: caminho ativo destacado, `TimeInState`, última transição piscando, parâmetros editáveis, botão "Request state". |
| **State Machine Debugger** (janela) | Lista de atores com runner; caminho ativo; histórico das últimas 50 transições (tempo, origem, destino, prioridade, condições verdadeiras); funciona mesmo sem abrir o grafo. |
| Inspector do `StateMachineRunner` | Definição, modo de avaliação, caminho ativo ao vivo, toggle de log de transições. |
| Rótulo em cena (opcional, Editor/Development) | Nome do estado ativo sobre o ator (gizmo). |
| Validators | §8. |
| Menus | `Assets/Create/RamiresTech Games/Hierarchical State Machine/State Machine Definition`. |

A edição passa por um **modelo de edição sem UI** (adicionar/remover/conectar/mover/definir inicial, com Undo) testável
em EditMode; a vista GraphView só chama esse modelo. Isso isola o GraphView (recomendação do spike).

---

## 8. Validações
| Erro | Aviso |
|---|---|
| Id duplicado/vazio; pai inexistente; ciclo de pais | Estado inalcançável a partir do inicial |
| Composto sem filho inicial ou com mais de um | Transição sem condições saindo de estado sem `Complete` (sempre dispara) |
| Transição para/de estado inexistente | Comportamento ausente em folha (estado vazio) |
| Condição nula na lista; parâmetro inexistente ou de tipo incompatível | Prioridade negativa |
| Ciclo entre sub-máquinas; tipo `[SerializeReference]` ausente (Core) | `MaxTransitionsPerTick` > 1 com transições sem condição (risco de cadeia) |

---

## 9. Testes previstos (depois da implementação, sem TDD)
- Ordem Enter/Exit pelo LCA (folha↔folha, para ancestral, para descendente, re-entrada).
- Prioridade, precedência de níveis, limiar de interrupção estático e dinâmico, `RequestState`.
- `MaxTransitionsPerTick`, conclusão de estado, `TimeInState`, *shallow history*.
- Parâmetros e triggers (consumo e expiração).
- Sub-máquinas achatadas, ciclo de sub-máquinas.
- Notificações em ordem e reentrância.
- Memória por ator: duas instâncias com o mesmo asset não compartilham estado.
- Sem GC em regime (Tick com 50 instâncias).
- Modelo de edição do grafo com Undo (EditMode); smoke da janela GraphView.
- PlayMode: runner com modos Update/FixedUpdate.
- QG12 em projeto vazio.

---

## 10. Critérios de aceite do M2
Além de QG1–QG12:

| # | Critério |
|---|---|
| AC1 | Designer monta uma HFSM de 3 níveis (com sub-máquina) **só no Editor** e a vê funcionando no sample. |
| AC2 | Ordem de Enter/Exit e escolha de transição conforme §2, cobertas por testes. |
| AC3 | Interrupção por limiar e prioridade, conforme §2.3. |
| AC4 | Guards sem efeitos colaterais; consumo no `OnEnter` documentado e testado com um command buffer de teste. |
| AC5 | Player e NPC usam a **mesma** definição no sample, com fontes de comando diferentes. |
| AC6 | Debugger mostra caminho ativo e histórico de transições ao vivo; o grafo destaca o estado ativo. |
| AC7 | Zero GC em regime. |
| AC8 | Nenhuma dependência de AI, Combat, Character ou Input System. |

## 11. Sample planejado — "Patrol & Alert"
Raiz → `Alive` { `Idle`, `Patrol`, `Alert` (limiar 10, completa após 2 s) } + `Dead` (transição de `Root`, prioridade 100).
Dois atores com o mesmo asset: um controlado por teclado (glue escreve parâmetros), outro por um script de timer (NPC).

## 12. Estrutura do package (proposta)
```
Runtime/  Definitions/ (StateMachineDefinition, StateData, TransitionData, ParameterDefinition, layout interno)
          Behaviours/  (StateBehaviour, StateBehaviour<T>, TransitionCondition, condições e comportamento incluídos)
          Execution/   (StateMachineInstance, StateContext, StateMachineParameters, args de eventos)
          Components/  (StateMachineRunner)
Editor/   Graph/ (modelo de edição + vista GraphView)  Debugger/  Inspectors/
Tests/    Editor/ · Runtime/
Samples~/ PatrolAndAlert/
```

## 13. Decisões para a revisão
| # | Decisão | Proposta | Alternativas |
|---|---|---|---|
| D1 | Tecnologia do grafo | **GraphView** como vista isolada; reavaliar Graph Toolkit no M4/M6 ([spike](m2-graph-editor-spike.md)) | Graph Toolkit agora (perde `[SerializeReference]`); canvas próprio (caro) |
| D2 | Loop das transições | `Update` por padrão, `FixedUpdate` opcional por runner | Sempre `FixedUpdate` |
| D3 | Transições por tick | 1 (configurável até 8) | Encadear até estabilizar |
| D4 | Interrupção | Limiar por estado (estático + dinâmico) × prioridade da transição | Flag `CanInterrupt` booleana por transição |
| D5 | Precedência | Níveis externos antes dos internos | Folha primeiro |
| D6 | Parâmetros e triggers | Incluídos; trigger com consumo ou expiração | Sem parâmetros (só command buffers) |
| D7 | Sub-máquinas | Incluídas (achatadas na compilação) | Fora do M2 |
| D8 | *History* | Somente *shallow*, opcional por composto | Sem history; *deep history* |
| D9 | Memória por ator | `StateBehaviour<TMemory>` pré-alocada | Clonar comportamentos por ator |
| D10 | Notificações | Fila FIFO após o tick (igual ao Stats) | Eventos síncronos |
| D11 | Infra de grafo compartilhada | No HFSM.Editor agora; extrair para Core.Editor no M4 com ADR | Já no Core |
| D12 | Regiões paralelas | Fora do M2 | Incluir |
