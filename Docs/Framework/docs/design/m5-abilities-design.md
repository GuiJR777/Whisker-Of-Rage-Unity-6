# M5 — Abilities · Especificação técnica

- **Status:** **v1 — proposta para revisão** (nenhum código; decisões D1–D18 e ADRs 0019–0025 aguardam aprovação)
  · **Data:** 2026-10-12
- **Unity:** 6000.6.5f1 · **Package:** `com.ramirestechgames.abilities` (L4, `RamiresTechGames.Abilities`)
- **Dependências (grafo, sem mudança):** hard `core`, `stats`; opcionais `hfsm`, `character`, `combat`,
  `com.unity.inputsystem` (ADR-0002). Equipment (M7) e AI (M6) serão consumidores opcionais.
- **Base contratual:** Core 0.4.0, Stats 0.1.0, HFSM 0.1.0, Character 0.2.0, Combat 0.1.0 (candidatos do M4). Nenhuma
  API aprovada é alterada; mudanças opcionais em outros packages estão isoladas na §20 e dependem de decisão.
- **Fora do package:** nomes e regras de um jogo. No Whiskers of Rage, Jutsus, Técnicas, elementos, chakra de Ryu e
  equipamentos são **assets e glue do jogo** construídos com os mecanismos genéricos daqui (§19). Sem sistema de cartas.

---

## 1. Objetivo e escopo

Um **sistema de habilidades genérico, data-driven e determinístico por passo fixo**, em escala indie, usado igualmente
por jogador e inimigos: habilidades ativas, passivas e disparadas por evento; custos sobre recursos do Stats com
confirmação e reembolso; cooldowns e cargas; requisitos e bloqueios por `GameplayTag`; **Gameplay Effects**
(instantâneos, temporários, infinitos, periódicos) que modificam stats e concedem tags, com stacking, imunidade e
dispel; targeting (próprio, alvo, área, direção, ponto, alcance, linha de visão); **projéteis** genéricos; composição
de habilidades por dados (linha do tempo + ações + efeitos), sem uma classe C# por habilidade; integração com HFSM,
Character, Combat e Input System.

### 1.1 Obrigatório no M5 · pendente de decisão · adiado

| Obrigatório no M5 | Pendente de decisão (§21) | Adiado (fora do M5) |
|---|---|---|
| `AbilityDefinition` (ativação, custos, cooldown/cargas, tags, targeting, linha do tempo com ações e janelas) | Relógio e hitstop (D1) | Rede, predição e rollback (R14) |
| `AbilitySystemComponent`: concessão/revogação por handle, tags possuídas com contagem, efeitos ativos, eventos | Aplicação de efeitos em duas fases (D2) | Editor de grafo de habilidades (D13) |
| Ciclo de vida: pedido → validação → commit → execução → fim/cancelamento/interrupção | Efeitos sobre o Stats (D3) | "Held" com duração de botão na fila de bordas (decisão do M4: só estado segurado) |
| Custos com verificação, confirmação e reembolso por política | Reserva de recursos (D4) | Atributos derivados exclusivos de Abilities (usa Stats) |
| Gameplay Effects: instantâneo, duração, infinito, periódico; modificadores; tags; stacking; imunidade; dispel; interações condicionais | Comandos e posse (D5) | Equipment (M7) e AI (M6) — só contratos de consumo |
| Gameplay Events e habilidades disparadas/passivas | Composição (D6), cargas (D7), concorrência (D8) | Projéteis com física dinâmica (Rigidbody) e ricochete |
| Targeting e validação de alvos; projéteis determinísticos | Projéteis × Combat (D9, D10) | Invocações/clones com IA (fornecido só o mecanismo "spawn") |
| Integrações Combat, Character, HFSM, Input System | Interrupção por golpe (D11), portão de ações do Combat (D12) | Localização, UI de habilidades, VFX/áudio (só eventos) |
| Inspector com linha do tempo, Ability Debugger, validators, gizmos/preview de targeting | Ferramentas (D13, D14), samples (D15) | |

---

## 2. Conceitos

| Termo | Definição |
|---|---|
| **Habilidade (ability)** | `AbilityDefinition`: o que pode ser ativado, a que custo, sob quais tags, mirando o quê e o que acontece ao longo do tempo. Sem nomes de jogo. |
| **Concessão (grant)** | Uma habilidade dada a um ator (`AbilitySpec`), identificada por um `AbilityHandle` (dono + vaga + geração). Guarda nível, cargas, cooldown e a fonte da concessão (ex.: um equipamento). |
| **Instância de execução** | `AbilityExecution` (pura, reutilizada): o estado de uma ativação em andamento (fase, τ, alvos, custos confirmados). |
| **Ativa / passiva / disparada** | Ativa: por comando. Passiva: ativa enquanto concedida. Disparada: ativada por um *Gameplay Event* (tag) recebido pelo ator. |
| **Gameplay Effect** | `GameplayEffectDefinition`: modificação aplicada a um ator — instantânea (altera recurso/base), com duração, infinita ou periódica; modificadores de stats, tags concedidas, imunidades. |
| **Efeito ativo** | `ActiveEffect` (puro): um efeito aplicado — stacks, tempo restante, próximo período, handles de modificadores, fonte. |
| **Spec de efeito** | `EffectSpec` (valor): definição + nível + instigador + magnitude capturada + tags de origem; o que viaja até o alvo. |
| **Tag possuída** | Tag ativa no ator com **contagem** (concedida por efeitos, por habilidades em execução ou por integrações). |
| **Gameplay Event** | Ocorrência com tag e payload (instigador, alvo, magnitude) enviada a um ator; pode disparar habilidades passivas. |
| **Targeting** | Estratégia que produz `AbilityTargetData` (atores, pontos, direção) e regras de validação (alcance, linha de visão, filtros). |
| **Projétil** | Entidade cinemática criada por uma habilidade, movida no passo fixo, que entrega ações/efeitos (ou um golpe do Combat) ao atingir. |
| **Commit** | Momento em que custos são debitados e o cooldown começa. Antes dele, cancelar não custa nada. |

---

## 3. Arquitetura

### 3.1 Camadas e assemblies

