<!--
CÓPIA SINCRONIZADA. Fonte única de verdade: Docs/Framework/docs/CONVENTIONS.md
Não edite aqui: altere a fonte e copie novamente. Links relativos abaixo apontam para Docs/Framework/docs/.
-->

# RamiresTech Games — Convenções de Desenvolvimento Unity 6

> **Versão:** 1.0.0 · **Status:** vigente
> **Fonte única de verdade.** Cópias (ex.: `Assets/Dev/Unity6_Development_Rules.md` em um projeto de jogo) devem ser
> sincronizadas a partir deste arquivo, nunca editadas diretamente.

Este documento consolida três fontes:

| Fonte | Conteúdo |
|---|---|
| **Master Prompt v1.2** (Framework Engineer) | Arquitetura, packages, Editor-first, quality gates |
| `Unity6_Development_Rules.md` (regras escritas anteriormente; só as regras, não o código) | SOLID, nomes, cabeçalho, formatação, performance |
| `unity_development_rules_ptbr.txt` (RamiresTech) | Namespaces, regions, Header+Tooltip, módulos reutilizáveis |

**Precedência em caso de conflito:** Master Prompt > este documento > demais fontes.
Todos os conflitos encontrados estão resolvidos explicitamente na [Seção 15](#15-conflitos-resolvidos).

---

## 1. Princípios

1. **Simples > Esperto · Explícito > Implícito · Legível > Curto.**
2. Alta coesão, baixo acoplamento. Uma classe = uma responsabilidade.
3. **Composition over inheritance.** Herança apenas para hierarquias rasas e estáveis (ex.: base de nó de BT).
4. **SOLID pragmático:** interfaces nos limites entre packages e onde existem ≥ 2 implementações reais ou
   uma costura de teste necessária. Nunca "interface por padrão".
5. **Data-driven:** comportamento configurado por ScriptableObjects e dados serializados, regras em código.
6. **Simulation × Presentation:** a simulação nunca lê Animator, SpriteRenderer, áudio ou UI.
   A apresentação observa a simulação (eventos/estado) e nunca a altera.
7. **Glue code** conecta sistemas. Sistemas não se conhecem diretamente fora do grafo de dependências oficial.
8. **Ordem de execução previsível** (ver [Seção 9](#9-ordem-de-execução-e-eventos)).
9. **Editor-first:** casos de uso comuns são resolvidos no Inspector, sem editar código do package.
10. **Sem abstrações, patterns ou dependências desnecessárias.** Um pattern entra quando resolve um problema
    concreto presente, não um hipotético.

---

## 2. Camadas de código em um package

| Camada | Onde | Pode depender de | Regras |
|---|---|---|---|
| **Domain** | `Runtime/<Area>/` (classes puras) | .NET, `UnityEngine` math/tipos de valor, Core | Sem `MonoBehaviour`. Recebe `deltaTime` por parâmetro. Testável em EditMode. |
| **Unity Integration** | `Runtime/<Area>/` (`MonoBehaviour`/`ScriptableObject`) | Domain | Ciclo de vida, serialização, física. Fino: delega ao Domain. |
| **Presentation** | `Runtime/Presentation/` ou assembly próprio | Domain, Integration | Animator/VFX/SFX/sprites. Opcional. Nunca escreve no Domain. |
| **Integration (cross-package)** | `Runtime/Integration/<Outro>/` com asmdef própria | Packages opcionais do grafo | Ver [ADR-0002](adr/0002-optional-integration-assemblies.md). |
| **Editor** | `Editor/` | Tudo acima | Inspectors, drawers, janelas, validators, gizmos. |

---

## 3. Nomes

| Elemento | Convenção | Exemplo |
|---|---|---|
| Package | `com.ramirestechgames.<dominio>` | `com.ramirestechgames.stats` |
| Namespace / Assembly raiz | `RamiresTechGames.<Sistema>` | `RamiresTechGames.Stats` |
| Namespace interno | raiz + caminho de pastas a partir de `Runtime/` (ou `Editor/`) | `Runtime/Modifiers/` → `RamiresTechGames.Stats.Modifiers` |
| Classes, métodos, propriedades, eventos | `PascalCase` | `StatCollection`, `TryConsume` |
| Campos privados (inclusive `[SerializeField]`) | `_camelCase` | `_baseValue` |
| Parâmetros e locais | `camelCase` | `deltaTime` |
| Constantes (`const` e `static readonly` imutáveis) | `UPPER_SNAKE_CASE` | `MAX_STACK_COUNT` |
| Interfaces | `I` + PascalCase | `IValidatable` |
| Booleanos | prefixo `Is/Has/Can/Should` | `IsGrounded` |
| Eventos C# | verbo no passado | `StatChanged`, `AttackStarted` |
| Handlers | `Handle` + evento | `HandleStatChanged` |
| Métodos Try | `Try` + verbo, `out` + retorno `bool` | `TryGetResource(def, out pool)` |
| ScriptableObject de definição | sufixo `Definition` | `AttackDefinition` |
| Componente de entrada de comandos | sufixo `CommandBuffer` | `CharacterCommandBuffer` |

**Sem abreviações.** Exceções permitidas (siglas consagradas): `AI`, `UI`, `HUD`, `VFX`, `SFX`, `Id`, `Min`, `Max`.
O id de package `hfsm` é mantido por decisão do Master Prompt, mas o código usa o nome completo:
`RamiresTechGames.HierarchicalStateMachine`.

---

## 4. Estrutura de arquivo

```csharp
// Purpose: Describe in one sentence what this type does.
using System;
using System.Collections.Generic;

using UnityEngine;

using RamiresTechGames.Core;

namespace RamiresTechGames.Stats.Modifiers
{
    public sealed class Example
    {
        // ...
    }
}
```

1. **Linha 1:** `// Purpose: ...` (obrigatório).
2. **Usings** em grupos separados por linha em branco: `System` → `Unity` → pacotes externos → projeto.
3. Um tipo público por arquivo; nome do arquivo = nome do tipo.
4. Namespace em bloco (Unity 6 compila C# 9 — sem file-scoped namespace).
5. **Final do arquivo:** exatamente uma quebra de linha.
6. Classes `sealed` por padrão; abrir para herança é uma decisão consciente.

### 4.1 Regions (padrão RamiresTech)

Obrigatórias em `MonoBehaviour`, `ScriptableObject`, classes de Editor e classes com mais de ~80 linhas.
Tipos pequenos (structs, enums, interfaces, classes curtas) podem omiti-las.
Ordem (omitir regions vazias):

```csharp
#region Constants
#region Properties
#region On Editor Editable          // [SerializeField] com [Header] e [Tooltip]
#region Events                      // substitui "Public Variables" (campos públicos são proibidos)
#region Private/Protected Variables
#region MonoBehaviour Methods       // Awake, OnEnable, Update, OnValidate...
#region Private/Protected Methods
#region Public Methods
```

---

## 5. Formatação

- Chaves **Allman** (chave em linha própria), indentação de 4 espaços.
- Limite de **120 colunas**.
- `.editorconfig` do template é obrigatório em todos os repositórios.
- **Tipos explícitos.** `var` permitido apenas quando o tipo aparece literalmente à direita (`new Foo()`)
  ou para tipos anônimos.
- **Early return**; condições compostas viram variáveis/métodos com nome (`bool canJump = ...;`).
- Sem números mágicos: `const`, `static readonly` ou dados em ScriptableObject.
  Exceções: `0`, `1`, `-1` como identidades aritméticas e `0.5f` para metade.

---

## 6. Regras Unity

### 6.1 Inspector

- Campos serializados: `[SerializeField] private`. **Campos públicos são proibidos.**
- Todo `[SerializeField]` tem `[Tooltip]` e está sob um `[Header]`.
- Use `[Min]`, `[Range]`, `[Delayed]` e clamps em `OnValidate` para impedir valores inválidos.
- Renomear campo serializado exige `[FormerlySerializedAs]`.
- Dados polimórficos: `[SerializeReference]` + `[MovedFrom]` ao mover/renomear tipos (ver Riscos).
- Menus padronizados:
  - `[CreateAssetMenu(menuName = "RamiresTech Games/<Sistema>/<Nome>")]`
  - `[AddComponentMenu("RamiresTech Games/<Sistema>/<Nome>")]`
  - Janelas: `Tools/RamiresTech Games/<Sistema>/<Nome>`
  - Use as constantes de `<Sistema>PackageInfo` para esses caminhos.

### 6.2 Ciclo de vida e referências

- `MonoBehaviour` = ciclo de vida e integração. Lógica de domínio em classes puras.
- Física em `FixedUpdate`; nada pesado em `Update`.
- Valide dependências em `Awake`/`OnValidate`; erro com contexto: `Debug.LogError(message, this)`.
- Use `[RequireComponent]` e `[DisallowMultipleComponent]` quando aplicável.
- Proibido em runtime: `Find*`, `SendMessage`, `Resources.Load` para lógica de gameplay.
- Ordem de execução com `[DefaultExecutionOrder(ExecutionOrder.<BANDA>)]` de `RamiresTechGames.Core`.

### 6.3 ScriptableObjects

- Representam **definições** (dados imutáveis em runtime). **Nunca** escreva em campos de um SO em runtime.
- Estado mutável vive em instâncias de runtime (`StatCollection`, `AbilityInstance`, ...).
- Definições implementam `IValidatable` (Core) para aparecer no Validator.

### 6.4 Performance

- Sem `new`, LINQ, closures, boxing ou concatenação de string em caminhos por frame.
- Física com APIs `NonAlloc` e buffers pré-alocados.
- Pooling com `UnityEngine.Pool` quando há instanciação recorrente. Sem framework de pool próprio.
- Cache de componentes; nada de `GetComponent` por frame.
- Debug/inspeção de runtime: somente Editor ou `DEVELOPMENT_BUILD`.

---

## 7. Tratamento de erros

- Nunca engula exceções. Se capturar, logue com contexto e relance ou trate de forma explícita.
- Prefira o padrão `Try` a exceções para fluxo esperado.
- Sem spam de log em `Update`: logue uma vez (flag) ou use o Validator.
- Configuração inválida é detectada no Editor (Validator/`OnValidate`), não no meio do gameplay.

---

## 8. API pública e documentação no código

- Todo tipo/membro público de um package tem XML doc (`/// <summary>`).
- Comentários explicam **por quê**, não **o quê**.
- A superfície pública é listada em `CONTRACTS.md` com nível de estabilidade.
- `internal` por padrão; `public` apenas para o que é contrato. Testes usam `InternalsVisibleTo`.

---

## 9. Ordem de execução e eventos

- **Simulação** roda em ticks explícitos com ordem definida pelas bandas de `ExecutionOrder` (Core):
  `COMMAND_SOURCES → DECISION → STATE_MACHINE → ABILITIES → COMBAT → CHARACTER → STATS → PRESENTATION → DEBUG`.
- **Eventos** servem para *notificar* (apresentação, UI, debug, áudio, glue code).
  Não construa cadeias de regras de gameplay disparadas por eventos entre packages: o fluxo de simulação
  deve ser legível seguindo chamadas de método.
- Eventos de contrato (listados em `CONTRACTS.md`) usam `event Action<TArgs>` com `TArgs` `readonly struct`.

---

## 10. Testes

- **Não usamos TDD.** Testes são escritos após a implementação, dentro do mesmo milestone, e são gate de entrega.
- Detalhes: [05-testing-strategy.md](05-testing-strategy.md).
- Nome: `Metodo_Condicao_ResultadoEsperado`.

---

## 11. Estrutura de pastas

### 11.1 Packages
Definida pelo template: [03-repository-structure.md](03-repository-structure.md).

### 11.2 Projetos de jogo (WOR e futuros)
Padrão RamiresTech, com `RTG_Unity_Modules` substituído por packages UPM:

```
Assets/
  Scripts/        <- glue code e código específico do jogo (namespace RamiresTechGames.<Jogo>.<Caminho>)
    Characters/Player/
  Prefabs/Characters/Player/
  Data/           <- assets de definição (ScriptableObjects) do jogo
  Resources/      <- evitar; somente quando inevitável
  Audio/SFX/  Audio/Music/
  Graphics/Textures/  Graphics/Sprites/Characters/Player/
  Scenes/
  Dev/            <- documentação de desenvolvimento
  _FrameworkSandbox/  <- Integration Sandbox do framework (somente no host de desenvolvimento)
Packages/
  com.ramirestechgames.*   <- submodules (um repo por package)
```

---

## 12. Dependências de terceiros

- Packages do framework **não** dependem de Input System, Cinemachine, Feel ou qualquer asset da Asset Store.
  Input System é permitido apenas em *integration assemblies* opcionais.
- Cinemachine e Feel são usados no projeto do jogo (apresentação/glue).
- **Nunca** copie código de terceiros (ex.: BeatEmUpTemplate2D, da Asset Store) sem licença compatível.
  Referências de terceiros servem apenas como inspiração de mecânicas.

---

## 13. Git

- Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`; `!` para breaking change.
- Branches: `main` (sempre compilável) + `feat/<assunto>`, `fix/<assunto>`.
- Arquivos `.meta` sempre versionados.

---

## 14. Definition of Done (por sistema)

Ver Quality Gates em [07-roadmap.md](07-roadmap.md#quality-gates).

---

## 15. Conflitos resolvidos

| # | Regra A | Regra B | Decisão |
|---|---|---|---|
| C1 | "Use interfaces e abstrações sempre que possível" (Unity6 Rules) | "Não criar abstrações desnecessárias" (Master) | Interfaces nos limites entre packages, em pontos de extensão e com ≥ 2 implementações ou costura de teste. |
| C2 | "Usar Design Patterns sempre que possível" (RamiresTech) | "Não criar patterns desnecessários" (Master) | Pattern só quando resolve problema concreto presente. Master prevalece. |
| C3 | "Nunca duplique lógica" (DRY) | Packages independentes + Core enxuto | Duplicação pequena entre packages é aceitável. Algo vai para o Core somente com ≥ 2 consumidores reais e ADR. |
| C4 | Pasta `RTG_Unity_Modules` para reutilizáveis (RamiresTech) | Um Unity Package por domínio em repo próprio (Master) | `RTG_Unity_Modules` é substituída por packages UPM. |
| C5 | Namespace `RamiresTechGames.[SISTEMA].[CAMINHO]` (RamiresTech) | "Namespaces consistentes padrão RamiresTech" (Master) | Adotado `RamiresTechGames.<Sistema>.<Caminho>`; assembly = namespace raiz. |
| C6 | Regions obrigatórias (RamiresTech) | Clean Code desencoraja regions | Obrigatórias em Unity components, SOs, Editor e classes > ~80 linhas; opcionais em tipos pequenos. |
| C7 | Region "Public Variables" (RamiresTech) | Campos públicos quebram encapsulamento | Campos públicos proibidos; region renomeada para "Events". |
| C8 | Constantes `UPPER_SNAKE_CASE` (Unity6 Rules) | Convenção .NET `PascalCase` para constantes | `UPPER_SNAKE_CASE`, conforme regra escrita. |
| C9 | "Sem abreviações" | Id de package `hfsm`, `ai` (Master) | Ids de package mantidos; namespaces sem abreviação (`HierarchicalStateMachine`); siglas permitidas listadas na Seção 3. |
| C10 | "Prefira eventos a polling" (Unity6 Rules) | "Execução com ordem previsível" (Master/Tyroller) | Simulação por ticks ordenados; eventos para notificação. |
| C11 | Arquitetura MVP (padrão adotado anteriormente) | Simulation/Presentation + Glue (Master) | Framework usa camadas Domain/Integration/Presentation/Editor; MVP não é exigido. |
| C12 | "ScriptableObject usado apenas para dados" | GAS/BT precisam de comportamento configurável | SO guarda dados; comportamento polimórfico via `[SerializeReference]` de classes puras sem estado; estado vive em instâncias de runtime. |
| C13 | Testes obrigatórios (Master) | "Não use TDD" (instrução do usuário) | Test-after: testes escritos após implementação, obrigatórios para fechar o milestone. |
| C14 | "Use object pooling" (Unity6 Rules) | Sem abstrações desnecessárias | `UnityEngine.Pool` onde há instanciação recorrente; sem framework próprio. |
| C15 | Usar InputSystem, Cinemachine, Feel (RamiresTech) | Packages não acoplam a Input/apresentação (Master) | Usados nos jogos; nos packages só via integration assemblies opcionais (Input System). |
| C16 | Formatação 100–120 colunas (Unity6 Rules) | — | Limite fixo de 120. |
