# M1 — Stats System · Especificação técnica

- **Package:** `com.ramirestechgames.stats` · assembly/namespace raiz `RamiresTechGames.Stats` · camada L1
- **Dependência hard:** `com.ramirestechgames.core` (≥ 0.2.0, por causa do seletor de `[SerializeReference]`) · **Opcionais:** nenhuma
- **Status:** **v2 — aprovada com correções; implementação autorizada.** M1 só é declarado concluído após revisão final.
- **Requisitos de origem:** Master Prompt §4.1; contratos em [02](../02-package-boundaries-and-contracts.md) (seção stats).
- **ADRs relacionadas:** [ADR-0008](../adr/0008-serialize-reference-picker-in-core.md) (seletor no Core.Editor),
  [ADR-0009](../adr/0009-ai-optional-stats.md) (AI → Stats opcional).

### Histórico
| Versão | Mudança |
|---|---|
| v1 | Design inicial (decisões D1–D11 em aberto). |
| v2 | D1–D11 aprovadas (D10 = `Clamp`). Correções da revisão: consumo atômico de múltiplos recursos (§5.3), integridade numérica (§6), cache e isolamento (§7), eventos/identidade/lotes (§8), regeneração × decaimento (§5.5), contrato de falhas (§9), AC3 reescrito e testes adicionais (§13). |

---

## 1. Objetivo e escopo

Sistema genérico de **atributos numéricos** e **recursos consumíveis**, configurado por ScriptableObjects, com
modificadores rastreáveis por fonte, stats derivadas por fórmula, regeneração/consumo, eventos e ferramentas de
inspeção em runtime. É a base numérica de Combat (dano), Abilities (custos e efeitos) e Equipment (bônus).

**Dentro do escopo (lista fechada, Master Prompt §4.1):** Stat Definitions · Stat Sets reutilizáveis ·
Base/Current/Max · modificadores Flat/Additive/Multiplicative · temporários e permanentes · fontes identificáveis ·
regeneração e consumo de recursos · stats derivadas por fórmula · recálculo e dependências · Runtime Stat
Inspector/debugger · eventos de mudança · API para Combat, Abilities e Equipment.

**Fora do escopo:** dano/defesa (Combat) · durações e stacking de efeitos (Abilities) · equipar (Equipment) ·
operação `Override` (D4) · parser de fórmulas em texto (D9) · save/load (o modelo já traz `StableId`) · rede ·
nomes de stats de qualquer jogo · HUD.

---

## 2. Conceitos

| Conceito | O que é | Base / Current / Max |
|---|---|---|
| **Stat** | Atributo numérico: valor base + modificadores. | **Base** = constante ou fórmula · **Value** = final |
| **Resource** | Quantidade consumível entre um mínimo e um máximo dado por uma stat. | **Current** · **Max** = stat de máximo · **Min** = piso |
| **Derived stat** | Stat cuja base vem de fórmula sobre valores **finais** de outras stats. | Base = fórmula(dependências) |
| **Modifier** | Operação + valor + fonte + duração opcional + prioridade. | — |
| **Source** | Quem aplicou o modificador; permite remover tudo dela. Identidade **por referência**. | — |
| **Stat Set** | Conjunto reutilizável com herança (arquétipos). | — |

Nomes como Vitality, Health, Energy existem **apenas** no sample.

---

## 3. Modelo de dados (ScriptableObjects imutáveis em runtime)

### 3.1 `StatDefinition`
| Campo | Tipo | Descrição |
|---|---|---|
| `StableId` | string (somente leitura) | Espelha o GUID do asset (atualizado no Editor; corrige duplicação de asset). |
| `DisplayName`, `Description`, `Category` | string | Exibição e agrupamento. |
| `DefaultBaseValue` | float | Base quando o set não define outra. |
| `HasMinValue`/`MinValue`, `HasMaxValue`/`MaxValue` | bool/float | Limites do valor final. |
| `Rounding` | `None`, `Round`, `Floor`, `Ceil` | Aplicado antes do clamp (D11). |

