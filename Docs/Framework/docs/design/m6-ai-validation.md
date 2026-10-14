# M6 — Evidências de implementação do AI

- **Especificação:** [m6-ai-design](m6-ai-design.md) v2 (WOR `f606142`, ajustes autorizados em `f0595be`); ADRs
  0026–0032.
- **Unity:** 6000.6.5f1 · **Package:** `com.ramirestechgames.ai` 0.1.0 candidata (sem tag), repositório privado
  `GuiJR777/com.ramirestechgames.ai`, submodule do host.
- **Packages existentes:** nenhum alterado (Core 0.4.0, Stats 0.1.1, HFSM 0.1.0, Character 0.2.0, Combat 0.1.0,
  Abilities 0.1.0 nos commits fixados no WOR).

## Entrega 1 — fundação da BT e do Blackboard (AI `94bf5f4`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode (AI) | 59/59 — ciclo de vida, `Parallel` com vários ramos e conflitos, aborts (ordem, um por tick, sem cascata, dentro de `Parallel`), orçamento (parada consistente, nó iniciado sem tick abortado), canais, services, decorators em 4 Δt, utility, random, blackboard, validators |
| PlayMode (AI) | 7/7 — escalonamento por identidade, relógio, escrita externa na avaliação seguinte, evento, pause/resume, disable, identidade inválida, ordem de criação embaralhada |
| Zero GC | `TreeAllocationTests` (árvore com `Parallel`, aborts `Both`, services, `Repeat`, `Cooldown`, `TimeLimit`, utility, random) |
| QG12 (só Core) | 66 testes (59 + 7), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK (AI incluído) |

Desvios: nenhum de contrato. Precisões de implementação registradas no CONTRACTS do package: subárvore sem filhos
falha; `Parallel` com política `Unset` vale `RequireAll` em runtime (erro de validação); `Restart` recria memórias
(aloca), `Resume` mantém.

## Entrega 2 — percepção e alvo (AI `eabad50`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode (AI) | 67/67 — memória (top-K em 6 ordens, esquecimento, saída do mundo, atraso, ameaça, zero GC), caixa de estímulos (top-K × entrega em 5 ordens), validators de perfil/seleção/nós |
| PlayMode (AI) | 24/24 — registro (duplicado, zero, cheio, clones `Combine`, handles velhos e de outro mundo, ordem do snapshot), visão (cone, alcance, time, parede, detectabilidade 0), audição em *n + 1*, estímulo dirigido, caixa com intervalo 5, saturação do mundo, esquecimento, seleção (histerese, empate), invariância com 3 ordens de criação (15 atores, 12 passos) |
| Zero GC | passo completo com 20 agentes e 20 percebíveis (P0, estímulos, 4 sensores, memória, seleção, árvore) |
| QG12 (só Core) | 91 testes (67 + 24), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Desvios e precisões: (1) num agente o `AIAgent` registra o percebível depois de resolver o mundo (o `OnEnable` do
percebível não registra), porque durante a ativação a ordem de `OnEnable` entre componentes não garante o mundo;
(2) `IsSensedNow` significa "percebido na última atualização": um sensor com intervalo maior que a avaliação não
percebe em todas; (3) linha de visão depende de colliders sincronizados pela física (testes chamam
`Physics.SyncTransforms` ao criar paredes); (4) dano de fonte não registrada é ignorado pela memória (a seleção só usa
entidades registradas).

## Entrega 3 — movimento e engajamento (AI `d4eb189`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode (AI) | 68/68 — inclui validators de perfil de engajamento e de nós de movimento/escopo |
| PlayMode (AI) | 38/38 — chegada, `AlignDepthFirst`, desvio de parede, travamento, separação, patrulha, distância/fuga/órbita, facing em `Parallel`, pulo/dash rastreados; engajamento com 6 agentes e 2 tokens (máximo 2 detentores em 400 passos, token retido em todos os ticks do filho, vagas distintas, liberação única por concessão), interrupção, troca de alvo, handle velho, agente desabilitado, revogação por tempo, mesma sequência de concessões em 3 ordens de criação |
| Zero GC | 12 agentes com escopo, movimento até a vaga, desvio, separação, órbita e revogação |
| QG12 (só Core) | 106 testes (68 + 38), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Desvios e precisões: (1) a liberação do token é imediata (síncrona) — concessões continuam só na fase +60, então o
resultado não depende da ordem dos agentes; isso permite reativar o escopo no mesmo tick sem conflito; (2) o lado de
desvio de obstáculo é mantido pelo nó enquanto a sonda acerta (sem isso o agente oscilava no eixo da parede — achado
pelo teste); (3) com score = −distância, os agentes mais próximos recuperam o token: a rotação entre atacantes é
configuração da árvore (ex.: `Cooldown` no escopo), não regra do package.