| Camada | Assembly | Conteúdo |
|---|---|---|
| Domain | `RamiresTechGames.Abilities` (`Runtime/Execution`, `Runtime/Effects`, `Runtime/Tags`, `Runtime/Targeting`) | `AbilityExecution`, `AbilityTimeline`, `ActiveEffectSet`, `StackingRules`, `OwnedTagCounter`, `TagRequirements`, `CostLedger`, `CooldownSet`, `TargetQuery` (puros, recebem Δt) |
| Unity Integration | `RamiresTechGames.Abilities` (`Runtime/Components`, `Runtime/Commands`, `Runtime/Definitions`, `Runtime/Projectiles`) | `AbilitySystemComponent` (+ helper de fase), `AbilityCommandBuffer`, definições (SO), `Projectile`, `ProjectilePool` |
| Contratos de extensão | `RamiresTechGames.Abilities` (`Runtime/Extensibility`) | `IAbilityAction`, `IAbilityWindowBehaviour`, `ITargetingStrategy`, `ITargetFilter`, `IActivationRequirement`, `IEffectExecution`, `IMagnitudeCalculation`, `IAbilityTimeSource`, `ITeamRelation`, `IAbilityTargetPoint` |
| Integration | `RamiresTechGames.Abilities.Integration.Combat` | Golpes do Combat aplicam efeitos (`IHitEffect`), entrega de projéteis/áreas como golpe (`ICombatHitSource`), ação "executar ataque", estados de combate → tags, interrupção por golpe, hitstop, times |
| Integration | `RamiresTechGames.Abilities.Integration.Character` | Ações de impulso, teleporte, suspensão (Freeze/Hold), fonte de modificadores de movimento, ponto/direção do facing |
| Integration | `RamiresTechGames.Abilities.Integration.HierarchicalStateMachine` | `AbilityBehaviour` (claim → pedido → conclusão), condições de comando, tags e estado |
| Integration | `RamiresTechGames.Abilities.Integration.InputSystem` | `PlayerInputAbilitySource` (botão → tag de entrada; segurado → estado) |
| Editor | `RamiresTechGames.Abilities.Editor` | Inspectors (linha do tempo da habilidade, resumo do efeito), preview de targeting/trajetória, Ability Debugger, validators |

### 3.2 Componentes de runtime

| Componente | Responsabilidade |
|---|---|
| `AbilitySystemComponent` (ASC) | Ponto único do ator: concessões, execuções, tags possuídas, efeitos ativos, cooldowns/cargas, eventos, caixa de entrada de efeitos. Tica na banda `ABILITIES`. |
| `AbilityCommandBuffer` | Bordas de ativação (tag de entrada, direção, alvo sugerido) sobre `EdgeQueue` do Core; estado "segurado" por tag de entrada; posse `Abilities`/`External` (ADR-0022). |
| `AbilityExecution` (puro) | Máquina de estados de uma ativação (§5), relógio próprio, alvos, custos confirmados. Pré-alocado por concessão. |
| `ActiveEffectSet` (puro) | Efeitos ativos com stacking, períodos, expiração, handles de modificadores do Stats. |
| `OwnedTagCounter` (puro) | Tags possuídas com contagem e fonte; consultas hierárquicas sem alocação. |
| `Projectile` / `ProjectilePool` | Projétil cinemático pooled; um `ProjectileDriver` por cena (sem singleton estático: componente explícito) avança e detecta no passo fixo. |
| `AbilityTargetPoint` | Ponto nomeado (boca, mão) de onde saem projéteis e áreas, por `GameplayTag` — mesmo modelo de pose canônica das `HitboxAnchor` (ADR-0016 refinado). |

### 3.3 Fluxo de um passo fixo

```
COMMAND_SOURCES (Update)        fontes escrevem bordas/estado no AbilityCommandBuffer (tag de entrada, mira)
STATE_MACHINE   (Update)        HFSM: condição espia + claim → AbilityBehaviour.RequestActivation(token)
ABILITIES       (−700) ASC      A1: pedidos → validação → commit → execuções avançam τ → ações do passo
                                    (efeitos saem para caixas de entrada; projéteis/áreas são criados)
ABILITIES + 50  (−650) ASC      A2: aplica a própria caixa (ordem total) → efeitos periódicos/expiração →
                                    tags e modificadores atualizados → eventos FIFO
COMBAT … +80    (−600…−520)     Combat (fase 1 inclui ProjectileCombatSource: candidatos de projéteis/áreas)
CHARACTER       (−500)          motor (impulsos das ações de habilidade entram no mesmo passo)
STATS           (Update)        Stats.Tick (regeneração de recursos)
```

Regras: (1) nada decidido em A1 de um ator lê estado mutável de outro ator além do snapshot do passo; efeitos entre
atores **sempre** passam pela caixa de entrada e são aplicados em A2, ordenados por (instigador, sequência) — mesma
ideia do Combat (ADR-0014). (2) Um efeito pedido em A2 (ex.: interação condicional) entra na caixa do passo seguinte.
(3) Modificadores aplicados em A2 do passo *n* valem para o Combat do mesmo passo *n* (Combat roda depois);
efeitos pedidos pelo Combat (`IHitEffect`, fase 4) valem em A2 do passo *n + 1*.

---

## 4. Definições (ScriptableObjects, imutáveis em runtime)

### 4.1 `AbilityDefinition`

| Grupo | Campos |
|---|---|
| Identidade | `AbilityTags` (o que a habilidade é), `InputTag` padrão (pode ser sobrescrita na concessão) |
| Ativação | `ActivationMode` (`Command`, `Passive`, `OnEvent`), `TriggerEvents` (tags de evento + condição), `RetriggerPolicy` (`Reject`, `Restart`), `MaxLevel` |
| Tags | `ActivationRequiredTags`, `ActivationBlockedTags`, `ActivationOwnedTags` (concedidas durante a execução), `CancelAbilitiesWithTags`, `BlockAbilitiesWithTags` (bloqueiam outras enquanto executa), `SourceRequired/Blocked`, `TargetRequired/Blocked` |
| Requisitos | `Requirements` (SR `IActivationRequirement`: postura, stat ≥ x, recurso ≥ x, condição do jogo) |
| Custos | `Costs` (recurso, quantidade fixa ou por nível/curva/stat), `CommitPoint` (`OnActivate`, `AtTime`, `OnFirstDelivery`), `RefundPolicy` (§6) |
| Cooldown/cargas | `Cooldown` (duração por nível/stat), `CooldownGroupTag` (cooldown compartilhado), `MaxCharges`, `RechargeTime`, `ChargeConsumption` |
| Targeting | `Targeting` (SR `ITargetingStrategy`), `Filters` (SR `ITargetFilter`), `SnapshotPolicy` (`AtActivation`, `PerAction`) |
| Linha do tempo | `Duration` (ou `UntilEnd`), `Phases` (rótulos: windup/ativo/recuperação/canal para ferramentas), `Actions` (SR `IAbilityAction` em instantes τ), `Windows` (SR `IAbilityWindowBehaviour` em `[a, b)`), `ChannelRequiresHeld` |
| Interrupção | `InterruptibleBy` (tags de interrupção: golpe, stun, silêncio), `PausedByHitstop` (padrão: sim), `CancelOnOwnerDisabled` |
| Apresentação | só eventos (`AbilityActivated`, ações com tags de cue); nenhum Animator/VFX no package |

