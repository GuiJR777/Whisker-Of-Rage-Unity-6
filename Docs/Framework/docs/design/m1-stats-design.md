# M1 — Stats System · Especificação técnica (design para revisão)

- **Package:** `com.ramirestechgames.stats` · assembly/namespace raiz `RamiresTechGames.Stats` · camada L1
- **Dependência hard:** `com.ramirestechgames.core` · **Opcionais:** nenhuma
- **Status:** **Design em revisão — não implementar antes da aprovação.**
- **Requisitos de origem:** Master Prompt §4.1; contratos em [02](../02-package-boundaries-and-contracts.md) (seção stats).

---

## 1. Objetivo e escopo

Sistema genérico de **atributos numéricos** e **recursos consumíveis**, configurado por ScriptableObjects, com
modificadores rastreáveis por fonte, stats derivadas por fórmula, regeneração/consumo, eventos e ferramentas de
inspeção em runtime. É a base numérica de Combat (dano), Abilities (custos e efeitos) e Equipment (bônus).

### Dentro do escopo (lista fechada, Master Prompt §4.1)
Stat Definitions · Stat Sets reutilizáveis · Base/Current/Max · modificadores Flat/Additive/Multiplicative ·
temporários e permanentes · fontes identificáveis · regeneração e consumo de recursos · stats derivadas por
fórmula · recálculo e gerenciamento de dependências · Runtime Stat Inspector/debugger · eventos de mudança ·
API para Combat, Abilities e Equipment.

### Fora do escopo
Dano e defesa (Combat) · durações/stacking de efeitos complexos (Abilities) · equipar (Equipment) · save/load
(o modelo já traz `StableId` para isso) · rede/replicação · nomes de stats de qualquer jogo · UI de jogo (HUD).

---

## 2. Conceitos

| Conceito | O que é | "Base / Current / Max" |
|---|---|---|
| **Stat** | Atributo numérico calculado: valor base + modificadores. Ex.: força, velocidade, vida máxima, chance de crítico. | **Base** = valor base (constante ou fórmula) · **Value** = valor final após modificadores |
| **Resource** | Quantidade consumível limitada entre um mínimo e um máximo dado por uma stat. Ex.: vida atual, energia. | **Current** = quantidade atual · **Max** = valor final da stat de máximo · **Min** = piso |
| **Derived stat** | Stat cuja **base** vem de fórmula sobre outras stats. Modificadores ainda se aplicam sobre o resultado. | Base = fórmula(valores finais das dependências) |
| **Modifier** | Alteração de uma stat com operação, valor, prioridade, **fonte** e duração opcional. | — |
| **Source** | Quem aplicou o modificador (instância de equipamento, efeito de ability, debug). Permite remover tudo de uma fonte. | — |
| **Stat Set** | Conjunto reutilizável de stats + valores base + fórmulas + recursos; suporta herança (arquétipos). | — |

Os nomes de exemplo usados neste documento e no sample (Vitality, Health, Energy, MoveSpeed) são **genéricos**
e vivem apenas em `Samples~`. O código do package não contém nenhum nome de stat.

---

## 3. Modelo de dados (definições — ScriptableObjects imutáveis em runtime)

### 3.1 `StatDefinition` (SO)
| Campo | Tipo | Descrição |
|---|---|---|
| `StableId` | string (GUID, somente leitura) | Gerado na criação; identidade estável para save/debug. Duplicatas são erro no Validator. |
| `DisplayName`, `Description`, `Category` | string | Nome no debugger/inspectors; categoria agrupa no inspector do set. |
| `DefaultBaseValue` | float | Base usada quando o set não define outra. |
| `HasMinValue` / `MinValue`, `HasMaxValue` / `MaxValue` | bool/float | Limites aplicados ao valor final (ex.: chance de crítico em [0, 1]). |
| `Rounding` | `StatRounding` { `None`, `Round`, `Floor`, `Ceil` } | Para stats inteiras. Aplicado antes do clamp. |