### 3.2 `ResourceDefinition`
| Campo | Tipo | Descrição |
|---|---|---|
| `StableId`, `DisplayName`, `Description` | — | Como acima. |
| `MaxStat` | `StatDefinition` (obrigatório) | Stat do máximo; deve existir no set. |
| `MinValue` | float (padrão 0) | Piso. |
| `InitialFill` + `InitialFraction` | `Full`/`Empty`/`Fraction`, [0, 1] | Valor inicial. |
| `RegenerationSource` | `None`/`Constant`/`Stat` | Origem da taxa (unidades/s). |
| `RegenerationPerSecond` / `RegenerationStat` | float / `StatDefinition` | Positiva = recuperação; **negativa = decaimento**. |
| `RegenerationDelayAfterDecrease` | float (s) | Pausa **apenas a recuperação** após redução **externa** (§5.5). |
| `RegenerateWhileDepleted` | bool | Recupera estando no mínimo? |
| `MaxChangePolicy` | `Clamp` (**padrão**, D10), `PreserveRatio`, `AddDifference` | Efeito de mudança do máximo em `Current`. |

### 3.3 `StatSetDefinition`
| Campo | Tipo | Descrição |
|---|---|---|
| `ParentSet` | `StatSetDefinition` | Herança: filho herda e sobrescreve. |
| `Stats` | `List<StatEntry>` | `Stat`, `BaseSource` (`Default`/`Constant`/`Formula`), `BaseValue`, `[SerializeReference, SelectImplementation] StatFormula Formula` (D3). |
| `Resources` | `List<ResourceEntry>` | `Resource` + override opcional de `InitialFill`. |

### 3.4 Fórmulas
```csharp
[Serializable]
public abstract class StatFormula
{
    public abstract void CollectDependencies(List<StatDefinition> dependencies);
    public abstract float Evaluate(IStatValueSource source);           // valores FINAIS das dependências
    public virtual void Validate(ValidationReport report, UnityEngine.Object context) { }
}
```
Incluídas: `LinearFormula` (`Constant + Σ Coefficient_i × stat_i`) e `CurveFormula` (`Scale × Curve(stat)`).
O jogo pode criar as suas (aparecem no seletor do Core). Sem parser textual (D9).

---

## 4. Modificadores

```csharp
public enum StatModifierOperation { Flat, Additive, Multiplicative }

public readonly struct StatModifier
{
    public StatModifier(StatModifierOperation operation, float value, IModifierSource source,
                        float duration = 0f, int priority = 0);
    // Flat: unidades · Additive: fração (0.1 = +10%) · Multiplicative: fator (1.5 = ×1.5)   (D2)
}
public interface IModifierSource { string DisplayName { get; } }
public sealed class ModifierSource : IModifierSource { public ModifierSource(string displayName); }
public readonly struct ModifierHandle { public static readonly ModifierHandle Invalid; public bool IsValid { get; } }
```

**Valor final (D1):**
```
base     = Constant | DefaultBaseValue | Formula(...)
additive = Max(0, 1 + Σ Additive)                     // total abaixo de -100% satura em zero
raw      = (base + Σ Flat) × additive × Π Multiplicative
final    = Clamp(Round(raw), Min, Max)
```
| Base | Modificadores | Final |
|---|---|---|
| 100 | — | 100 |
| 100 | Flat +20 | 120 |
| 100 | Flat +20, Additive +0.10, Additive +0.15 | 150 |
| 100 | Flat +20, Additive +0.25, Multiplicative ×2 | 300 |
| 100 | Multiplicative ×1.5, ×0.5 | 75 |
| 5 | Multiplicative ×0 | 0 |
| 0.9 (Max 1) | Additive +0.5 | 1 |
| 100 | Additive −1.5 | 0 (saturação) |

**Ordem canônica:** a agregação percorre os modificadores de cada stat ordenados por
`(Operation, Priority, Value)`. O resultado depende só do **conjunto** de modificadores ativos, não da ordem em
que foram adicionados ou removidos (§13, AC3).

**Temporários × permanentes × base:** `Duration > 0` expira em `Tick` (buffs simples); `Duration = 0` vive até
ser removido (Equipment; efeitos do Abilities, que controla duração/stacking e remove por fonte — D8);
progressão permanente usa `TrySetBaseValue`/`TryAddToBaseValue` (proibido em derivadas).