### 4.2 `GameplayEffectDefinition`

| Grupo | Campos |
|---|---|
| Identidade | `AssetTags` (o que o efeito é: usadas por imunidade, dispel, interações) |
| Duração | `DurationPolicy` (`Instant`, `HasDuration`, `Infinite`), `Duration` (magnitude), `Period`, `ExecutePeriodicOnApplication` |
| Modificadores | lista de (`StatDefinition`, `StatModifierOperation`, magnitude) — aplicados ao Stats com o efeito ativo como `IModifierSource` (§7.2) |
| Execuções | SR `IEffectExecution` (instantâneas ou por período): `ResourceDelta` (cura/dano simples, absoluto ou por stat), `SetBaseValue`/`AddToBaseValue`, `GrantResource`, `SendEvent` |
| Tags | `GrantedTags` (enquanto ativo), `ApplicationRequiredTags/BlockedTags` (no alvo), `GrantedImmunityTags` (bloqueia efeitos com essas `AssetTags`), `RemoveEffectsWithTags` (dispel ao aplicar), `OngoingRequiredTags` (inibe enquanto faltar) |
| Stacking | `StackingPolicy` (`Independent`, `AggregateByTarget`, `AggregateBySource`), `StackLimit`, `OverflowPolicy` (`Reject`, `RefreshOnly`, `ReplaceOldest`), `DurationRefresh` (`Refresh`, `Extend`, `Keep`), `PeriodReset` (`Reset`, `Keep`), `ExpirationPolicy` (`RemoveAll`, `RemoveOneStack`), magnitude por stack (linear ou curva) |
| Interações | `ConditionalEffects` (se o alvo tiver tags X → aplicar efeito Y; opcional: remover efeitos com tags Z) — base genérica para "combinações de elementos" (§19) |
| Remoção | `RemoveWhenOwnerGainsTags`, `RemoveOnEvents` (tags de evento), `MaxDuration` de segurança |

### 4.3 Outras definições
| Definição | Conteúdo |
|---|---|
| `ProjectileDefinition` | Velocidade, aceleração, gravidade, alcance/tempo de vida, raio (esfera) ou cápsula, máscara, perfuração (`MaxHits`, `HitInterval`), direção guiada opcional (taxa de curva até o alvo), ações ao acertar/expirar (SR), `CombatDelivery` opcional (integração Combat) |
| `AbilitySetDefinition` | Lista de concessões iniciais (habilidade, nível, tag de entrada) e efeitos iniciais (passivos) — loadout do ator |
| `AbilityInputMapDefinition` | Opcional: tag de entrada → ação de input (integração Input System) |

Todas implementam `IValidatable`; nenhuma conhece nomes de jogo.

---

## 5. Ciclo de vida da habilidade

### 5.1 Estados de uma execução
```
Idle ──pedido──▶ Validating ──ok──▶ Committed? ─▶ Running ──fim──▶ Ending ─▶ Idle
                    │falha                      │ cancel (voluntário) / interrupt (golpe, tag, morte)
                    ▼                            ▼
                 Rejected(motivo)             Cancelled / Interrupted ─▶ Ending ─▶ Idle
```
- **Validação** (A1, ordem fixa, primeiro motivo vence): concedida e habilitada → não em execução (ou `Restart`) →
  tags do ator (`Required`/`Blocked`, bloqueios de outras habilidades) → requisitos SR → cooldown/cargas → custos
  pagáveis (incluindo reservas, §6) → targeting válido (se `SnapshotPolicy = AtActivation`). Resultado:
  `ActivationResult` (`Activated`, `NotGranted`, `OnCooldown`, `NoCharges`, `BlockedByTags`, `MissingTags`,
  `RequirementFailed`, `CannotAfford`, `InvalidTarget`, `AlreadyActive`).
- **Commit** no `CommitPoint`: debita custos atomicamente (`TryConsumeResources`), consome carga e inicia cooldown.
  Se o commit falhar (recurso gasto por outro sistema entre validação e commit), a execução termina como
  `Cancelled(CommitFailed)` sem efeitos posteriores.
- **Execução**: τ em segundos de jogo; ações em τ e janelas `[a, b)` pela **regra de sobreposição** do Combat
  (ADR-0013): nenhuma ação/janela pulada com Δt diferentes; ações dentro de uma janela ativa disparam uma vez.
- **Fim**: ao atingir `Duration`, quando uma ação `EndAbility` executa, ou ao soltar o "segurado" (`ChannelRequiresHeld`).
- **Cancelamento** (voluntário: comando, `CancelAbilitiesWithTags`, troca de estado) e **interrupção** (involuntária:
  tag de interrupção recebida, golpe que interrompe — integração Combat —, ASC desabilitado, ator morto): ambos param
  as ações futuras, removem `ActivationOwnedTags`, encerram janelas (`OnWindowExit`) e aplicam a política de reembolso.

### 5.2 Concorrência
- Habilidades diferentes podem executar ao mesmo tempo; a exclusão é declarada por tags (`ActivationOwnedTags` +
  `ActivationBlockedTags`/`BlockAbilitiesWithTags`/`CancelAbilitiesWithTags`), como no GAS, sem um "slot" fixo.
- A mesma concessão tem **uma** execução por vez (`RetriggerPolicy`: `Reject` ou `Restart` = cancela e reinicia).
- Passivas não usam execução com τ: aplicam seus efeitos infinitos enquanto concedidas (revogar remove).
- Disparadas por evento usam a mesma validação (custos, cooldowns, tags) — ex.: "ao receber golpe, 20% de chance de
  contra-ataque" é dado + condição.

### 5.3 Pedidos e posse (ADR-0022)
`AbilityCommandBuffer` espelha o `CombatCommandBuffer` (ADR-0015): bordas com token e prioridade sobre `EdgeQueue`
(Core 0.4.0, 3.º consumidor), posse `Abilities` (o ASC consome a melhor borda válida) ou `External` (a HFSM
reivindica a borda e chama `RequestActivation(token)`; o ASC executa no primeiro passo possível; `CancelRequest(token)`
descarta). Estado segurado por tag de entrada (`SetHeld(inputTag, bool)`) para canalizar/carregar — **sem** duração
de botão na fila (decisão do M4). Resultado do pedido notificado uma vez (`ActivationResolved`).

---

## 6. Custos e recursos

