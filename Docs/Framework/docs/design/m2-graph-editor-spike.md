# M2 — Spike: tecnologia de Graph Editor

- **Status:** concluído, para revisão · **Data:** 2026-10-09 · **Unity:** 6000.6.5f1
- **Pergunta:** qual tecnologia usar nos editores de grafo do framework (HFSM no M2, Combo Graph no M4, Behaviour Tree no
  M6)? Risco R1 do roadmap.
- **Método:** inspeção da API instalada (reflexão no Editor) + dois protótipos descartáveis em `Assets/_GraphSpike`
  (removidos após o spike; nada foi commitado), exercitados pelo Unity CLI.

## Opções avaliadas

| # | Opção | Situação no Unity 6000.6.5f1 |
|---|---|---|
| A | **Graph Toolkit** (`Unity.GraphToolkit.Editor`) | **Módulo nativo** do Editor (`UnityEditor.GraphToolkitModule`), 51 tipos públicos: `Graph`, `Node`, `ContextNode`/`BlockNode`, subgraphs, `GraphDatabase`, `GraphVisualization` (debug). Sem `[Experimental]`/`[Obsolete]` nos tipos exportados. |
| B | **GraphView** (`UnityEditor.Experimental.GraphView`) | Módulo nativo (`UnityEditor.GraphViewModule`). Namespace *Experimental*, **sem** `[Obsolete]`; compilou com 0 warnings. |
| C | Canvas próprio em UI Toolkit | Não prototipado (estimativa). |
| D | IMGUI próprio | Descartado (legado, desempenho e UX inferiores). |
| E | Terceiros (xNode, NodeGraphProcessor — MIT) | Descartado: regra do framework proíbe dependências externas nos packages. |

## Protótipo A — Graph Toolkit
Grafo `[Graph("hfsmspike", SupportsSubgraphs)]`, nó de estado com portas *Enter*/*Transitions* e opções (bool,
referência a `ScriptableObject`, valor polimórfico), subgraph local e um `ScriptedImporter` que converte o grafo em um
ScriptableObject de runtime.

| Verificação | Resultado |
|---|---|
| Criar grafo, adicionar 3 nós, conectar 2 transições por código | ✅ |
| Subgraph local (`CreateLocalSubgraphNode`) — base para hierarquia | ✅ |
| `Title`/`Position` editáveis; opção `bool` persistida e lida no importer | ✅ |
| Importer (`LoadGraphForImporter`) gerando o SO de runtime (3 estados, 2 transições) | ✅ |
| `GraphVisualization`: contexto criado, `FillAmount` por nó aplicado (debug ao vivo) | ✅ API presente (efeito visual exige o grafo aberto em janela) |
| **Opção polimórfica (`[SerializeReference]`)** | ❌ **Não persiste.** Gravada como `Constant<SpikeBehaviour>` com `data:` vazio; o importer recebe `null`. Só parecia funcionar via `LoadGraph` porque o grafo estava em cache na memória. |
| Tamanho do arquivo (4 nós) | ~15 KB de YAML com IDs internos (diffs ruidosos) |

**Consequência:** com Graph Toolkit, cada tipo de comportamento precisaria virar um **tipo de nó do Editor** (ou um asset
ScriptableObject por estado) e um importer faria o mapeamento para os tipos de runtime. Isso duplica cada tipo de
estado/condição/nó de BT (classe de Editor + classe de runtime) e tira o designer do fluxo `[SerializeReference]` +
seletor do Core (ADR-0008) que já usamos no Stats.

## Protótipo B — GraphView
`GraphView` com grade, zoom, arraste, seleção, minimapa; os nós e arestas são **uma vista** do mesmo ScriptableObject de
runtime (lista de estados com `[SerializeReference]` e lista de transições).

| Verificação | Resultado |
|---|---|
| Janela com 3 nós e 1 aresta construídos a partir do SO | ✅ |
| Criar transição pela vista grava no SO | ✅ |
| Undo desfaz a transição (SO volta a 1) | ✅ |
| Reconstruir a vista a partir dos dados | ✅ |
| Destaque de nó (para o debugger) | ✅ |
| Comportamento polimórfico persistido no asset | ✅ (`SpikeMoveBehaviour`, `speed: 7`) |
| Tamanho do asset (3 estados) | ~1,4 KB |
| Warnings de compilação | 0 |

**Custos conhecidos:** copiar/colar, janela de busca de nós, sincronização vista ↔ dados e layout precisam ser escritos
por nós (o Graph Toolkit traz isso pronto). Namespace *Experimental* sem evolução de features.

## Comparação

| Critério (peso) | A — Graph Toolkit | B — GraphView | C — UI Toolkit próprio |
|---|---|---|---|
| Dados = nossas definições (SO + `[SerializeReference]`, seletor do Core) (alto) | ❌ exige nós de Editor por tipo + importer | ✅ edição direta | ✅ |
| Hierarquia / navegação de sub-máquinas (alto) | ✅ subgraphs nativos | ⚠️ implementar (breadcrumb + vista por nível) | ⚠️ implementar |
| Debug ao vivo (alto) | ✅ `GraphVisualization` (nó/fio) | ✅ estilos nos elementos | ✅ |
| Undo / copiar-colar / busca (médio) | ✅ prontos | ⚠️ Undo via SO ✅; copiar/colar e busca a implementar | ❌ tudo a implementar |
| Diffs e merge do asset (médio) | ⚠️ ~10× maior, IDs internos | ✅ compacto, nosso formato | ✅ |
| Direção da Unity / longevidade (médio) | ✅ sucessor oficial, módulo nativo | ⚠️ manutenção, namespace Experimental | ✅ sob nosso controle |
| Esforço para HFSM + Combo + BT (médio) | Médio-alto (duplicação por tipo) | Médio | Alto (2–3 semanas só de canvas) |

## Recomendação
1. **Usar GraphView no M2**, como *vista* sobre as definições ScriptableObject (fonte da verdade continua sendo o SO com
   `[SerializeReference]`). Nenhum dado de runtime depende do GraphView.
2. **Isolar o GraphView** atrás de uma camada fina de Editor (modelo de edição testável sem UI + vista). Trocar de
   tecnologia no futuro afeta só o Editor, nunca o formato dos assets.
3. **Infra compartilhada:** começa em `RamiresTechGames.HierarchicalStateMachine.Editor` (único consumidor no M2). No M4
   (Combo Graph, segundo consumidor) extrair para `Core.Editor` com ADR — regra de ≥ 2 consumidores.
4. **Reavaliar o Graph Toolkit no M4 e no M6** com dois gatilhos: (a) suporte a valores polimórficos em opções de nó,
   ou (b) evidência de deprecação do GraphView (`[Obsolete]`/remoção).

## Riscos e mitigação
| Risco | Mitigação |
|---|---|
| GraphView ser marcado obsoleto/removido | Camada de vista isolada; dados independentes; gatilho de reavaliação em cada milestone de grafo. |
| Recursos que o Graph Toolkit daria de graça (busca, copiar/colar) | Busca via `SearchWindow` do próprio GraphView; copiar/colar fora do escopo do M2 (ver design). |

## Evidências
Protótipos arquivados fora do repositório (scratch da sessão). Comandos: reflexão sobre `UnityEditor.GraphToolkitModule`;
`GraphDatabase.CreateGraph/SaveGraph/LoadGraph`, importer, `GraphVisualization.Registry`; janela GraphView aberta e
manipulada via `eval`. Console sem erros ou warnings após remover os protótipos.