---

## 5. Runtime

### 5.1 `StatCollection` (Pure, uma por ator)
```csharp
public sealed class StatCollection : IStatValueSource
{
    public StatCollection(StatSetDefinition set, IReadOnlyList<BaseValueOverride> overrides = null);

    // Leitura — obrigatória lança; opcional usa Try (§9)
    public bool Has(StatDefinition stat);
    public float GetValue(StatDefinition stat);                      // StatNotFoundException se ausente
    public bool TryGetValue(StatDefinition stat, out float value);
    public float GetBaseValue(StatDefinition stat);

    // Base
    public bool TrySetBaseValue(StatDefinition stat, float value);   // false: ausente, derivada ou não finito
    public bool TryAddToBaseValue(StatDefinition stat, float delta);

    // Modificadores
    public ModifierHandle AddModifier(StatDefinition stat, in StatModifier modifier);           // lança (§9)
    public bool TryAddModifier(StatDefinition stat, in StatModifier modifier, out ModifierHandle handle);
    public bool RemoveModifier(ModifierHandle handle);                // false: inválido, antigo ou de outra coleção
    public int RemoveModifiersFromSource(IModifierSource source);
    public bool IsActive(ModifierHandle handle);
    public void GetModifiers(StatDefinition stat, List<ActiveModifierInfo> results);
    public void GetAllModifiers(List<ActiveModifierInfo> results);

    // Recursos
    public ResourcePool GetResource(ResourceDefinition resource);    // ResourceNotFoundException se ausente
    public bool TryGetResource(ResourceDefinition resource, out ResourcePool pool);
    public bool CanAfford(IReadOnlyList<ResourceCost> costs);
    public ResourceTransactionResult TryConsumeResources(IReadOnlyList<ResourceCost> costs, object instigator = null);

    // Lote e tempo
    public StatBatchScope BeginBatch();
    public void Tick(float deltaTime);

    public event Action<StatChangedArgs> StatChanged;               // Stat, OldValue, NewValue
    public event Action<ResourceChangedArgs> ResourceChanged;       // espelho de todos os pools (debugger/log)
    public IReadOnlyList<string> Diagnostics { get; }               // problemas de configuração detectados
}
```

### 5.2 `ResourcePool` (Pure, pertence a uma `StatCollection`)
```csharp
public sealed class ResourcePool
{
    public ResourceDefinition Definition { get; }
    public float Current { get; } public float Max { get; } public float Min { get; }
    public float Normalized { get; } public bool IsDepleted { get; }     // Current <= Min

    public bool CanAfford(float amount);
    public bool TryConsume(float amount, object instigator = null);      // custo: tudo ou nada
    public float Decrease(float amount, object instigator = null);       // dano: retorna aplicado
    public float Increase(float amount, object instigator = null);       // cura: retorna aplicado
    public void SetCurrent(float value, object instigator = null);
    public void Fill(object instigator = null); public void Deplete(object instigator = null);

    public event Action<ResourceChangedArgs> Changed;   // Resource, Old, New, Delta, Reason, Instigator
    public event Action<ResourcePool> Depleted;          // uma vez por transição para o mínimo
    public event Action<ResourcePool> Replenished;       // uma vez por saída do mínimo
}
public enum ResourceChangeReason { Consume, Decrease, Increase, Regeneration, Decay, MaxChanged, Set }
```

### 5.3 Consumo atômico de múltiplos recursos (correção 1)
API mínima para o futuro GAS pagar custos compostos (ex.: 20 de energia + 5 de vida) — **não** é um sistema de
transações genérico.
```csharp
public readonly struct ResourceCost { public ResourceCost(ResourceDefinition resource, float amount); }
public enum ResourceTransactionStatus { Success, InvalidCost, MissingResource, InsufficientResource }
public readonly struct ResourceTransactionResult
{
    public ResourceTransactionStatus Status { get; }
    public ResourceDefinition FailedResource { get; }   // null em Success
    public bool Succeeded => Status == ResourceTransactionStatus.Success;
}
```
Algoritmo de `TryConsumeResources(costs)`:
1. **Validar** todas as entradas: recurso não nulo e presente; quantia finita e ≥ 0. Falha → retorna o status, nada muda.
2. **Consolidar** custos do mesmo recurso (somados em buffer interno pré-alocado, sem GC).
3. **Verificar** `CanAfford` de cada recurso com o total consolidado. Falha → `InsufficientResource`, nada muda.
4. **Aplicar** todas as reduções (reason `Consume`; reinicia o atraso de recuperação de cada recurso afetado).
5. **Notificar** só depois do passo 4: um `Changed` por recurso (na ordem do set), seguido de `Depleted` quando houver.
`CanAfford(costs)` executa os passos 1–3 sem aplicar. Custos de valor 0 são válidos e não geram eventos.