| Item | Decisão proposta |
|---|---|
| Fonte | Recursos do Stats (`ResourceDefinition`), via `StatCollection.TryConsumeResources` (atômico, tudo-ou-nada). |
| Verificação | Na validação: `CanAfford(custos + reservas do ator)`. |
| Reserva | **Livro de reservas no ASC** (`CostLedger`, D4): uma execução validada com `CommitPoint` posterior reserva o custo; outras habilidades do mesmo ator veem o recurso menos as reservas. Não trava o recurso no Stats (outros sistemas — Combat, scripts — ainda podem gastar): o commit pode falhar e é tratado (§5.1). Reserva real no Stats fica como mudança opcional (§20). |
| Commit | `OnActivate` (padrão), `AtTime(τ)` (ex.: só debita quando o feitiço "sai"), `OnFirstDelivery` (primeiro alvo atingido). |
| Reembolso | Política por habilidade: `None`, `OnCancelBeforeDelivery` (cancelada antes de qualquer entrega), `OnInterrupt` (fração configurável), `OnNoTarget` (nenhum alvo válido na entrega). Reembolso = `ResourcePool.Increase` com a habilidade como instigador; nunca acima do debitado. |
| Custos dinâmicos | Magnitude por nível (curva), por stat (`IMagnitudeCalculation`) ou modificada por efeitos (stat de "custo" do jogo). |
| Canal | Custo por período (`ResourceDelta` em janela) — se não puder pagar, a janela termina e a habilidade cancela (`CannotSustain`). |

Nada de "chakra" ou "PH" no package: o jogo cria os recursos no Stats e os referencia nos custos.

---

## 7. Gameplay Effects

### 7.1 Tipos e aplicação
- **Instant**: executa `Executions` uma vez (ex.: dano simples, cura, ganho de recurso, mudança de base) e não fica ativo.
- **HasDuration / Infinite**: fica ativo; aplica modificadores e tags enquanto ativo; `Period` > 0 executa
  `Executions` a cada período (veneno, regeneração extra).
- **Pipeline de aplicação (A2)**: imunidades do alvo (`GrantedImmunityTags` × `AssetTags`) → `ApplicationRequired/
  BlockedTags` → stacking (§7.3) → modificadores/tags → `RemoveEffectsWithTags` (dispel) → `ConditionalEffects`
  (entram na caixa do próximo passo) → eventos (`EffectApplied`, `EffectStacked`, `EffectBlocked(motivo)`).
- **Inibição**: `OngoingRequiredTags` ausentes suspendem modificadores/tags do efeito sem removê-lo (volta quando a
  condição volta), como no GAS.

### 7.2 Sobre o Stats — sem duplicação (ADR-0020)
| Necessidade | Quem faz |
|---|---|
| Matemática dos modificadores (`Flat`/`Additive`/`Multiplicative`, arredondamento, limites, derivadas) | **Stats** (inalterado) |
| Remover exatamente o que um efeito colocou | **Stats**: cada `ActiveEffect` é o `IModifierSource`; `RemoveModifiersFromSource` (garantia 2 do Stats: igual a nunca ter recebido) |
| Duração, período, stacking, inibição, pausa | **Abilities** (no passo fixo). Os modificadores são aplicados **sem** `Duration` do Stats (infinitos) e removidos pelo Abilities: o Stats expira modificadores temporizados no `Update` com Δt variável e sem noção de stacks/hitstop |
| Mudanças instantâneas de recurso | **Stats** (`Increase`/`Decrease`), chamadas pelas `Executions` |
| Leitura de atributos para magnitudes | `IStatValueSource` do Stats |

### 7.3 Stacking
`AggregateByTarget` (um `ActiveEffect` por definição no alvo) e `AggregateBySource` (um por par definição+instigador)
somam stacks até `StackLimit`; magnitude dos modificadores = base × f(stacks) (linear ou curva). `Independent` cria
instâncias separadas (limite de segurança por definição). Políticas de duração/período/expiração da tabela 4.2.
Todo stacking é determinístico: a caixa de entrada é aplicada na ordem total (instigador, sequência).

### 7.4 Imunidade, dispel e remoção
Imunidade = tags concedidas que bloqueiam efeitos com `AssetTags` correspondentes (hierárquicas: imune a "Element"
bloqueia "Element.Fire"). Dispel = execução/efeito que remove efeitos ativos por consulta de `AssetTags` (todos, N
stacks ou os mais antigos). `RemoveWhenOwnerGainsTags` e `RemoveOnEvents` cobrem "quebra ao receber golpe".

---

## 8. Gameplay Tags (Core 0.4.0, sem mudança)

- O Core fornece `GameplayTag` (hierárquico) e `GameplayTagSet` (conjunto serializável). O Abilities adiciona, **no
  próprio package**, `OwnedTagCounter` (tags possuídas com contagem por fonte, capacidade fixa, consultas `Has`/`HasAny`
  /`HasAll` hierárquicas sem alocação) e `TagRequirements` (exige todas de `Require`, nenhuma de `Ignore`) — ADR-0025.
- Fontes de tags possuídas: efeitos ativos (`GrantedTags`), execuções (`ActivationOwnedTags`), concessões passivas,
  integrações (estados de combate, §12.1) e API (`AddLooseTag/RemoveLooseTag` com handle, para o jogo).
- `TagsChanged` agrega mudanças do passo (um evento por tag que entrou/saiu, em A2).
- Proposta para o futuro (não necessária no M5): mover `OwnedTagCounter`/`TagRequirements` para o Core quando a AI
  (M6) precisar — regra de ≥ 2 consumidores.

---

## 9. Targeting

| Estratégia (SR `ITargetingStrategy`) | Produz |
|---|---|
| `SelfTarget` | o próprio ator |
| `CommandTarget` | alvo/ponto/direção vindos do comando (mira do jogador, alvo escolhido pela AI) |
| `NearestInRange` | ator mais próximo dentro do alcance (desempate por identidade) |
| `AreaSphere`, `AreaBox`, `AreaCone` | atores na área (consulta `NonAlloc`, ordem total por distância e identidade), origem no ator, num `AbilityTargetPoint` ou num ponto |
| `Direction` | direção (facing lógico, mira ou para o alvo) |
| `GroundPoint` | ponto no chão (raycast) dentro do alcance |

Filtros (SR `ITargetFilter`, combináveis): relação de time (`ITeamRelation`; a integração Combat usa o perfil),
tags do alvo (`TargetRequired/Blocked`), vivo/morto (tag), alcance mín./máx., **linha de visão** (raycast `NonAlloc`
contra máscara de obstáculos), máximo de alvos, excluir o próprio ator. Validação produz `TargetingResult`
(`Valid`, `NoTarget`, `OutOfRange`, `NoLineOfSight`, `FilteredOut`).

`AbilityTargetData`: estrutura de valor com capacidade fixa (atores, pontos, direção, origem) — pré-alocada por
execução; nenhuma alocação por ativação. `SnapshotPolicy`: `AtActivation` (alvos congelados) ou `PerAction` (cada ação
reavalia; ex.: rajada que segue o alvo).