### 3.2 `ResourceDefinition` (SO)
| Campo | Tipo | Descrição |
|---|---|---|
| `StableId`, `DisplayName`, `Description` | — | Como em `StatDefinition`. |
| `MaxStat` | `StatDefinition` (obrigatório) | Stat que define o máximo. Deve existir no set. |
| `MinValue` | float (padrão 0) | Piso. |
| `InitialFill` | `ResourceInitialFill` { `Full`, `Empty`, `Fraction` } + `InitialFraction` [0..1] | Valor inicial. |
| `RegenerationSource` | { `None`, `Constant`, `Stat` } | Origem da taxa (unidades/s). `Stat` permite regen modificável por buffs. |
| `RegenerationPerSecond` / `RegenerationStat` | float / `StatDefinition` | Taxa; **negativa = decaimento** (ex.: barra que esvazia sozinha). |
| `RegenerationDelayAfterDecrease` | float (s) | Pausa a regen após qualquer redução (ex.: stamina). |
| `RegenerateWhileDepleted` | bool | Se recupera estando no mínimo (vida: não; stamina: sim). |
| `MaxChangePolicy` | `ResourceMaxChangePolicy` { `Clamp`, `PreserveRatio`, `AddDifference` } | O que acontece com `Current` quando `Max` muda (ver §5.4). |

### 3.3 `StatSetDefinition` (SO)
| Campo | Tipo | Descrição |
|---|---|---|
| `ParentSet` | `StatSetDefinition` (opcional) | Herança de arquétipo: o filho herda entradas e sobrescreve as que declarar. |
| `Stats` | `List<StatEntry>` | `Stat`, `BaseSource` { `Default`, `Constant`, `Formula` }, `BaseValue`, `[SerializeReference] StatFormula Formula`. |
| `Resources` | `List<ResourceEntry>` | `Resource` + override opcional de `InitialFill`. |

O conjunto efetivo é calculado **uma vez** (pai → filho, entradas do filho vencem) e cacheado por set.

### 3.4 Fórmulas (`[SerializeReference]`, classes puras sem estado)
```csharp
[Serializable]
public abstract class StatFormula
{
    /// Adds every stat this formula reads (used for dependency ordering and cycle detection).
    public abstract void CollectDependencies(List<StatDefinition> dependencies);

    /// Evaluates using FINAL values of the dependencies.
    public abstract float Evaluate(IStatValueSource source);
}
```
| Fórmula incluída | Expressão | Exemplo |
|---|---|---|
| `LinearFormula` | `Constant + Σ(Coefficient_i × stat_i)` | MaxHealth = 50 + 10 × Vitality |
| `CurveFormula` | `Scale × Curve.Evaluate(stat)` | Retornos decrescentes de Agility → crítico |

O jogo pode criar fórmulas próprias herdando `StatFormula` (aparecem no seletor de tipo do inspector).
**Sem parser de expressões em texto** (ver §11, alternativas).

---

## 4. Modificadores

### 4.1 Estrutura
```csharp
public enum StatModifierOperation { Flat, Additive, Multiplicative }

public readonly struct StatModifier
{
    public StatModifier(StatModifierOperation operation, float value, IModifierSource source,
                        float duration = 0f, int priority = 0);
    public StatModifierOperation Operation { get; }
    public float Value { get; }          // Flat: unidades · Additive: fração somada (0.1 = +10%) · Multiplicative: fator (1.5 = ×1.5)
    public IModifierSource Source { get; } // obrigatório
    public float Duration { get; }       // 0 = permanente (até ser removido)
    public int Priority { get; }         // apenas ordenação de exibição/estabilidade; não altera a matemática
}

public interface IModifierSource { string DisplayName { get; } }   // implementado por equipamentos, efeitos, etc.
public sealed class ModifierSource : IModifierSource { ... }       // fonte simples nomeada (debug, scripts, testes)
public readonly struct ModifierHandle { ... }                       // retorno de AddModifier; remoção individual
```

### 4.2 Fórmula do valor final
```
base     = Constant | DefaultBaseValue | Formula(final values of dependencies)
raw      = (base + Σ Flat) × (1 + Σ Additive) × Π Multiplicative
final    = Clamp(Round(raw), Min, Max)          // Round/Clamp conforme a StatDefinition
```