### 5.4 Recálculo e dependências
1. O set efetivo é compilado em um **layout imutável** (§7): stats em ordem topológica, índices, dependentes,
   recursos e mapeamento stat de máximo → recursos.
2. Ciclos de fórmula: erro no Validator; em runtime a fórmula do ciclo é desativada (base = `DefaultBaseValue`) e
   registrada em `Diagnostics` + um `LogError`.
3. **Recálculo ansioso e completo:** mudou base/modificador de S → recalcula S e dependentes em ordem topológica,
   sempre a partir de base + conjunto de modificadores (nunca por deltas).
4. Política de máximo dos recursos aplicada **uma vez ao fim do lote** (§8.3), do máximo inicial ao final do lote.

### 5.5 Regeneração × decaimento (correção 5)
| | Recuperação (taxa > 0) | Decaimento (taxa < 0) |
|---|---|---|
| Atraso `RegenerationDelayAfterDecrease` | Aplica-se | **Não** se aplica |
| O que reinicia o atraso | Somente reduções **externas**: `TryConsume`, `TryConsumeResources`, `Decrease`, `SetCurrent` para baixo, `Deplete` | — |
| O que **não** reinicia | `Decay`, `MaxChanged` (clamp), `Regeneration` | Decaimento nunca reinicia o atraso |
| Em `IsDepleted` | Só se `RegenerateWhileDepleted` | Para no mínimo (`Depleted` ao atingir) |
| Reason do evento | `Regeneration` | `Decay` |
O atraso é consumido com precisão de sub-tick: se vencer no meio de um `Tick`, só o restante do delta regenera.
Taxa vinda de stat não finita é tratada como 0 (§6).

### 5.6 Componentes Unity
| Componente | Banda | Responsabilidade |
|---|---|---|
| `StatsComponent` | `STATS` (`Update`) | Set + overrides de base por instância; cria a `StatCollection` (no `Awake` ou no primeiro acesso); `Tick(Time.deltaTime)` (D7); `Rebuild()` para aplicar edições de asset em Play Mode. |
| `ResourceEventsComponent` | `PRESENTATION` | `UnityEvent`s `OnDepleted`, `OnReplenished`, `OnNormalizedChanged(float)` para designers ligarem feedback sem código. |

---

## 6. Integridade numérica (correção 2)
| Situação | Comportamento |
|---|---|
| NaN/±Infinity em valor de modificador, duração, base, quantia de recurso, custo | Rejeitado: métodos obrigatórios lançam `ArgumentException`; `Try*` retornam `false`/status `InvalidCost`. Estado inalterado. |
| Quantia negativa em `TryConsume`/`Decrease`/`Increase`/custo | Rejeitada como acima (use a operação inversa). |
| Fator `Multiplicative` < 0 | Rejeitado. `0` é válido (zera). |
| Σ `Additive` < −1 | Fator aditivo satura em 0. |
| `Duration` < 0 | Rejeitado. |
| `StatDefinition` com `Min > Max` | Erro no Validator; em runtime `Min` prevalece (resultado = `Min`). |
| Fórmula retorna NaN/Infinity (ex.: divisão por zero em fórmula do jogo) ou lança exceção | Base = `DefaultBaseValue` da stat; um `LogError`/`LogException` por stat e por coleção; registrado em `Diagnostics`. Próximas avaliações seguem tentando (o erro não é logado de novo). |
| Valor final não finito (overflow) | Final = valor limitado de `DefaultBaseValue`; diagnóstico como acima. |
| `MaxStat` final abaixo do `MinValue` do recurso | Máximo efetivo = `MinValue` (intervalo colapsa; recurso fica esgotado); diagnóstico; aviso do Validator quando os limites da stat permitem isso. |
| Taxa de regeneração não finita | Tratada como 0; diagnóstico. |
| `Tick` com delta negativo ou não finito | Ignorado; diagnóstico. |
| `InitialFraction` fora de [0, 1] | Erro no Validator; em runtime é limitado a [0, 1]. |