---

## 10. Composição de habilidades (ADR-0023)

Uma habilidade é **dados**: linha do tempo com ações pontuais e janelas, cada uma um `[SerializeReference]` escolhido
no seletor do Core. Nenhuma classe por habilidade; novos comportamentos genéricos = novas implementações SR (do
package, das integrações ou do jogo).

| Ações incluídas (`IAbilityAction`, em τ) | Janelas incluídas (`IAbilityWindowBehaviour`, em `[a, b)`) |
|---|---|
| `ApplyEffectToTargets`, `ApplyEffectToSelf`, `RemoveEffectsWithTags` | `ApplyEffectWhileActive` (efeito enquanto a janela durar) |
| `SpawnProjectile` (definição, ponto, direção, dispersão, quantidade) | `GrantTagsWhileActive` (ex.: invulnerável, superarmadura via integração) |
| `SpawnArea` (área instantânea ou persistente que aplica efeitos/entrega golpe) | `PeriodicAction` (repete uma ação a cada período: canal) |
| `SendGameplayEvent`, `EndAbility`, `RetargetIfNeeded` | `RequireHeld` (encerra a habilidade ao soltar) |
| `Spawn` (instancia prefab do jogo com instigador; base para clones/armadilhas — comportamento do objeto é do jogo) | `ConsumeResourceOverTime` |
| Integrações: `PerformAttack`, `ApplyImpulse`, `Teleport`, `FreezeMotor`/`Hold` (Character/Combat) | Integrações: `MovementMultiplier`, `LockFacing` |

Magnitudes (`IMagnitudeCalculation`): constante, curva por nível, stat do instigador/alvo × fator, `SetByCaller`
(valor posto pelo jogo no pedido, por tag). **Critério de aceite:** duas habilidades novas montadas só com assets.

---

## 11. Projéteis (D9, D10 — ADR-0024)

- **Simulação própria e determinística:** cinemática no passo fixo (`p += v·Δt`, gravidade/aceleração, curva guiada
  limitada), sem Rigidbody dinâmico. Colisão por **sweep** (`SphereCast`/`CapsuleCast` `NonAlloc`) do ponto anterior
  ao atual — sem tunneling por velocidade; buffers pré-alocados com saturação contada (política do M3/M4).
- **Ordem e identidade:** cada projétil tem `ProjectileId` = identidade do instigador combinada com o índice de
  criação (como `CombatantId.Combine`); resultados entre projéteis e alvos independem da ordem dos componentes.
- **Pooling:** `ProjectilePool` por definição/prefab, pré-aquecido; zero GC em regime. Capacidade e excedente contados.
- **Duas entregas:**
  1. **Sem Combat**: ao atingir um ator com ASC, aplica as ações/efeitos de acerto (pela caixa de entrada, §3.3).
  2. **Com Combat** (integração): o projétil vira um **produtor de candidatos** `ICombatHitSource` e enfileira
     `AttackSnapshot`s na fase 1 do Combat (banda `COMBAT`) com as `HitProperties` do projétil. Defesa, parry,
     bloqueio, poise, dano, reações e hitstop são do Combat; efeitos de habilidade vão por `IHitEffect`. O
     `ReceiveHitFeedback` decide o destino do projétil (parado por bloqueio, destruído, perfurando).
- **Áreas** (`SpawnArea`) usam o mesmo caminho: consulta por passo, registro "uma vez por alvo ou por intervalo".
- **Fora do M5:** reflexão por parry (exige regra nova no Combat), ricochete físico, projéteis com Rigidbody.

---

## 12. Integrações e autoridade

| Domínio | Autoridade | Abilities faz | Nunca faz |
|---|---|---|---|
| Habilidades, custos, cooldowns, efeitos, tags possuídas | **Abilities** | tudo | — |
| Ataque corpo a corpo, detecção, resolução, dano com defesa, reações, hitstop | **Combat** | pede `StartAttack`; entrega projéteis/áreas como candidatos; aplica efeitos via `IHitEffect` | decidir um golpe; mudar recursos de vida fora do pipeline quando a entrega é do Combat |
| Movimento, pulo, dash, suspensão | **Character** | impulsos (`IExternalForceReceiver`), `Teleport`, `Freeze`/`Hold` por handle, multiplicadores (`IMovementModifierSource`) | integrar o motor |
| Estado do personagem | **HFSM** | estado `AbilityBehaviour`, condições | trocar estados diretamente |
| Atributos e recursos | **Stats** | modificadores com fonte, consumo/ganho de recurso | matemática de stats própria |
| Decisão (jogador/AI) | **fonte de comandos** | — | ler input fora da integração Input System |

### 12.1 Combat (`Abilities.Integration.Combat`)
- `ApplyGameplayEffectOnHit : IHitEffect` (SR em `HitProperties.Effects`): enfileira um `ICombatEffect` reutilizado
  que aplica o `EffectSpec` ao ASC do alvo no passo seguinte (regra do Combat respeitada).
- `CombatProjectileSource`/`CombatAreaSource` (`ICombatHitSource`, banda `COMBAT`): candidatos de projéteis/áreas.
- Ação `PerformAttack` (inicia um `AttackDefinition` no `CombatController` do ator) e janela `GrantArmor`.
- `CombatStateTags`: espelha estados do `CombatTarget` em tags configuráveis (hitstun, knockdown, agarrado, guarda,
  hitstop) — base para `ActivationBlockedTags` e interrupção.
- Interrupção: golpe com `Interrupts` envia o evento configurado (ex. tag "interrompido") → habilidades com essa tag
  em `InterruptibleBy` são interrompidas (D11).
- `IAbilityTimeSource` pelo hitstop do `CombatTarget`; `ITeamRelation` pelo perfil (`IsHostileTo`).
- Eventos de combate → Gameplay Events configuráveis (golpe confirmado, recebido, bloqueado, parry, abate) — base de
  passivas "ao acertar, ganhar recurso" sem código.

### 12.2 Character
`ApplyImpulse` (espaço do facing), `Teleport`, `FreezeMotor`/`HoldTo` (handles, liberados no fim/cancelamento),
`AbilityMovementModifierSource` (encadeável como o do Combat: multiplicadores por janelas/efeitos), facing lógico e
pontos de origem.

### 12.3 HFSM
`AbilityBehaviour` (claim da borda → `RequestActivation` → completa quando a execução termina; `OnExit` cancela o
pedido e, opcionalmente, a execução), condições `HasAbilityCommand`, `CanActivateAbility`, `IsAbilityActive(tags)`,
`HasOwnedTags`.

