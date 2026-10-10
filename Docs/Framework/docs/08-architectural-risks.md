# 08 — Riscos arquiteturais

Probabilidade (P) e Impacto (I): A = alto, M = médio, B = baixo.

## Riscos do framework

| # | Risco | P | I | Mitigação | Quando |
|---|---|---|---|---|---|
| R1 | **Tecnologia de graph editor.** Três editores de grafo (HFSM, Combo, BT). `GraphView` é experimental e sem evolução; o Graph Toolkit da Unity ainda está amadurecendo. Escolha errada = 3× retrabalho. | A | A | Spike no M2 com ADR; uma infraestrutura de grafo compartilhada só se as três usarem a mesma tecnologia (aí sim candidata a package `graph` ou ao Core.Editor, com ADR). | M2 ✔ spike: GraphView agora, reavaliar Graph Toolkit no M4/M6 ([spike](design/m2-graph-editor-spike.md)) |
| R2 | **Explosão de integration assemblies.** Cada par de packages pode gerar adapters; manutenção combinatória. | M | M | Integração só com caso de uso concreto no Sample/Sandbox; cada adapter com teste; grafo limita os pares possíveis. | Contínuo |
| R3 | **Version skew entre 8 repositórios.** Combinações não testadas no jogo. | A | M | `Assets/_FrameworkSandbox` é a verdade de integração; `COMPATIBILITY.md` com conjuntos validados; `versionDefines` com faixas. | M1+ |
| R4 | **Fragilidade de `[SerializeReference]`.** Renomear/mover classe apaga dados silenciosamente. | A | A | `[MovedFrom]` obrigatório; tipos SR `sealed` com nomes estáveis; teste que carrega assets de sample; nota no CHANGELOG. | M2+ |
| R5 | **Colisão de GUIDs** ao copiar `.meta` entre packages. | M | A | Template sem `.meta`; Unity gera no primeiro import; commit dos `.meta` gerados. | M0 ✔ |
| R6 | **Core vira monólito.** | M | A | Regra: ≥ 2 consumidores reais + ADR. Core M0 tem só ExecutionOrder e Validation. | Contínuo |
| R7 | **Estado mutável em ScriptableObject** (alterações em Play Mode persistem no asset). | M | A | Definições sem setters públicos; instâncias de runtime; revisão de código e teste de "asset inalterado após Play". | M1+ |
| R8 | **Timing de combate:** hitboxes em `FixedUpdate` × animação em `Update`; hitstop; tempo por frames × segundos. | A | A | ADR no M4: timing data-driven em segundos (ou ticks fixos) independente do Animator; adapter opcional para sincronizar com animação. | M4 |
| R9 | **Orquestração cross-domain** (ex.: atacar trava movimento; parry interrompe ability). | A | M | Feita pela composição de estados na HFSM + políticas configuráveis nos estados de integração; nunca chamadas diretas entre packages fora do grafo. | M3–M5 |
| R10 | **Escopo do GAS** cresce para nível Unreal. | M | A | Lista fechada de requisitos do Master Prompt; o que não está nela vai para ROADMAP futuro. | M5 |
| R11 | **GC/perf** em BT, HFSM, efeitos e overlaps. | M | M | `NonAlloc`, buffers pré-alocados, testes `Is.Not.AllocatingGCMemory`, gate de perf no M8. | Contínuo |
| R12 | **Especificidades 2.5D vazando** para o Character genérico (lanes de profundidade, flip de sprite). | M | M | Character é 3D genérico; restrições de plano/eixo são configuração do profile; flip de sprite é presentation. | M3 |
| R13 | **Versão do Editor:** 6000.6 pode não ser LTS; APIs de física/UI Toolkit mudam entre streams. | M | M | `"unity": "6000.6"` no package.json; ampliar faixa só após testar em outra versão. | M0 ✔ |
| R14 | **Determinismo/rede** não é objetivo. Se um jogo consumidor precisar de multiplayer, a simulação baseada em Rigidbody não é determinística. | B | A | Declarado como não-objetivo; domínio puro facilita evolução futura. | — |
| R15 | **Overhead operacional** de muitos repos para um time pequeno. | A | M | Scripts (`new_package`, `check_dependencies`), um único projeto host, convenções idênticas, CLAUDE.md por repo. | Contínuo |
| R16 | **Debuggers em build de release.** | B | M | Código de debug em assemblies Editor ou sob `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`. | Contínuo |
| R17 | **Acoplamento acidental ao jogo.** Packages são desenvolvidos dentro do projeto do WOR; é fácil um package passar a depender de código, assets ou cenas do jogo. | M | A | `check_dependencies.py` rejeita referência de package a qualquer assembly fora do framework/Unity; código de jogo só em `Assets/`, código de framework só em `Packages/com.ramirestechgames.*`; testes de package sem cenas/assets do jogo; samples verificados no host e removidos; QG12 (instalação em projeto vazio descartável). | Contínuo |
| R19 | **Recriação de pasta temporária em testes de Editor (instável no host).** Em algumas execuções no WOR, `AssetDatabase.CreateFolder` logo após apagar a mesma pasta devolveu o GUID antigo sem recriar a pasta; falharam `SelectImplementationTests.SaveAsset_...` (Core) e casos `ResourceDefinition` de `StableIdTests` (Stats). Causa não identificada; reproduz com as assemblies da HFSM excluídas; não ocorre no QG12. Sonda isolada (2026-10-10): a falha só apareceu quando o asset da pasta tinha tipo `[SerializeReference]` ausente **e** registro de Undo (sequência do teste de tipo ausente do Core), mas a mesma combinação passou na execução seguinte; não determinística. | M | B | Registrado em COMPATIBILITY; testes aprovados não alterados sem causa identificada; investigar (pasta única por teste ou `Refresh` após apagar) se voltar a ocorrer. | Aberto (M2) |
| R18 | **Arquitetura sem legado = escopo aberto.** Sem código anterior para limitar decisões, cada sistema pode crescer além do necessário. | M | M | Requisitos do Master Prompt são a lista fechada de cada milestone; extras vão para "Futuro" no ROADMAP do package. | Contínuo |

## Referência de mecânicas (inspiração, não migração)

O novo Whiskers of Rage é construído **do zero** sobre estes packages. O projeto WOR antigo (`GuiJR777/WOR`) e o
**BeatEmUpTemplate2D** (Osarion) **não** são referência arquitetural, não têm requisito de retrocompatibilidade e
nenhuma classe, API, prefab ou estrutura deles é preservada.

O template serve apenas como **inspiração de game feel** para o estudo de design do M4:

| Mecânica | O que observar no estudo do M4 |
|---|---|
| Combos | Sequências leve/pesado, ramificações, janela de continuação e reset. |
| Hit-confirm | Combo só continua/ramifica quando o golpe conecta; sensação de impacto (hitstop). |
| Agarrões e arremessos | Agarrar ao encostar, golpes durante o agarrão, arremesso que atinge outros alvos. |
| Reações aos golpes | Knockback, launch, knockdown, levantar, stun após parry. |
| Defesa | Bloqueio e janela de parry. |

**Regra de licença:** o template é um asset da Unity Asset Store. Nenhum código, sprite, áudio ou cena dele entra
em qualquer repositório do framework.