---

## 7. Cache e isolamento (correção 3)
- **Layout compilado (`StatSetLayout`, interno, imutável):** criado a partir do set efetivo e **compartilhado** por
  todas as coleções do mesmo set. Só contém dados somente leitura (arrays nunca expostos).
- **Invalidação no Editor:** um contador global de versão (`StatsDefinitionVersion`) é incrementado no `OnValidate`
  de qualquer `StatDefinition`, `ResourceDefinition` ou `StatSetDefinition` (edição no Inspector, Undo/Redo,
  reimport) e por um `AssetPostprocessor` quando assets desses tipos são importados, movidos ou apagados. O set
  guarda a versão com que compilou; versão diferente → recompila no próximo uso. Em builds a versão nunca muda.
  O cache é `[NonSerialized]`, então recarga de domínio também o descarta.
- **Coleções existentes não são alteradas** por edições de asset em Play Mode (elas seguram o layout antigo).
  Para aplicar: `StatsComponent.Rebuild()` (botão no inspector/Runtime Inspector) ou sair e entrar no Play Mode.
- **Isolamento:** todo estado mutável (bases, valores, modificadores, recursos, filas de eventos, buffers) pertence a
  uma única `StatCollection`. Duas coleções do mesmo set não compartilham nada mutável. Definições nunca são escritas
  em runtime (teste de snapshot).

---

## 8. Eventos, identidade e lotes (correção 4)

### 8.1 Identidade
- **`IModifierSource`:** identidade **por referência** (`ReferenceEquals`), ignorando `Equals`/`GetHashCode`
  sobrescritos. `null` é rejeitado. Um componente Unity destruído continua sendo a mesma referência: quem aplica
  é responsável por remover (`RemoveModifiersFromSource`) ao desequipar/terminar/destruir.
- **`ModifierHandle`:** `(id da coleção, slot, geração)`. Válido somente para a coleção que o criou e enquanto o
  modificador estiver ativo. Após remoção ou expiração, o slot muda de geração: handles antigos nunca removem um
  modificador novo. `RemoveModifier` com handle inválido/antigo/de outra coleção retorna `false` sem efeito.

### 8.2 Ordem das notificações
Ao final de cada operação (ou do lote externo):
1. `StatChanged` para cada stat cujo valor mudou, em **ordem topológica** (dependências antes de dependentes);
2. eventos de recurso na ordem em que ocorreram; para cada mudança: `Changed` (pool) → `ResourceChanged`
   (coleção) → `Depleted`/`Replenished` quando houver transição.
Valores nos argumentos são os do momento da mudança.

### 8.3 Lotes (`BeginBatch`)
- **Leituras sempre atuais:** valores são recalculados imediatamente a cada operação dentro do lote.
- **Notificações adiadas e coalescidas:** um `StatChanged` por stat com `Old` = valor no início do lote e
  `New` = valor no fim (omitido se iguais). Eventos de recurso são enfileirados e emitidos no fim.
- **Máximo dos recursos** reflete a stat de máximo ao fim do lote: `ResourcePool.Max` e a política são aplicados
  uma única vez, do máximo inicial ao final (evita resultado dependente do caminho, ex.: Clamp em 100→50→150).
- Lotes aninham; o flush acontece no `Dispose` do mais externo. `Dispose` duplicado é ignorado.

### 8.4 Reentrância
Listeners podem chamar a API (ex.: ao esgotar vida, aplicar um efeito). A mutação acontece imediatamente; suas
notificações vão para o **fim da fila** e são entregues depois das atuais (FIFO, sem recursão). Uma exceção em um
listener é logada (`LogException`) e não impede os demais. Proteção contra laço: mais de 10 000 notificações em um
único despacho → `LogError` e a fila é descartada.