| Base | Modificadores | Final | Leitura |
|---|---|---|---|
| 100 | — | 100 | — |
| 100 | Flat +20 | 120 | |
| 100 | Flat +20, Additive +0.10, Additive +0.15 | 150 | (120) × 1.25 |
| 100 | Flat +20, Additive +0.25, Multiplicative ×2 | 300 | 150 × 2 |
| 100 | Multiplicative ×1.5, Multiplicative ×0.5 | 75 | multiplicativos se compõem |
| 5 (MoveSpeed) | Multiplicative ×0 | 0 | "enraizado" |
| 0.9 (Crit, Max 1) | Additive +0.5 | 1 | clamp |

### 4.3 Temporários × permanentes × base
| Tipo | Como | Quem usa |
|---|---|---|
| **Temporário** | `Duration > 0`; `StatCollection.Tick` expira e remove. | Buffs simples sem Abilities, testes, debug. |
| **Permanente** | `Duration = 0`; vive até `RemoveModifier`/`RemoveModifiersFromSource`. | Equipment (enquanto equipado); Abilities (o efeito controla a própria duração e remove por fonte). |
| **Mudança de base** | `TrySetBaseValue` / `TryAddToBaseValue`. | Progressão (level up, upgrades permanentes). Proibido em stats derivadas. |

Regra para Abilities: **uma única fonte da verdade de tempo** — efeitos com duração/stacking adicionam modificadores
permanentes e os removem pela fonte quando acabam; não usam `Duration` do Stats.

---

## 5. Runtime

### 5.1 `StatCollection` (Pure) — a API central
```csharp
public sealed class StatCollection : IStatValueSource
{
    public StatCollection(StatSetDefinition set, IReadOnlyList<BaseValueOverride> overrides = null);

    // Leitura
    public bool Has(StatDefinition stat);
    public float GetValue(StatDefinition stat);                 // final; erro logado 1x e 0 se ausente
    public bool TryGetValue(StatDefinition stat, out float value);
    public float GetBaseValue(StatDefinition stat);

    // Base
    public bool TrySetBaseValue(StatDefinition stat, float value);     // false em stat derivada/ausente
    public bool TryAddToBaseValue(StatDefinition stat, float delta);

    // Modificadores
    public ModifierHandle AddModifier(StatDefinition stat, in StatModifier modifier);
    public bool RemoveModifier(ModifierHandle handle);
    public int RemoveModifiersFromSource(IModifierSource source);       // todas as stats
    public void GetModifiers(StatDefinition stat, List<ActiveModifierInfo> results); // sem alocar (lista do chamador)

    // Lote: recalcula e emite eventos uma vez ao final (ex.: equipar item com 5 bônus)
    public StatBatchScope BeginBatch();                                 // using (stats.BeginBatch()) { ... }

    // Recursos
    public bool TryGetResource(ResourceDefinition resource, out ResourcePool pool);
    public ResourcePool GetResource(ResourceDefinition resource);

    // Tempo (chamado pelo StatsComponent; domínio não lê Time.*)
    public void Tick(float deltaTime);                                   // expira temporários + regen

    public event Action<StatChangedArgs> StatChanged;                   // readonly struct: Stat, OldValue, NewValue
}
```

### 5.2 `ResourcePool` (Pure)
```csharp
public sealed class ResourcePool
{
    public ResourceDefinition Definition { get; }
    public float Current { get; }  public float Max { get; }  public float Min { get; }
    public float Normalized { get; }  public bool IsDepleted { get; }   // Current <= Min

    public bool CanAfford(float amount);                                  // Current - amount >= Min
    public bool TryConsume(float amount, object instigator = null);       // custo: tudo ou nada
    public float Decrease(float amount, object instigator = null);        // dano: aplica o possível, retorna aplicado
    public float Increase(float amount, object instigator = null);        // cura/restauração, retorna aplicado
    public void SetCurrent(float value, object instigator = null);
    public void Fill();  public void Deplete();

    public event Action<ResourceChangedArgs> Changed;     // Resource, Old, New, Delta, Reason, Instigator
    public event Action<ResourcePool> Depleted;           // uma vez por transição para o mínimo
    public event Action<ResourcePool> Replenished;        // uma vez por saída do mínimo
}
public enum ResourceChangeReason { Consume, Decrease, Increase, Regeneration, MaxChanged, Set }
```
Custos múltiplos (Abilities): checar `CanAfford` de todos e só então `TryConsume` de cada um (documentado como
padrão; não há transação implícita entre pools).