## Entrega 4 — integrações e rastreamento de comandos (AI `4223e41`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings (host com todos os packages) |
| EditMode (AI) | 85/85 — 17 novos de rastreamento com eventos sintéticos (tabela da §10.3 e regras da §10.3.1) |
| PlayMode (AI, só Core) | 38/38 |
| PlayMode (AI, integrações com packages reais) | 23/23 — Combat (9), Abilities (8), Character (4) e Stats (1), montados pelos campos serializados, sem internals de outros packages |
| Sandbox (HFSM real) | 2/2 — `AiStateMachinePatternPlayModeTests` |
| Regressão completa do WOR | EditMode 828/828 (13 assemblies) · PlayMode 431/431 (11 assemblies + Sandbox) — corrigido na entrega 5: o total EditMode publicado antes (1555) leu um arquivo de resultados antigo para o Core (789 em vez de 62) |
| QG12 (só Core) | 123 testes (85 + 38), 0 ignorados, 0 mensagens do compilador; integration assemblies excluídas pelas `defineConstraints` |
| `check_dependencies` | OK (após declarar Stats nas asmdefs de Combat/Abilities — achado do gate) |

Cenários exigidos pelo ajuste 4 da auditoria e pela §18.1, todos com executores reais:
- **Duas ações iguais enfileiradas:** `PackageOwner` serializa (a segunda espera no bridge e recebe a instância 2);
  `HfsmOwner` correlaciona cada borda pelo token na ordem de consumo do executor.
- **Prioridades diferentes:** `HfsmOwner` com `Heavy` (prioridade 5) entre dois `Light` — instâncias 1 (Heavy), 2 e 3
  pelos tokens certos.
- **Expiração e rejeição:** borda que não executa na janela → `Expired` (Combat e Abilities); caminho divergente
  (`AgentOwner` com posse `Combat`) → `Rejected`, `RequestRefusedCount`, `PathMismatch` e aviso.
- **Consumo `External` pela HFSM:** Sandbox — a borda escrita em `DECISION + 20` é reivindicada pela HFSM real
  (`EvaluationLoop.FixedUpdate`) e o ataque começa **no mesmo passo fixo**; estados `Attack → Grounded`; desfecho por
  token, sem `Unconfirmed`.
- **Interrupção após o início:** golpe recebido durante o ataque → `Interrupted` (+ estímulo de dano e
  `WasHitRecently`); evento `Interrupt` da habilidade → `Interrupted` (+ estímulo do evento).
- **Troca jogador ↔ IA com bordas pendentes:** rastreios `Cancelled`, `ControlLost`, árvore parada e sem escritas;
  `TakeControl` descarta as bordas pendentes do jogador; Sandbox com o mesmo ator e a mesma `StateMachineDefinition`
  alternando jogador → IA → jogador (AC2).
- **`Unconfirmed` provocado:** script ativa outra concessão da mesma tag de entrada no passo em que o ASC consome a
  borda da IA → dois inícios compatíveis → `Unconfirmed` (contado); borda estrangeira no buffer de combate →
  `Unconfirmed`.
- `AgentOwner`: o bridge pede a execução com o token; abandono cancela no executor (borda descartada, nenhuma execução
  posterior).