---

## 9. Contrato de falhas (correção 6)
| Chamada | Stat/recurso ausente | Argumento inválido (§6) |
|---|---|---|
| `GetValue`, `GetBaseValue`, `GetResource`, `AddModifier` | `StatNotFoundException` / `ResourceNotFoundException` (mensagem com stat e set) | `ArgumentException` |
| `TryGetValue`, `TryGetResource`, `TryAddModifier`, `TrySetBaseValue`, `TryAddToBaseValue` | `false` | `false` |
| `TryConsumeResources`, `CanAfford(costs)` | `MissingResource` / `false` | `InvalidCost` / `false` |
| `ResourcePool.Decrease/Increase/SetCurrent` | — | `ArgumentException` |
| `ResourcePool.TryConsume/CanAfford` | — | `false` |
| Argumento `null` (stat, recurso, fonte) | `ArgumentNullException` em todos os métodos | |
Nenhuma consulta obrigatória retorna 0 silenciosamente. Consumidores com dados opcionais (ex.: item que dá bônus a
uma stat que o ator pode não ter) usam as variantes `Try*`.

---

## 10. Integração com outros packages
| Package | Como usa | Onde fica o código |
|---|---|---|
| Combat (M4, hard) | `IStatValueSource` nas etapas de dano; `ResourcePool.Decrease(amount, instigator)`; `Depleted`. | Combat |
| Abilities (M5, hard) | `CanAfford`/`TryConsumeResources` para custos compostos; efeitos aplicam modificadores permanentes com a instância do efeito como `IModifierSource` e removem por fonte (D8). | Abilities |
| Equipment (M7, hard) | `BeginBatch` + `AddModifier`/`TryAddModifier` com a instância equipada como fonte; `RemoveModifiersFromSource` ao desequipar. | Equipment |
| Character (M3, opcional) | Velocidade a partir de uma stat + `StatChanged`. | `Character.Integration.Stats` |
| AI (M6, opcional — ADR-0009) | Condições sobre stats/recursos. | `AI.Integration.Stats` |
| Core | `IValidatable`, `ExecutionOrder`, seletor `[SelectImplementation]` (ADR-0008). | Stats |

---

## 11. Ferramentas de Unity Editor
| Ferramenta | Conteúdo |
|---|---|
| Menus `Assets/Create/RamiresTech Games/Stats/` | Stat Definition, Resource Definition, Stat Set Definition. |
| Inspector de `StatDefinition`/`ResourceDefinition` | Campos com Header/Tooltip, `StableId` somente leitura, resumo do Validator. |
| Inspector de `StatSetDefinition` | Lista editável (fórmulas pelo seletor do Core); **prévia** do set efetivo (origem herdada/própria, base, valor sem modificadores, ordem de cálculo); máximo previsto dos recursos; botão "Add missing Max stats"; resumo do Validator. |
| Inspector de `StatsComponent` | Edit Mode: set, overrides e prévia. Play Mode: valores e recursos ao vivo; botões Rebuild e Runtime Inspector. |
| **Runtime Stat Inspector** (`Tools/RamiresTech Games/Stats/Runtime Inspector`) | Lista de atores ativos; por stat: base → modificadores (fonte, operação, valor, tempo restante) → final; recursos (barra, regen/atraso); diagnósticos; log das últimas mudanças; ações de debug (adicionar modificador, remover por fonte, encher/esvaziar/alterar recurso). |
| Validators | §12. |

## 12. Validações
| Objeto | Erro | Aviso |
|---|---|---|
| `StatDefinition` | `StableId` vazio ou diferente do GUID do asset; `Min > Max`; limites não finitos; base padrão não finita | Base padrão fora dos limites; sem `DisplayName` |
| `ResourceDefinition` | `MaxStat` ausente; regen por stat sem stat; `InitialFraction` fora de [0, 1]; valores não finitos | Limites da `MaxStat` permitem máximo abaixo do `MinValue` |
| `StatSetDefinition` | Stat duplicada; ciclo de herança; ciclo entre fórmulas; fórmula ausente com origem `Formula`; fórmula lendo stat fora do set; fórmula com tipo ausente (`[SerializeReference]` órfão); `MaxStat`/stat de regen fora do set; base não finita | Base fora dos limites da stat |
| `StatsComponent` | Sem set | Override para stat fora do set ou derivada |