### 5.3 Recálculo e dependências
1. Na construção, o set efetivo vira arrays indexados (`StatDefinition` → índice via dicionário construído **uma vez**).
2. As dependências das fórmulas formam um grafo; calcula-se a **ordem topológica** e a lista de dependentes de cada
   stat. **Ciclos** são erro do Validator (antes do Play) e, se chegarem ao runtime, a fórmula é ignorada
   (base = `DefaultBaseValue`) com um único `LogError` com contexto.
3. **Recálculo ansioso e completo:** ao mudar base/modificador de S, recalcula S e seus dependentes na ordem
   topológica, **sempre a partir de base + lista de modificadores** (nunca por deltas incrementais). Isso garante
   restauração exata ao remover modificadores (sem drift de float).
4. **Eventos depois do lote:** `StatChanged` só dispara depois que todo o subgrafo foi recalculado (listeners veem
   estado consistente), uma vez por stat cujo valor mudou. `BeginBatch` estende o lote para várias operações.
5. Mudança de uma stat que é `MaxStat` de um recurso aplica a `MaxChangePolicy` (§5.4) e emite `Changed` com
   `Reason = MaxChanged`.

### 5.4 Política de mudança do máximo
| Política | Max 100 → 150 (Current 80) | Max 100 → 50 (Current 80) | Uso típico |
|---|---|---|---|
| `Clamp` | 80 | 50 | Energia, munição |
| `PreserveRatio` | 120 | 40 | Escalas proporcionais |
| `AddDifference` | 130 | 50 (só clamp ao reduzir) | Vida ao equipar bônus de vida máxima |

### 5.5 Regeneração (em `Tick`)
Aplica `rate × deltaTime` quando: taxa ≠ 0, atraso pós-redução vencido, e (`!IsDepleted` ou
`RegenerateWhileDepleted`). Limita a [Min, Max]. Taxa negativa = decaimento. Emite `Changed` com
`Reason = Regeneration` apenas quando o valor muda.

### 5.6 Componentes Unity
| Componente | Banda | Responsabilidade |
|---|---|---|
| `StatsComponent` | `ExecutionOrder.STATS` (`Update`) | `[SerializeField] StatSetDefinition`, overrides de base por instância (variações na cena sem novo asset). Cria a `StatCollection` em `Awake`; tica com `Time.deltaTime`. Expõe `Stats`. |
| `ResourceEventsComponent` | `PRESENTATION` | Glue sem código: escolhe um recurso e expõe `UnityEvent`s (`OnDepleted`, `OnReplenished`, `OnChanged(float normalized)`) para designers ligarem animação/SFX/UI. |

`Update` com tempo escalado: regen e durações respeitam `timeScale`. Hitstop/time scale local é decisão do M4
(ADR-0003); o Stats só recebe o delta que lhe passarem.

---

## 6. Integração com outros packages
Stats é L1 e não conhece ninguém acima. Quem integra depende dele (hard) ou tem integration assembly.

| Package | Como usa o Stats | Onde fica o código |
|---|---|---|
| **Combat** (M4, hard) | Etapas de dano (`IDamageStep`) leem `IStatValueSource` do atacante/defensor; aplicam `ResourcePool.Decrease(amount, instigator)` no recurso de vida escolhido no `DamageProfile`; reagem a `Depleted`. | Combat |
| **Abilities** (M5, hard) | Custos: `CanAfford`/`TryConsume`. Gameplay Effects: modificadores permanentes com a instância do efeito como `IModifierSource`, removidos por fonte; efeitos periódicos usam `Increase`/`Decrease`. Duração e stacking ficam no Abilities. | Abilities |
| **Equipment** (M7, hard) | Equipar: `BeginBatch` + `AddModifier` com a instância equipada como fonte. Desequipar: `RemoveModifiersFromSource`. | Equipment |
| **Character** (M3, opcional) | `StatDrivenMovement`: lê uma stat escolhida no inspector (ex.: velocidade) e reage a `StatChanged`. | `Character.Integration.Stats` |
| **AI** (M6) | Condições como "recurso < 30%". **Requer mudança no grafo** (ver §11, D6). | `AI.Integration.Stats` (se aprovado) |
| **Core** | Definições e `StatsComponent` implementam `IValidatable`; componentes usam `ExecutionOrder`. | Stats |