Desvios e precisões: (1) `JumpOrDashAction` passa a chamar `Abandon` ao parar por qualquer motivo, para liberar a vaga
de rastreio (antes só em abort — vagas ficariam ocupadas); (2) uma rejeição externa com idade ≥ janela é classificada
como `Expired` (o executor reporta `Rejected` quando a borda expira antes da execução); (3) no `PackageOwner`, a
condição 5 da §10.3.1 ("outro início que pudesse consumir a borda") vale para Combat (qualquer ação não nula) e não
para inícios de outra tag de entrada (Abilities) ou de outro tipo de borda (pulo × dash), que não consomem a borda;
(4) os bridges se ligam de forma preguiçosa em `TakeControl` porque o `AIAgent` pode habilitar antes do `Awake` do
bridge no mesmo objeto (achado pelos testes); (5) com `takeControlOnEnable` desligado, o agente com bridges começa sem
controle e não avalia; (6) os testes de integração do package usam um "claimer" que imita um estado `External`; o
padrão com a HFSM real fica na Sandbox, como previsto. Nenhum package existente foi alterado.

## Entrega 5 — ferramentas (AI `3aad7f2`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings (achado e corrigido: CS0108 em `BehaviourNodeView.Children`, renomeado `ChildrenPort`) |
| EditMode (AI) | 97/97 — 12 novos de ferramentas (`EditorToolsTests`) |
| PlayMode (AI + integrações) | 38/38 + 24/24 (novo: outra fonte habilitada do buffer e caminho × posse) |
| Sandbox | 2/2 |
| Regressão completa do WOR | EditMode 840/840 (13 assemblies) · PlayMode 432/432 (11 assemblies + Sandbox) |
| QG12 (só Core) | 135 testes (97 + 38), 0 ignorados, 0 mensagens do compilador (inclui a assembly de Editor) |
| `check_dependencies` | OK |

Validação automatizada (sintética) das ferramentas:
- **Graph Editor:** modelo com um passo de Undo por operação (inclusive várias no mesmo frame), raiz e filhos, ordem
  explícita, recusa de ciclo/filho em folha/segundo filho de escopo, remoção com descendentes, decorators (ordem =
  aninhamento) e services (só composites), troca de tipo, auto layout de cima para baixo, problemas da validação no nó;
  janela com nós e arestas, reconstrução após edição e Undo, subárvore por duplo clique e Voltar, sobreposição de status
  de uma instância em execução (Running/Success, nó fora da árvore compilada sem status, limpeza ao parar de depurar).
- **Captura do Editor:** `d5_graph_editor.png` (janela real via `GUIView.GrabPixels`, árvore de demonstração com
  serviço, decorators com abort, escopo de engajamento, `Parallel`, canais e problemas de validação em vermelho no nó e
  no painel). Capturas em Play Mode (debugger, sobreposição ao vivo e gizmos) serão feitas com os samples da entrega 6.
- **Busca:** só tipos públicos e instanciáveis (sondas internas dos testes ficam de fora).
- **Inspector do Blackboard:** uso das chaves encontrado em árvores do próprio Blackboard ou de descendentes (outros
  Blackboards ignorados).
- **Validação de cena:** dois mundos, id duplicado (erro com os dois objetos), mais percebíveis que a capacidade;
  bridge com outra fonte habilitada do mesmo buffer (aviso) e caminho incompatível com a posse (erro).

Desvios e precisões: (1) as portas seguem a linha padrão do `Node` do GraphView (pai à esquerda, filhos à direita do
cabeçalho, ambas verticais) — tentar centralizá-las acima/abaixo do nó não teve efeito com a folha de estilo padrão; o
fluxo continua de cima para baixo; (2) o glifo de services foi trocado por texto ("Service:") porque a fonte do Editor
não o tem; (3) a avaliação via `unity command eval` voltou a estourar o limite de 5 s do thread principal nesta sessão;
as capturas usam um item de menu temporário (não versionado) executado por `unity command menu`.

### Checklist de validação manual (owner, mouse físico)
Pré-condição: abrir uma `BehaviourTreeDefinition` (duplo clique no asset).
1. Botão direito no fundo → **Create Node…** abre a busca; escolher `Sequence` cria a raiz no ponto clicado.
2. Arrastar da porta de filhos de um composite até a porta de pai de outro nó cria a aresta; o título ganha `#n`.
3. Tentar ligar um nó a um descendente seu, a uma folha ou um segundo filho a um `Engagement Scope`: a aresta não fica.
4. Menu do nó → **Move Left/Right** troca a ordem (`#1`/`#2`); **Sort Children By Position** reordena pela posição.
5. Menu do nó → **Add Decorator ▸ Cooldown** e **Add Service ▸ Select Target**: faixas aparecem no nó; o painel lateral
   edita os campos com o seletor de implementação do Core.