### 12.4 Input System, Equipment (M7), AI (M6)
- Input: `PlayerInputAbilitySource` (ação → tag de entrada; segurado → `SetHeld`).
- Equipment (futuro): concede habilidades e aplica efeitos infinitos com **o equipamento como fonte**
  (`GrantAbility(def, level, source)`, `ApplyEffect(spec)`, `RemoveBySource(source)`); nada do M7 é implementado agora.
- AI (futuro): escreve no `AbilityCommandBuffer` e consulta `CanActivate`/`GetCooldownRemaining`/tags.

---

## 13. Tempo e hitstop (ADR-0019)

| Relógio | Regra proposta |
|---|---|
| Linha do tempo da execução (τ) | Segundos de jogo acumulando Δt fixo; janelas pela regra de sobreposição (ADR-0013); **pausa no hitstop** do ator quando há `IAbilityTimeSource` (padrão `PausedByHitstop = true`, como os ataques) |
| Cooldowns e recargas | Segundos de jogo no passo fixo; **não** pausam no hitstop (padrão; configurável por habilidade) |
| Duração e período de efeitos | Segundos de jogo no passo fixo; **não** pausam no hitstop (padrão; configurável por efeito) |
| Projéteis | Passo fixo; não pausam no hitstop do instigador (configurável por definição) |
| `timeScale` | Respeitado pelo passo fixo da Unity; sem relógio próprio por ator (time scale por ator fica fora do M5) |
| Recursos do Stats | Regeneração continua no `Update` do Stats (inalterado); custos e efeitos instantâneos são aplicados no passo fixo |

---

## 14. Execução e desempenho

- Banda `ABILITIES` (−700) com duas sub-bandas (A1 = +0, A2 = +50); um componente de fase oculto, como no Combat.
- Pré-alocação: execuções por concessão, `AbilityTargetData`, caixas de entrada, buffers de consulta, pools de
  projéteis. Zero GC em regime (teste com 50 atores ativando, aplicando efeitos com stacking, projéteis e áreas).
- Capacidades fixas com contadores de saturação (caixa de entrada, tags possuídas, efeitos ativos, projéteis).

---

## 15. Ferramentas de autoria (D13, D14)

| Ferramenta | Conteúdo |
|---|---|
| Inspector de `AbilityDefinition` | Linha do tempo (ações, janelas, commit, fases) em s, frames@60 e passos do Δt; resumo de custos/cooldown/cargas/tags; validação inline |
| Inspector de `GameplayEffectDefinition` | Resumo legível (duração, período, modificadores, stacking, tags concedidas/imunidades/dispel, interações) |
| Preview na Scene | Áreas de targeting no ator escolhido (esfera/caixa/cone, alcance, linha de visão); trajetória prevista do projétil (amostras por passo, sweep) |
| Ability Debugger (Play Mode) | Atores com ASC; concessões (estado, τ, cooldown, cargas, último resultado de ativação); tags possuídas com contagem e fonte; efeitos ativos (stacks, tempo, período, inibido); reservas; últimos eventos; botões (conceder, aplicar efeito, limpar cooldowns) |
| Validators | Ações/janelas fora da duração, commit depois do fim, custo sem recurso, recurso fora do stat set do ator, cooldown negativo, cargas sem recarga, stacking sem limite, período sem duração, tags contraditórias (exigida e bloqueada), efeito que concede imunidade a si mesmo, interação condicional circular, projétil sem máscara/velocidade, targeting sem filtro de time em área |

**Editor de grafo — não proposto no M5 (D13).** As habilidades são lineares no tempo (linha do tempo + listas SR) e
os efeitos são registros; um grafo visual adicionaria ~1.500 linhas de Editor e uma segunda forma de expressar a
mesma coisa, sem ganho de determinismo nem de validação. Reavaliar quando houver um caso real de ramificação dentro
de uma habilidade (escolha por condição em τ) que listas não expressem bem; o Combo Graph do Combat já cobre o
encadeamento entre ataques.

**Código de linha do tempo compartilhado (D14):** o desenho da linha do tempo do `AttackDefinition` (Combat.Editor)
seria o 2.º consumidor da mesma ideia. Proposta: duplicar o necessário no Abilities.Editor (não há dependência
Abilities → Combat obrigatória) e avaliar extração para `Core.Editor` só com o 3.º consumidor (BT no M6), com ADR.

---

## 16. Samples (D15)
- **Abilities Playground** (Abilities + Stats): atores simples; bola de fogo (projétil + efeito periódico), escudo
  (efeito com duração + imunidade), aura (passiva), dash de energia (custo + cooldown + cargas), dispel, "combinação"
  genérica (efeito A + efeito B → efeito C por `ConditionalEffects`); painel com tags e efeitos.
- **Abilities Combat Character HFSM** (opcional; Combat + Character + HFSM): mesma definição de estados do sample do
  Combat com um estado de habilidade; projétil entregue como golpe do Combat (bloqueável, parry stun), golpe com efeito
  de habilidade, passiva "ao acertar, ganhar recurso", interrupção por golpe.

---

## 17. API pública (proposta)
| Tipo | Categoria | Estabilidade |
|---|---|---|
| `AbilityDefinition`, `GameplayEffectDefinition`, `ProjectileDefinition`, `AbilitySetDefinition` | SO | Experimental |
| `AbilitySystemComponent` (`Grant/Revoke`, `TryActivate`, `Cancel`, `ApplyEffect`, `RemoveEffect`, `RemoveEffectsWithTags`, `SendEvent`, `HasTags`, `GetCooldown`, `GetCharges`, eventos) | MB | Experimental |
| `AbilityCommandBuffer` | MB | Experimental |
| `AbilityHandle`, `ActiveEffectHandle`, `LooseTagHandle` (dono + vaga + geração) | struct | Experimental |
| `EffectSpec`, `AbilityTargetData`, `ActivationResult`, `TargetingResult`, args de eventos | struct/enum | Experimental |
| `IAbilityAction`, `IAbilityWindowBehaviour`, `ITargetingStrategy`, `ITargetFilter`, `IActivationRequirement`, `IEffectExecution`, `IMagnitudeCalculation`, `IAbilityTimeSource`, `ITeamRelation` | interfaces/SR | Experimental |
| `Projectile`, `ProjectilePool`, `ProjectileDriver`, `AbilityTargetPoint` | MB | Experimental |

Handles seguem a lição do M4 (dono + vaga + geração; handle estrangeiro nunca age).

---

## 18. Qualidade