---

## 7. Ferramentas de Unity Editor

| Ferramenta | Tipo | Conteúdo |
|---|---|---|
| Menus de criação | `Assets/Create/RamiresTech Games/Stats/` | Stat Definition, Resource Definition, Stat Set Definition. |
| Inspector de `StatSetDefinition` | Custom Inspector (UI Toolkit) | Tabela por categoria: stat, origem da base (herdada/sobrescrita/fórmula), valor base, **coluna de prévia** (fórmulas avaliadas com valores base), avisos inline; lista de recursos com máximo previsto; botão "Add missing Max stats"; resumo do Validator no topo. |
| Seletor de fórmula | Property Drawer para `[SerializeReference] StatFormula` | Dropdown com todos os tipos concretos (`TypeCache`), inclusive fórmulas do jogo. |
| Inspector de `StatsComponent` | Custom Inspector | Em Edit Mode: set e overrides com prévia. Em Play Mode: valores ao vivo e barras de recursos. |
| **Runtime Stat Inspector** | `Tools/RamiresTech Games/Stats/Runtime Inspector` | Ator selecionado (ou lista de atores com `StatsComponent`): por stat, base → modificadores (fonte, operação, valor, tempo restante) → final; recursos com current/max/regen/atraso; log das últimas N mudanças; ações de debug (adicionar modificador de teste, encher/esvaziar recurso, remover por fonte). |
| Validators (`IValidatable`) | Core Validator | Ver §8. |

Não haverá editor de grafo de dependências: a ordem e os ciclos aparecem como lista/aviso no inspector do set.

---

## 8. Validações (Validator do Core)
| Objeto | Erro | Aviso |
|---|---|---|
| `StatDefinition` | `StableId` vazio/duplicado; `Min > Max`; base padrão fora dos limites | Sem `DisplayName` |
| `ResourceDefinition` | `MaxStat` ausente; regen por stat sem stat; fração inicial fora de [0, 1] | `MinValue` negativo |
| `StatSetDefinition` | Stat duplicada; ciclo de herança; ciclo entre fórmulas; fórmula lendo stat fora do set; `MaxStat` de recurso fora do set; fórmula nula com origem `Formula` | Base fora dos limites da stat |
| `StatsComponent` | Sem set | Override de stat que não existe no set |

---

## 9. Estrutura do package (proposta)
```
Runtime/
  Definitions/   StatDefinition, ResourceDefinition, StatSetDefinition, StatEntry, ResourceEntry, enums
  Formulas/      StatFormula, LinearFormula, CurveFormula, IStatValueSource
  Modifiers/     StatModifier, StatModifierOperation, ModifierHandle, IModifierSource, ModifierSource, ActiveModifierInfo
  Values/        StatCollection, ResourcePool, StatBatchScope, StatChangedArgs, ResourceChangedArgs (+ internos)
  Components/    StatsComponent, ResourceEventsComponent
Editor/
  Inspectors/    StatSetDefinitionEditor, StatsComponentEditor
  Drawers/       StatFormulaDrawer (ou picker do Core, ver D5)
  Windows/       RuntimeStatInspectorWindow
Tests/
  Editor/        domínio (matemática, dependências, recursos, lotes, alocação, validators)
  Runtime/       StatsComponent em Play Mode; Fixtures/
Samples~/
  StatsPlayground/  stats genéricas (Vitality, MaxHealth, Health, Energy, MoveSpeed), cena com ator e ações por ContextMenu
```

---

## 10. Critérios de aceite do M1
Além de QG1–QG12:

| # | Critério | Verificação |
|---|---|---|
| AC1 | Designer cria stats, recurso e set (com herança e fórmula) **só pelo Inspector** e vê a prévia dos valores. | Sample + revisão |
| AC2 | A matemática da §4.2 reproduz todos os exemplos da tabela. | Testes paramétricos |
| AC3 | `RemoveModifiersFromSource` restaura **exatamente** (igualdade bit a bit) todas as stats e o `Max` dos recursos afetados. | Testes |
| AC4 | Derivadas recalculam em ordem topológica; `StatChanged` dispara uma vez por stat alterada por lote; ciclos detectados pelo Validator e neutralizados em runtime com um único erro. | Testes |
| AC5 | Recursos: `TryConsume` tudo-ou-nada; `Decrease`/`Increase` com clamp; regen com atraso e decaimento; `Depleted`/`Replenished` exatamente uma vez por transição; as 3 políticas de máximo. | Testes |
| AC6 | Runtime Stat Inspector mostra base → modificadores (fonte, operação, tempo restante) → final ao vivo e executa ações de debug. | Revisão em Play Mode |
| AC7 | Zero alocação de GC em regime: `GetValue`, `Tick` com 50 coleções, `AddModifier`/`RemoveModifier` após aquecimento. | `Is.Not.AllocatingGCMemory()` |
| AC8 | Nenhum nome de stat no código do package. | Revisão + busca |
| AC9 | ScriptableObjects inalterados após uma sessão de Play com modificadores e consumo. | Teste |
| AC10 | Sample "Stats Playground" funciona sem escrever código. | QG4/QG11 |
| AC11 | `CONTRACTS.md` lista a API com estabilidade e as garantias AC2–AC5. | QG6 |

---

## 11. Decisões para a revisão
| # | Decisão | Proposta | Alternativas descartadas |
|---|---|---|---|
| D1 | Fórmula de agregação | `(base + ΣFlat) × (1 + ΣAdditive) × ΠMultiplicative`, depois round e clamp | Todos percentuais somados (perde composição); ordem configurável por modificador (complexo de depurar) |
| D2 | Representação do multiplicativo | Fator (`1.5` = ×1.5); Additive em fração (`0.1` = +10%); inspector mostra `×` e `%` | Tudo em porcentagem |
| D3 | Onde ficam as fórmulas | Na entrada do `StatSetDefinition`, com herança de sets para reaproveitar | Na `StatDefinition` (global, impede arquétipos diferentes) |
| D4 | Operação `Override` | **Fora** do M1 (lista fechada do Master Prompt); `Multiplicative ×0` cobre "zerar" | Incluir já |
| D5 | Seletor de tipos `[SerializeReference]` | **Mover para o Core.Editor** (atributo + drawer genéricos), pois Stats (M1) e HFSM (M2) já precisam, e Combat/Abilities/AI virão; exige ADR | Drawer específico em cada package (duplicação) |
| D6 | AI precisa ler stats | **Adicionar `stats` às dependências opcionais de `ai`** no grafo (ADR); camadas continuam válidas (L5 → L1) | Ler stats só via integração Abilities (indireto, frágil) |
| D7 | Tempo do Stats | `Update`, banda `STATS`, tempo escalado; hitstop decidido no M4 | Tickar em `FixedUpdate` (regen não depende de física) |
| D8 | Durações | Stats só para buffs simples; Abilities é dono de durações/stacking dos seus efeitos | Stats controlar todas as durações (acopla regras de stacking ao Stats) |
| D9 | Parser de expressões ("10*VIT+50") | Não no M1; fórmulas como classes serializadas | Parser em runtime (custo, erros só em Play, difícil de validar) |
| D10 | Política padrão de máximo | `Clamp` como padrão da `ResourceDefinition`; designers escolhem por recurso | `AddDifference` como padrão |
| D11 | Valores inteiros | Tudo `float` internamente; inteiros via `Rounding` da definição | Tipos separados int/float |

## 12. Plano de implementação (após aprovação)
1. `new_package.py stats` → repo privado + submodule + `testables`.
2. Definições + validators → `StatCollection` (modificadores, recálculo, lotes) → `ResourcePool` → componentes.
3. Inspectors, seletor de fórmula (conforme D5), Runtime Stat Inspector.
4. Sample "Stats Playground".
5. Testes (EditMode + PlayMode + alocação), docs do package, QG1–QG12, tag `v0.1.0`.