---

## 13. Critérios de aceite do M1
Além de QG1–QG12:

| # | Critério | Verificação |
|---|---|---|
| AC1 | Designer cria stats, recurso e set (com herança e fórmula) só pelo Inspector e vê a prévia. | Sample + revisão |
| AC2 | A matemática da §4 reproduz todos os exemplos da tabela. | Testes paramétricos |
| AC3 | **Recálculo determinístico sem drift:** para o mesmo base e o mesmo conjunto de modificadores ativos, o valor final é **bit a bit idêntico** independentemente da ordem e do histórico de adições/remoções; remover todos os modificadores de uma fonte devolve stats e `Max` dos recursos ao valor de uma coleção que nunca os recebeu. **Não** se promete restaurar `Current` de recursos que sofreram consumo, regeneração ou clamp. | Testes, incl. sequências aleatórias com seed |
| AC4 | Derivadas em ordem topológica; um `StatChanged` por stat alterada por lote; ciclos detectados pelo Validator e neutralizados em runtime com diagnóstico. | Testes |
| AC5 | Recursos: `TryConsume` tudo-ou-nada; `Decrease`/`Increase` com clamp; recuperação com atraso só após redução externa; decaimento sem atraso e sem reiniciar atraso; `Depleted`/`Replenished` uma vez por transição; 3 políticas de máximo. | Testes |
| AC6 | `TryConsumeResources`: consolida duplicados, tudo-ou-nada, nenhuma mudança nem evento em falha, eventos só após aplicar. | Testes |
| AC7 | Integridade numérica (§6) e contrato de falhas (§9) cobertos caso a caso. | Testes |
| AC8 | Eventos: ordem (§8.2), coalescência e leituras atuais em lote, reentrância FIFO, isolamento de exceções, proteção de laço; handles antigos/de outra coleção. | Testes |
| AC9 | Cache: layout compartilhado entre coleções; invalidado por mudança de versão; coleções existentes intactas; isolamento entre atores; SOs inalterados após uso. | Testes |
| AC10 | Runtime Stat Inspector mostra base → modificadores → final ao vivo e executa ações de debug. | Revisão em Play Mode |
| AC11 | Zero GC em regime: `GetValue`, `Tick` com 50 coleções, `AddModifier`/`RemoveModifier` e `TryConsumeResources` após aquecimento. | `Is.Not.AllocatingGCMemory()` |
| AC12 | Nenhum nome de stat no código do package; sample "Stats Playground" funciona sem código. | Revisão + QG4/QG11 |
| AC13 | Seletor `[SelectImplementation]` do Core: lista tipos válidos; atribuição com Undo/Redo; serialização preservada após salvar/recarregar e após recarga de domínio; tipo ausente detectado, exibido e removível; sem dependência de Stats no Core. | Testes do Core |

## 14. Estrutura do package
```
Runtime/
  Definitions/   StatsDefinitionAsset, StatDefinition, ResourceDefinition, StatSetDefinition, StatEntry, ResourceEntry, enums,
                 StatsDefinitionVersion, StatSetLayout (internal)
  Formulas/      StatFormula, LinearFormula, CurveFormula, IStatValueSource
  Modifiers/     StatModifier, StatModifierOperation, ModifierHandle, IModifierSource, ModifierSource, ActiveModifierInfo
  Values/        StatCollection, ResourcePool, ResourceCost, ResourceTransactionResult, StatBatchScope, event args,
                 StatNotFoundException, ResourceNotFoundException, BaseValueOverride
  Components/    StatsComponent, ResourceEventsComponent
Editor/          inspectors, Runtime Stat Inspector, AssetPostprocessor de versão
Tests/Editor     domínio, eventos, recursos, integridade, cache, alocação, validators
Tests/Runtime    StatsComponent/ResourceEventsComponent em Play Mode; Fixtures/
Samples~/StatsPlayground
```