### 18.1 Estratégia de testes
**EditMode (domínio):** validação de ativação (cada `ActivationResult`, ordem dos motivos); commit nos três pontos;
reembolso por política; livro de reservas; cooldowns, grupos e cargas (recarga parcial, consumo múltiplo) em 4 Δt;
linha do tempo pela regra de sobreposição (nenhuma ação/janela pulada; ações uma vez); cancelamento × interrupção
(ações futuras não executam, tags removidas, reembolso); concorrência por tags; efeitos: instant/duração/infinito/
periódico (contagem de períodos em 4 Δt), stacking (cada política), inibição, imunidade hierárquica, dispel,
`ConditionalEffects` sem laço; tags com contagem (fontes múltiplas, remoção parcial); remoção exata de modificadores
(`RemoveModifiersFromSource` iguala coleção virgem); handles estrangeiros/obsoletos; validators.

**PlayMode (física real):** targeting (áreas, cone, linha de visão com obstáculo, ordem total, máximo de alvos);
projéteis rápidos contra alvos finos sem tunneling; perfuração e intervalo; **independência de ordem** (mesma cena
com ordem de criação embaralhada → mesmos efeitos, stacks e tags); integração Combat (projétil bloqueado, parry,
efeito via `IHitEffect` no passo seguinte, interrupção por golpe, hitstop pausando τ mas não cooldowns); Character
(impulso, suspensão liberada no cancelamento); HFSM (claim → pedido → conclusão; `OnExit` cancela); Input System;
**zero GC** com 50 atores.

### 18.2 Critérios de aceite
| # | Critério |
|---|---|
| AC1 | Duas habilidades novas montadas só com assets (sem código), uma com projétil e outra com efeito periódico e stacking. |
| AC2 | Ativa, passiva e disparada por evento usando a mesma validação (tags, custos, cooldowns). |
| AC3 | Custos: nenhum débito antes do commit; reembolso exato pela política; commit que falha não deixa efeito. |
| AC4 | Remover um efeito restaura os stats exatamente (garantia do Stats), em qualquer ordem de stacks. |
| AC5 | Resultados independentes da ordem entre atores (efeitos cruzados, stacks, projéteis). |
| AC6 | Nenhuma ação/janela pulada e mesma sequência de decisões em Δt 0,02 / 1/60 / 0,01 / 0,0333 e `timeScale` 0,5. |
| AC7 | Integração Combat via `IHitEffect` e `ICombatHitSource` sem mudar contratos do Combat; projétil bloqueável/parável. |
| AC8 | Zero GC em regime com 50 atores; consultas de física sem alocação. |
| AC9 | Nenhum nome de jogo (Jutsu, chakra, Ryu, carta) em código, assets do package ou samples. |
| AC10 | QG1–QG12 (QG12 em projeto vazio com Core + Stats). |

### 18.3 Quality Gates
QG1–QG12 como nos milestones anteriores; validação manual das ferramentas documentada (interação real × sintética).

---

## 19. Whiskers of Rage — como o jogo usará os mecanismos (sem código no package)

| Necessidade do jogo | Mecanismo genérico |
|---|---|
| Jutsus elementais | Habilidades ativas com custo num recurso **definido pelo jogo** no Stats, projéteis/áreas, efeitos com `AssetTags` de elemento (tags do jogo) |
| Combinações de elementos | `ConditionalEffects`: efeito com tag A aplicado a alvo com tag B → aplica efeito C e consome A/B (regras = assets do jogo) |
| Técnicas passivas | Habilidades passivas (efeitos infinitos) ou disparadas por eventos de combate (golpe confirmado, parry, abate) |
| Barra que carrega com golpes e gera pontos | Passiva disparada por evento de golpe que aplica efeito instantâneo de ganho de recurso; conversão por limiar = efeito condicional do jogo |
| Chakra/recurso do protagonista | `ResourceDefinition` do jogo; nada no package |
| Equipamentos (M7) | Concessão de habilidades e efeitos infinitos com o equipamento como fonte |
| Chefes (área, giro, lasers) | Áreas persistentes, janelas periódicas, projéteis (lasers longos = cápsulas/sweep) |
| Ideias do GDD 2025 (clones, nuvem, selamento, duelo, caçada) | `Spawn` (objeto do jogo), áreas persistentes, efeitos com tags de bloqueio ("selado" bloqueia habilidades), marcas em alvos (tags + efeitos condicionais), buffs temporários |

O GDD 2025 ainda descreve cartas; o owner orientou **não** introduzir sistema de cartas. Nada no design depende disso.

---

## 20. Impacto em packages existentes (nada muda sem aprovação)

| Package | Mudança | Necessária? |
|---|---|---|
| Core 0.4.0 | Nenhuma: usa `ExecutionOrder.ABILITIES`, `EdgeQueue`, `GameplayTag`/`GameplayTagSet`, `SelectImplementation`, validação | — |
| Core (futuro) | `OwnedTagCounter`/`TagRequirements` e o desenho de linha do tempo no `Core.Editor` | Não (avaliar no M6, ≥ 2/3 consumidores, ADR) |
| Stats 0.1.0 | Nenhuma | — |
| Stats (opcional, D4) | Reserva real de recurso (`ResourcePool.Reserve/Release`) para que outros sistemas também a respeitem | Não; o livro de reservas cobre o M5 |
| HFSM 0.1.0 | Nenhuma | — |
| Character 0.2.0 | Nenhuma (impulso, teleporte, suspensão e modificadores já existem) | — |
| Combat 0.1.0 | Nenhuma para AC7 (`IHitEffect`, `ICombatHitSource`, `EnqueueCandidate`, eventos e `StartAttack` bastam) | — |
| Combat (opcional, D12) | `ICombatActionGate` aditivo para bloquear ataques enquanto uma habilidade executa **sem** HFSM | Não; com HFSM a orquestração já garante |

### 20.1 Matriz de dependências (sem mudança no grafo)
| Package | Camada | Hard | Opcional | Consome do Abilities |
|---|---|---|---|---|
| abilities | L4 | core, stats | hfsm, character, combat, inputsystem | — |
| ai (M6) | L5 | core | stats, character, combat, **abilities** | `AbilityCommandBuffer`, consultas |
| equipment (M7) | L5 | core, stats | **abilities** | concessões e efeitos com fonte |

---

## 21. Decisões para aprovação