6. Selecionar nós e **Delete**: o nó some com seus descendentes; **Ctrl+Z** restaura; **Ctrl+Y** refaz.
7. Arrastar nós e **Ctrl+Z**: as posições voltam; **Auto Layout** organiza de cima para baixo.
8. Nó com problema fica com borda vermelha e tooltip; o painel "Tree" lista a validação completa.
9. Duplo clique num `Subtree` abre a árvore incluída; **Back** volta.
10. Em Play Mode, selecionar um agente que roda a árvore: nós ativos em azul, sucesso verde, falha vermelha, abortado
    âmbar, com "n steps ago"; o rótulo do topo mostra "Debugging <agente>".
11. **AI Debugger:** lista de agentes; selecionar um mostra árvore (ativos, trabalho/orçamento), Blackboard (editar um
    Bool/Int/Float aplica na avaliação seguinte), memória com pontuações, engajamento, bridges e rastreios, mundo e
    histórico (64).
12. **Gizmos:** agente selecionado mostra cone de visão e proximidade; em Play Mode linhas para percebidos, último ponto
    dos lembrados, alvo em vermelho, vaga de engajamento e velocidade; alvo de engajamento mostra o anel de vagas.
13. Inspector do `BlackboardDefinition`: chaves herdadas e **Find Usage In Behaviour Trees**.
14. Validator do Core (`Tools/RamiresTech Games/Validator`) numa cena com ids duplicados e um bridge com outra fonte
    habilitada: erros e avisos listados.

## Entrega 6 — samples e fechamento (AI `8d4e1b1`, `ce901a3`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings (host com todos os packages) |
| EditMode (AI) | 98/98 — novo: guarda de serialização (`SerializationGuardTests`) |
| PlayMode (AI, só Core) | 42/42 — novos: determinismo e escala (`ScaleAndDeterminismPlayModeTests`) |
| PlayMode (AI, integrações com packages reais) | 25/25 — novo: com todas as vagas de rastreio ocupadas a ação espera e executa quando uma vaga é liberada |
| Sandbox (HFSM real) | 2/2 — `AiStateMachinePatternPlayModeTests` (BT → buffers → HFSM e troca jogador ↔ IA) |
| Regressão completa do WOR | EditMode 841/841 (13 assemblies: Core 62, Stats 126, HFSM 118, Character 164 + 17 + 4 + 5, Combat 164 + 9 + 2, Abilities 70 + 2, AI 98) · PlayMode 437/437 (11 assemblies + Sandbox: Stats 4, HFSM 5, Character 85, Combat 105 + 20 + 3, Abilities 108 + 38, AI 42 + 25, Sandbox 2) |
| QG12 (só Core) | 140 testes (98 + 42), 0 ignorados, 0 mensagens do compilador; integration assemblies excluídas pelas `defineConstraints` |
| `check_dependencies` | OK |
| AC10 (nomes de jogo) | nenhuma ocorrência de nomes do WOR em `Runtime`, `Editor` e `Samples~` |

### Determinismo e escala (AC4, AC9)
- **Criação embaralhada:** `ShuffledCreation_ProducesTheSameLogicalTrace` cria agentes e percebíveis em três ordens
  (sementes 0, 3 e 11) e compara, a cada passo fixo, o traço lógico gravado em `DECISION + 90`: alvo, nós ativos,
  estado e vaga de engajamento e comando de movimento arredondado — traços idênticos.
- **Tempo variável:** `timeScale` 0,5 com `captureFramerate` 24 produz as mesmas decisões por passo fixo que a
  referência (equivalência lógica, não bit a bit da PhysX).
- **Zero GC com 50 agentes:** `FiftyAgents_WarmedFullSteps_DoNotAllocate` — 50 agentes e 30 alvos com sensores,
  seleção, engajamento e movimento; passos completos aquecidos sem alocação; **0,421 ms por passo** (média registrada
  pelo teste; Editor, máquina de desenvolvimento).
- **Fonte de tempo:** com `IAITimeSource` pausada o agente não avalia nem avança o relógio; na retomada o Δt conta só
  os passos não pausados (sem salto); os bridges continuam amostrando. `CombatHitstopTimeSource` liga o hitstop do
  Combat a essa fonte (opt-in, por componente).

### Samples (QG4, AC1, AC2)
Ambos validados por execuções roteirizadas em Play Mode (itens de menu e sondas temporários, não versionados) e por um
teste de importação (cópia de `Samples~` para `Assets/`, cenas em Play Mode sem nenhuma mensagem no console). Capturas:
`d6_basics_game.png`, `d6_arena_game.png`, `d6_graph_live.png` (sobreposição ao vivo do Graph Editor),
`d6_debugger_live.png` (AI Debugger em Play Mode), `import_AIBasics.png`, `import_AICombatArena.png`.

**AI Basics** (AI + Character, só assets — guarda e batedor):
- 0,5 s: o guarda patrulha (`Return And Patrol`); o batedor vagueia (`Wander`).
- 3,0 s: o guarda vê o intruso pelo cone com paredes e persegue (`Chase (seen) › Run To Intruder`).
- 3,1–6,5 s: o intruso se esconde atrás de uma cobertura; o guarda vai até a última posição e procura
  (`Search Last Position › Look Around`, memória `old`); 8,0 s: reencontra e volta a perseguir.
- 13,1 s: intruso colocado 4 m atrás do guarda (fora do cone): busca; 13,7 s: barulho atrás do guarda → audição, vira e
  persegue (`Chase`).
- 14,6 s: intruso a 2 m do batedor → `Flee`; acima de 6 m volta a `Wander`.

**AI Combat Arena** (AI + Stats + Character + Combat + Abilities + HFSM; mesmo prefab `ArenaFighter` e mesma
`StateMachineDefinition` para jogador e IA):
- Tab troca o herói para IA (0,5 s) e de volta para o teclado (9,0 s): árvore parada sem escritas, rastreios
  `Cancelled` (AC2, também coberto na Sandbox).
- Tokens de engajamento no herói: no máximo **3 detentores** (2 corpo a corpo + 1 à distância) durante toda a
  execução.
- Brawler em combos, Skirmisher em *hit and run* (`Retreat` depois do golpe), Bruiser alterna guarda (`Guard Up`) e
  pesado; Champion em fase 1 com combos e, com a vida em 40% (6,5 s), **fase 2** (`StatCondition`, abort Both) com
  `Bolt` e `Smash`.
- Caster: sonda de 20 s (1000 passos) com **14 ativações de `Bolt`** pelo `ActivateAbilityAction` (turno à distância →
  distância 5–7 m → face → bolt → recuperação). O contador "attacks" do roteiro conta só ataques do Combat; o bolt é
  uma habilidade, por isso o Caster aparecia com 0.

### Correção encontrada pelos samples
Campos privados de runtime sem `[NonSerialized]` são serializados pela Unity através do domain reload de entrar em Play
Mode: o array nulo do registro do `AIWorld` voltava **vazio** e o mundo criado na cena ficava com capacidade 0
("registry is full"). Os testes nunca pegaram porque criam o mundo em runtime. Todo estado de runtime de `AIWorld`,
`AIEngagementTarget` e dos bridges foi marcado `[NonSerialized]`; `SerializationGuardTests` exige que todo campo privado
dos componentes do package seja `[SerializeField]` ou `[NonSerialized]`.

Desvios e precisões: (1) `IAITimeSource` (previsto na §3/§17) entra nesta entrega com `CombatHitstopTimeSource`;
(2) com todas as vagas de rastreio do bridge ocupadas, as ações rastreadas ficam `Running` e tentam de novo até
`MaxWait` (antes falhavam no ato); `JumpOrDash` espera enquanto a locomoção pode se mover; (3) o sample
`Getting Started` do template foi removido; (4) `ITargetSelector` (listado na §3/§17) não foi criado: a seleção de alvo
é configurada por `TargetSelectionDefinition` com `ITargetConsideration` (extensível), e um seletor plugável é mudança
aditiva futura; (5) as árvores dos samples usam `Wait And Rethink` (`WaitAction` 0,25 s) como último ramo — um
`IdleAction` ali prendia o agente que não tinha alvo no primeiro tick; (6) os samples não têm testes automatizados no
package (vivem em `Samples~`); os roteiros de validação ficaram fora do repositório. Nenhum package existente foi
alterado.