| # | Decisão | Recomendação | Alternativas |
|---|---|---|---|
| D1 | Relógio | **Passo fixo, segundos de jogo, regra de sobreposição (ADR-0013); τ pausa no hitstop; cooldowns e efeitos não** (ADR-0019) | Tudo pausa no hitstop; relógio por `Update` |
| D2 | Efeitos entre atores | **Caixa de entrada + aplicação em A2 com ordem total** (ADR-0019) | Aplicação imediata (dependente de ordem) |
| D3 | Efeitos × Stats | **Abilities controla duração/stacks; Stats só modificadores infinitos com o efeito como fonte** (ADR-0020) | Usar `Duration` do Stats (Update, sem stacks/hitstop) |
| D4 | Reserva de custo | **Livro de reservas no ASC + commit que pode falhar** (ADR-0021) | Reserva real no Stats (mudança no Stats); sem reserva (débito na ativação apenas) |
| D5 | Comandos | **`AbilityCommandBuffer` sobre `EdgeQueue`, posse Abilities/External, tag de entrada, estado segurado** (ADR-0022) | Ativação direta por API; reutilizar o buffer do Combat |
| D6 | Composição | **Linha do tempo + ações/janelas SR + efeitos** (ADR-0023) | Classe por habilidade; grafo visual |
| D7 | Cargas e cooldown | **Por concessão, com grupos por tag e cargas com recarga sequencial** | Cooldown como Gameplay Effect (GAS) |
| D8 | Concorrência | **Exclusão por tags; uma execução por concessão; `Reject`/`Restart`** | Slots fixos de habilidade |
| D9 | Projéteis | **Cinemáticos determinísticos com sweep e pool no Abilities** (ADR-0024) | Rigidbody dinâmico; projéteis no Combat |
| D10 | Projéteis × Combat | **Entrega como `ICombatHitSource` na fase 1 do Combat** (ADR-0024) | Abilities calcula dano próprio para projéteis |
| D11 | Interrupção por golpe | **Combat → evento/tag configurável → `InterruptibleBy`** | Toda habilidade interrompida por qualquer golpe que interrompe |
| D12 | Portão de ações do Combat | **Orquestração pela HFSM no M5; `ICombatActionGate` aditivo só se aprovado** | Abilities muda posse do controller durante a execução |
| D13 | Editor de grafo | **Não no M5** (§15) | Graph Editor de habilidades |
| D14 | Linha do tempo no Editor | **Duplicar o mínimo; extrair para Core.Editor no 3.º consumidor** | Extrair agora (ADR + Core 0.5.0) |
| D15 | Samples | **Abilities Playground + Abilities Combat Character HFSM** | Um sample só |
| D16 | Tags possuídas | **`OwnedTagCounter` no Abilities** (ADR-0025) | No Core agora (Core 0.5.0) |
| D17 | Dano de efeitos | **`ResourceDelta` simples (DoT/cura) sem defesa; dano com defesa só pela entrega do Combat** | Efeitos chamando o pipeline de dano do Combat (acoplamento) |
| D18 | Pontos de origem | **`AbilityTargetPoint` com pose canônica (refinamento do ADR-0016)** | Transform vivo (dependente de animação) |

---

## 22. ADRs propostas
| # | Título |
|---|---|
| [0019](../adr/0019-abilities-execution-and-timing.md) | Execução de habilidades: passo fixo, duas sub-bandas, caixa de entrada e hitstop |
| [0020](../adr/0020-gameplay-effects-over-stats.md) | Gameplay Effects sobre o Stats: duração e stacking no Abilities, modificadores com fonte no Stats |
| [0021](../adr/0021-ability-costs-commit-refund.md) | Custos de habilidade: verificação, reserva, commit e reembolso |
| [0022](../adr/0022-ability-commands-and-ownership.md) | Comandos de habilidade e posse em duas fases |
| [0023](../adr/0023-ability-composition.md) | Composição de habilidades por linha do tempo e ações SR (sem grafo) |
| [0024](../adr/0024-projectiles-and-combat-delivery.md) | Projéteis determinísticos e entrega pelo Combat |
| [0025](../adr/0025-owned-tags-in-abilities.md) | Tags possuídas com contagem no Abilities |

---

## 23. Riscos
| # | Risco | Mitigação |
|---|---|---|
| R21 | Escopo do GAS (muitas features, explosão de casos) | Lista fechada da §1.1; entregas pequenas (§24); "adiado" explícito |
| R22 | Determinismo com efeitos cruzados e projéteis | Caixa de entrada com ordem total; identidades combinadas; testes de ordem embaralhada (AC5) |
| R23 | Reserva sem suporte do Stats (Combat pode gastar o reservado) | Commit que falha é um caminho testado; reserva real no Stats como opção (D4) |
| R24 | Muitos projéteis (desempenho, saturação) | Pools pré-aquecidos, sweep `NonAlloc`, contadores, teste com 50 atores × N projéteis |
| R25 | Fragilidade de `[SerializeReference]` (muitos tipos SR) | `[MovedFrom]`, tipos `sealed`, testes que carregam os assets dos samples (R4) |
| R26 | Regeneração do Stats no `Update` × custos no passo fixo | Documentado; custos usam o valor atual; testes com `timeScale` |
| R27 | Interações condicionais em laço (A → B → A) | Validator de ciclo + limite de profundidade por passo (contado) |
| R28 | Duplicação de código de Editor (linha do tempo) | D14: extrair no 3.º consumidor com ADR |

---

## 24. Entregas (cada uma auditável, com testes e docs)

| # | Entrega | Conteúdo | Saída verificável |
|---|---|---|---|
| 1 | Fundação de efeitos e tags | Package, definições de efeito, `ActiveEffectSet` (instant/duração/infinito/período), modificadores com fonte no Stats, stacking, inibição, imunidade, dispel, `ConditionalEffects`, `OwnedTagCounter`, ASC (efeitos/tags/handles), caixa de entrada A2, eventos | EditMode + PlayMode; zero GC de efeitos; AC4, AC5 (efeitos) |
| 2 | Ciclo de vida de habilidades | `AbilityDefinition`, concessões/handles, validação, custos (reserva, commit, reembolso), cooldowns/grupos/cargas, linha do tempo com ações/janelas SR, cancelamento/interrupção, concorrência, passivas e disparadas por evento, `AbilityCommandBuffer` e posse, `IAbilityTimeSource` | AC2, AC3, AC6 |
| 3 | Targeting e projéteis | Estratégias, filtros, linha de visão, `AbilityTargetData`, pontos de origem, projéteis (sweep, pool, perfuração, guiado), áreas | PhysX; AC1 (projétil), AC5 (projéteis) |
| 4 | Integrações | Combat (`IHitEffect`, `ICombatHitSource`, ações, tags de estado, interrupção, hitstop, times, eventos), Character, HFSM, Input System | AC7 |
| 5 | Ferramentas | Inspectors, preview de targeting/trajetória, Ability Debugger, validators | Validação de ferramentas documentada |
| 6 | Samples e fechamento | 2 samples, regressão completa, zero GC (AC8), QG1–QG12, docs, auditoria | AC1–AC10 |

Cada entrega termina com: suítes do package e do WOR verdes, QG12, `check_dependencies`, commits e relatório de
desvios. Nenhuma tag antes da auditoria final do M5.