## Release candidate — AI 0.1.0 (sem tag)

| Entrega | Commit do AI | Commit do WOR |
|---|---|---|
| 1 — fundação da BT e do Blackboard | `94bf5f432187d819e59d58f0410a4dc5f4c5a744` | `f13daf1` |
| 2 — percepção e alvo | `eabad508ee9f99790365a531a5c57233894b0a09` | `ca1490f` |
| 3 — movimento e engajamento | `d4eb1897767b594025d6a3929f58bc42a4249129` | `bbdc985` |
| 4 — integrações e rastreamento | `4223e414ae04156244d9a006b18dc751255568ac` | `50dfc6d` |
| 5 — ferramentas | `3aad7f2fdd3e118fcd06e6f45593cf9cd69ecdf6` | `842c2e7` |
| 6 — samples e fechamento | `8d4e1b19da05f0b16cb44baf4553206637a64f02` + `ce901a3920d2f0b9f6f5cb8642afac5fd1b526a2` (doc) | commit que fixa `ce901a3` |

Critérios de aceite atendidos por testes: AC1 (samples só com assets; domínio sem referências a motor, combat ou ASC),
AC2 (Sandbox e arena), AC3 (entrega 1), AC4 (entregas 2, 3 e 6), AC5–AC6 (entrega 2), AC7 (entrega 3), AC8 (entrega
4), AC9 (entrega 6), AC10 (sem nomes de jogo) e AC12 (QG12 só com Core). **AC11 parcial:** modelo do grafo, debugger,
gizmos e validators validados por automação; a validação com mouse físico continua pendente do owner.

Riscos e limitações abertos:
- **R41** — uma borda abandonada em `HfsmOwner`/`PackageOwner` pode executar dentro da janela do consumidor; mitigada
  por janelas curtas, `ClearChannel` e `AgentOwner`; `Retract` nos buffers exige ADR e minor de três packages (§20.2).
- **R37** — `PackageOwner` correlaciona sem token (regra de borda única; ambiguidades viram `Unconfirmed`, contadas).
- **R42** — a ordem `AttackStarted` → `ActionResolved` do Combat é coberta por teste de contrato, mas ainda não está
  registrada no CONTRACTS do Combat.
- **R38** — sem NavMesh: navegação direta com desvio; travamento → `Failure`.
- Ferramentas e automação: portas do Graph Editor na linha padrão do `Node` do GraphView (cosmético);
  `unity command eval` estoura 5 s nesta máquina (validações por itens de menu temporários); em Play Mode por CLI com
  o Editor sem foco, o player loop só avança com `Application.runInBackground` ligado em runtime.
- Os packages do M4/M5 continuam sem tag e sem validação manual do owner; o AI 0.1.0 depende deles só nas integration
  assemblies.

### Checklist de validação manual do owner (samples)
Pré-condição: Package Manager › RamiresTech Games - AI › Samples › importar **AI Basics** e **AI Combat Arena**.
1. **AI Basics:** WASD move o intruso; o guarda patrulha, vê pelo cone (não através das paredes), persegue, procura a
   última posição quando o intruso se esconde e volta à patrulha depois de esquecê-lo (4 s).
2. N faz barulho: o guarda que não vê o intruso vira e investiga.
3. Aproximar-se do batedor: ele foge e, acima de 6 m, volta a vaguear.
4. Tab troca o agente do painel; o painel mostra nós em execução, alvo, Blackboard, caixa de estímulos e memória.
5. **AI Combat Arena:** Tab troca o herói entre teclado e IA sem travar ataques pela metade; o mesmo prefab luta nos
   dois modos.
6. Nunca mais de 3 atacantes no herói (painel "attackers"); o Caster dispara bolts à distância; o Bruiser defende.
7. Com a vida do Champion abaixo de 50%, ele muda para a fase 2 (bolts e pesados).
8. R recarrega a arena; um lutador nocauteado sai e libera os tokens.
9. Com um agente selecionado: AI Debugger, sobreposição ao vivo no Graph Editor e gizmos (itens 10–12 do checklist das
   ferramentas, acima).
