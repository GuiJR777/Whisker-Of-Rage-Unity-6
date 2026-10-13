# M5 — Abilities · Especificação técnica

- **Status:** **v2 — aprovada para implementação** (arquitetura geral e D1–D18 aprovadas; ajustes obrigatórios da
  revisão incorporados na §25, que prevalece em caso de divergência; ADRs 0019–0025 aceitas) · **Data:** 2026-10-12
- **Histórico:** v1 (proposta) → v2 (ajustes 1–6 e refinamentos da revisão: determinismo A0/A1/A2, custos, latência
  do `IHitEffect`, projéteis, `Stats.Rebuild`, cadeias condicionais)
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
| `AbilityTargetPoint` | Ponto nomeado (boca, mão) de onde saem projéteis e áreas, por `GameplayTag` — mesmo modelo de pose canônica das `HitboxAnchor` (refinamento R20 do ADR-0016), com contratos de facing próprios do Abilities (§25.7). |

### 3.3 Fluxo de um passo fixo

```
COMMAND_SOURCES (Update)        fontes escrevem bordas/estado no AbilityCommandBuffer (tag de entrada, mira)
STATE_MACHINE   (Update)        HFSM: condição espia + claim → AbilityBehaviour.RequestActivation(token)
ABILITIES       (−700) ASC      A0: snapshot do início do passo (tags, vivo, time, pose) + avanço de cooldowns
ABILITIES + 30  (−670) ASC      A1: pedidos → validação → commit → execuções avançam τ → ações do passo
                                    (lê outros atores só pelo snapshot A0; efeitos saem para caixas de entrada;
                                    mutações do mundo vão para a fila do próprio ator; projéteis avançam)
ABILITIES + 60  (−640) ASC      A2: fila de mutações do mundo → aplica a própria caixa (ordem total, top-K) →
                                    efeitos periódicos/expiração → tags e modificadores atualizados → eventos FIFO
COMBAT … +80    (−600…−520)     Combat (fase 1 inclui ProjectileCombatSource: candidatos de projéteis/áreas)
CHARACTER       (−500)          motor (impulsos das ações de habilidade entram no mesmo passo)
STATS           (Update)        Stats.Tick (regeneração de recursos)
```

Regras: (1) nada decidido em A1 de um ator lê estado de outro ator além do **snapshot A0** (§25.1); efeitos entre
atores **sempre** passam pela caixa de entrada e são aplicados em A2, na ordem total (canal, emissor, sequência,
índice) — mesma ideia do Combat (ADR-0014). (2) Um efeito pedido em A2 (ex.: interação condicional) entra na caixa
do passo seguinte. (3) Modificadores aplicados em A2 do passo *n* valem para o Combat do mesmo passo *n* (Combat roda
depois); efeitos pedidos pelo Combat (`IHitEffect`, fase 4 de *n*) valem em **A2 de *n + 2*** (§25.3).

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
- **Commit** no `CommitPoint`: debita os custos congelados e consolidados atomicamente (`TryConsumeResources`),
  consome carga e inicia cooldown. Se o commit falhar (recurso gasto por outro sistema entre validação e commit), a
  execução termina como `Cancelled(CommitFailed)` sem débito, carga, cooldown nem entregas (§25.2).
- **Execução**: τ em segundos de jogo; ações em τ e janelas `[a, b)` pela **regra de sobreposição** do Combat
  (ADR-0013): nenhuma ação/janela pulada com Δt diferentes; ações dentro de uma janela ativa disparam uma vez.
- **Fim**: ao atingir `Duration`, quando uma ação `EndAbility` executa, ou ao soltar o "segurado" (`ChannelRequiresHeld`).
- **Cancelamento** (voluntário: comando, `CancelAbilitiesWithTags`, troca de estado) e **interrupção** (involuntária:
  tag de interrupção recebida, golpe que interrompe — integração Combat —, ASC desabilitado, ator morto): ambos param
  as ações futuras, removem `ActivationOwnedTags`, encerram janelas (`OnWindowExit`) e aplicam a política de reembolso.

### 5.2 Concorrência
- Habilidades diferentes podem executar ao mesmo tempo; a exclusão é declarada por tags (`ActivationOwnedTags` +
  `ActivationBlockedTags`/`BlockAbilitiesWithTags`/`CancelAbilitiesWithTags`), como no GAS, sem um "slot" fixo.
- A mesma concessão tem **uma** execução por vez (`RetriggerPolicy`: `Reject` ou `Restart` = encerra a anterior
  por completo e só então valida a nova, §25.7).
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
| Verificação | Na validação: custos **consolidados por recurso** e **congelados** na reserva; `disponível = atual − reservas das outras execuções` (a própria reserva nunca bloqueia o próprio commit) — §25.2. |
| Reserva | **Livro de reservas no ASC** (`CostLedger`, D4): uma execução validada com `CommitPoint` posterior reserva o custo; outras habilidades do mesmo ator veem o recurso menos as reservas. Não trava o recurso no Stats (outros sistemas — Combat, scripts — ainda podem gastar): o commit pode falhar e é tratado (§5.1). Reserva real no Stats fica como mudança opcional (§20). |
| Commit | `OnActivate` (padrão), `AtTime(τ)` (ex.: só debita quando o feitiço "sai"), `OnFirstDelivery` (primeiro alvo atingido; **só** em entregas diretas do Abilities — com entrega pelo Combat é erro de validação, §25.2). Nenhuma ação entregadora antes do commit. |
| Reembolso | Política por habilidade: `None`, `OnCancelBeforeDelivery` (cancelada antes de qualquer entrega), `OnInterrupt` (fração configurável), `OnNoTarget` (nenhum alvo válido na entrega). Reembolso = `ResourcePool.Increase` com a habilidade como instigador; o devolvido é o valor aplicado pelo Stats (limitado pelo máximo atual), nunca acima do debitado. |
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
- **Ordem e identidade:** cada disparo tem `ProjectileShotId` = (instigador, índice de disparo, geração da vaga do
  pool); resultados entre projéteis e alvos independem da ordem dos componentes; disparos criados no passo *n*
  começam a mover em *n + 1* (§25.4).
- **Pooling:** `ProjectilePool` por definição/prefab, pré-aquecido; zero GC em regime. Capacidade e excedente contados.
- **Duas entregas:**
  1. **Sem Combat**: ao atingir um ator com ASC, aplica as ações/efeitos de acerto (pela caixa de entrada, §3.3).
  2. **Com Combat** (integração): o projétil vira um **produtor de candidatos** `ICombatHitSource` e enfileira
     `AttackSnapshot`s na fase 1 do Combat (banda `COMBAT`) com as `HitProperties` do projétil. Defesa, parry,
     bloqueio, poise, dano, reações e hitstop são do Combat; efeitos de habilidade vão por `IHitEffect`. O
     `ReceiveHitFeedback` só grava o resultado (fase 3); o destino do projétil (parado por bloqueio, destruído,
     perfurando) é decidido no A1 seguinte pelas políticas da §25.4, e a vaga só volta ao pool sem feedback pendente.
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
  no sink; o Combat o aplica na fase 4 de *n + 1*, que coloca o `EffectSpec` na caixa do ASC; o efeito vale em **A2
  de *n + 2*** (latência exata na §25.3; regra do Combat respeitada).
- `CombatProjectileSource`/`CombatAreaSource` (`ICombatHitSource`, banda `COMBAT`): candidatos de projéteis/áreas.
- Ação `PerformAttack` (inicia um `AttackDefinition` no `CombatController` do ator). A janela `GrantArmor` foi
  **adiada** (fora da 0.1.0, decisão da auditoria preliminar — §26).
- `CombatStateTags`: espelha estados do `CombatTarget` em tags configuráveis (hitstun, knockdown, agarrado, guarda,
  hitstop) — base para `ActivationBlockedTags` e interrupção.
- Interrupção: golpe com `Interrupts` envia o evento configurado (ex. tag "interrompido") → habilidades com essa tag
  em `InterruptibleBy` são interrompidas (D11). O listener (fim da fase 4 de *n*) só registra o pedido; a
  interrupção vale em **A1 de *n + 1*** (§25.3).
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

- Banda `ABILITIES` (−700) com três sub-bandas (A0 = +0, A1 = +30, A2 = +60); componentes de fase ocultos, como no
  Combat (§25.1).
- Pré-alocação: execuções por concessão, `AbilityTargetData`, caixas de entrada, buffers de consulta, pools de
  projéteis. Zero GC em **regime aquecido** (teste com 50 atores ativando, aplicando efeitos com stacking, projéteis e
  áreas); operações de criação de conteúdo ficam fora, listadas na §25.7.
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
| `AbilityActorId`, `ProjectileShotId` (identidades estáveis para ordem total, §25.1/§25.4) | struct | Experimental |
| `EffectSpec` (com cadeia causal de tamanho fixo, §25.6), `AbilityTargetData`, `ActivationResult`, `TargetingResult`, args de eventos | struct/enum | Experimental |
| `IAbilityAction`, `IAbilityWindowBehaviour`, `ITargetingStrategy`, `ITargetFilter`, `IActivationRequirement`, `IEffectExecution`, `IMagnitudeCalculation`, `IAbilityTimeSource`, `ITeamRelation`, `IAbilityFacingSource`, `IAbilityFacingFrameSource` | interfaces/SR | Experimental |
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
**zero GC** com 50 atores. Testes acrescentados pela v2: §25.9.

### 18.2 Critérios de aceite
| # | Critério |
|---|---|
| AC1 | Duas habilidades novas montadas só com assets (sem código), uma com projétil e outra com efeito periódico e stacking. |
| AC2 | Ativa, passiva e disparada por evento usando a mesma validação (tags, custos, cooldowns). |
| AC3 | Custos (§25.2): magnitude congelada, consolidação por recurso, própria reserva excluída, liberação única (cancelamento e `Restart`), `CommitFailed` sem débito/carga/cooldown/entregas, reembolso ≤ debitado e limitado pelo máximo; `OnFirstDelivery` proibido com entrega pelo Combat. |
| AC4 | Remover um efeito restaura os stats exatamente (garantia do Stats), em qualquer ordem de stacks. |
| AC5 | Equivalência lógica com ordem de criação/execução embaralhada (§25.1): efeitos cruzados, tags bloqueadoras simultâneas, stacking, saturação da caixa (top-K), projéteis. |
| AC6 | Nenhuma ação/janela pulada e mesma sequência de decisões em Δt 0,02 / 1/60 / 0,01 / 0,0333 e `timeScale` 0,5. |
| AC7 | Integração Combat via `IHitEffect` e `ICombatHitSource` sem mudar contratos do Combat; efeito de golpe ativo exatamente em A2 de *n + 2*; interrupção em A1 de *n + 1*; projétil bloqueável/parável com políticas da §25.4 e pool sem reuso com feedback pendente. |
| AC8 | Zero GC em regime aquecido com 50 atores (exceções da §25.7); consultas de física sem alocação. |
| AC9 | Nenhum nome de jogo (Jutsu, chakra, Ryu, carta) em código, assets do package ou samples. |
| AC10 | QG1–QG12 (QG12 em projeto vazio com Core + Stats). |
| AC11 | `StatsComponent.Rebuild` com efeitos ativos e inibidos: modificadores restaurados na nova coleção sem duplicação, stacks/tempos/tags preservados; remoção posterior exata (§25.5). |
| AC12 | `ConditionalEffects`: ciclos entre definições acusados pelo validator; cadeias recorrentes entre passos bloqueadas em runtime com diagnóstico; cadeias válidas intactas (§25.6). |

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
| Stats 0.1.0 | Nenhuma (o ASC acompanha `Rebuilt` e ressincroniza os modificadores, §25.5) | — |
| Stats (opcional, D4) | Reserva real de recurso (`ResourcePool.Reserve/Release`) para que outros sistemas também a respeitem | Não; o livro de reservas cobre o M5 |
| HFSM 0.1.0 | Nenhuma | — |
| Character 0.2.0 | Nenhuma (impulso, teleporte, suspensão e modificadores já existem) | — |
| Combat 0.1.0 | Nenhuma para AC7 (`IHitEffect`, `ICombatHitSource`, `EnqueueCandidate`, eventos e `StartAttack` bastam; latência *n + 2* aceita) | — |
| Combat (não adotado no M5) | `ICombatActionGate`: **não** introduzido; orquestração pela HFSM (D12 aprovada) | — |
| Combat (futuro, com ADR) | Aplicação direta de `IHitEffect` em *n + 1* | Não; exige emenda do contrato do `IHitEffect` (§25.3) |

### 20.1 Matriz de dependências (sem mudança no grafo)
| Package | Camada | Hard | Opcional | Consome do Abilities |
|---|---|---|---|---|
| abilities | L4 | core, stats | hfsm, character, combat, inputsystem | — |
| ai (M6) | L5 | core | stats, character, combat, **abilities** | `AbilityCommandBuffer`, consultas |
| equipment (M7) | L5 | core, stats | **abilities** | concessões e efeitos com fonte |

| Abilities consome de | Contratos usados (nenhum alterado) |
|---|---|
| Core 0.4.0 | `ExecutionOrder.ABILITIES`, `EdgeQueue`, `GameplayTag`/`GameplayTagSet`, `SelectImplementation`, validação |
| Stats 0.1.0 | `StatsComponent` (+ `Rebuilt`), `StatCollection` (modificadores com fonte, `RemoveModifiersFromSource`, `TryConsumeResources`), `ResourcePool.Increase/Decrease`, `IStatValueSource` |
| Combat 0.1.0 (integração) | `IHitEffect`, `ICombatEffectSink`/`ICombatEffect`, `ICombatHitSource`, `CombatTarget.EnqueueCandidate`, `HitResult`/`HitOutcome`, eventos, `StartAttack`, hitstop, perfil (times) |
| Character 0.2.0 (integração) | `IExternalForceReceiver`, `Teleport`, suspensão por handle, `IMovementModifierSource`, facing (implementa `IAbilityFacingSource`/`IAbilityFacingFrameSource`) |
| HFSM 0.1.0 (integração) | behaviours, condições, claim de comando |

---

## 21. Decisões (aprovadas na revisão da v1)

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
| D12 | Portão de ações do Combat | **Orquestração pela HFSM no M5; `ICombatActionGate` não introduzido** | Abilities muda posse do controller durante a execução |
| D13 | Editor de grafo | **Não no M5** (§15) | Graph Editor de habilidades |
| D14 | Linha do tempo no Editor | **Duplicar o mínimo; extrair para Core.Editor no 3.º consumidor** | Extrair agora (ADR + Core 0.5.0) |
| D15 | Samples | **Abilities Playground + Abilities Combat Character HFSM** | Um sample só |
| D16 | Tags possuídas | **`OwnedTagCounter` no Abilities** (ADR-0025) | No Core agora (Core 0.5.0) |
| D17 | Dano de efeitos | **`ResourceDelta` simples (DoT/cura) sem defesa; dano com defesa só pela entrega do Combat** | Efeitos chamando o pipeline de dano do Combat (acoplamento) |
| D18 | Pontos de origem | **`AbilityTargetPoint` com pose canônica (refinamento do ADR-0016)** | Transform vivo (dependente de animação) |

---

## 22. ADRs (aceitas na v2)
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
| R22 | Determinismo com efeitos cruzados e projéteis | Snapshot A0; mutações do mundo diferidas para A2; caixa de entrada com ordem total e saturação top-K; identidades por ator e por disparo; equivalência lógica (não bit a bit do PhysX); testes de ordem embaralhada (AC5) |
| R23 | Reserva sem suporte do Stats (Combat pode gastar o reservado) | Commit que falha é um caminho testado; reserva real no Stats como opção (D4) |
| R24 | Muitos projéteis (desempenho, saturação) | Pools pré-aquecidos, sweep `NonAlloc`, contadores, teste com 50 atores × N projéteis |
| R25 | Fragilidade de `[SerializeReference]` (muitos tipos SR) | `[MovedFrom]`, tipos `sealed`, testes que carregam os assets dos samples (R4) |
| R26 | Regeneração do Stats no `Update` × custos no passo fixo | Documentado; custos usam o valor atual; testes com `timeScale` |
| R27 | Interações condicionais em laço (A → B → A), inclusive entre passos | Validator de ciclo no grafo de definições + cadeia causal no `EffectSpec` (limite 8, recorrência bloqueada com diagnóstico) — §25.6 |
| R29 | Latência de 2 passos dos efeitos de golpe (`IHitEffect` → ASC) perceptível no jogo | Documentada e testada (AC7); o que precisa valer antes usa eventos/tags do Combat; aplicação direta só com ADR (§25.3) |
| R30 | `StatsComponent.Rebuild` com efeitos ativos (modificadores perdidos ou duplicados) | ASC acompanha `Rebuilt` e ressincroniza por coleção; testes AC11 (§25.5) |
| R31 | Feedback do Combat chegando a disparo reciclado do pool | Identidade com geração; liberação só sem feedback pendente; feedback antigo descartado e contado (§25.4) |
| R28 | Duplicação de código de Editor (linha do tempo) | D14: extrair no 3.º consumidor com ADR |

---

## 24. Entregas (cada uma auditável, com testes e docs)

| # | Entrega | Conteúdo | Saída verificável |
|---|---|---|---|
| 1 | Fundação de efeitos e tags | Package, definições de efeito, `ActiveEffectSet` (instant/duração/infinito/período), modificadores com fonte no Stats, ressincronização no `Rebuild`, stacking, inibição, imunidade, dispel, `ConditionalEffects` com cadeia causal, `OwnedTagCounter` por fonte, ASC (efeitos/tags/handles), sub-bandas A0/A1/A2, snapshot, caixa de entrada com ordem total e top-K, eventos | EditMode + PlayMode; zero GC de efeitos; AC4, AC5 (efeitos), AC11, AC12 |
| 2 | Ciclo de vida de habilidades | `AbilityDefinition`, concessões/handles, validação, custos (congelamento, consolidação, reserva, commit, reembolso), cooldowns/grupos/cargas, linha do tempo com ações/janelas SR, fila de mutações do mundo, cancelamento/interrupção/`Restart`, concorrência, passivas e disparadas por evento, `AbilityCommandBuffer` e posse, `IAbilityTimeSource` | AC2, AC3, AC6 |
| 3 | Targeting e projéteis | Estratégias, filtros, linha de visão, `AbilityTargetData`, pontos de origem com pose canônica, projéteis (identidade por disparo, sweep, pool, perfuração, guiado, top-K, `OnInstigatorLost`), áreas | PhysX; AC1 (projétil), AC5 (projéteis) |
| 4 | Integrações | Combat (`IHitEffect`, `ICombatHitSource`, ações, tags de estado, interrupção, hitstop, times, eventos), Character, HFSM, Input System | AC7 |
| 5 | Ferramentas | Inspectors, preview de targeting/trajetória, Ability Debugger, validators | Validação de ferramentas documentada |
| 6 | Samples e fechamento | 2 samples, regressão completa, zero GC (AC8), QG1–QG12, docs, auditoria | AC1–AC10 |

Cada entrega termina com: suítes do package e do WOR verdes, QG12, `check_dependencies`, commits e relatório de
desvios. Nenhuma tag antes da auditoria final do M5.

---

## 25. Ajustes da revisão (v2) — normativos

Esta seção prevalece sobre as anteriores em caso de divergência. Aprovada a direção de D1–D18; `ICombatActionGate`
não entra no M5 (D12: orquestração pela HFSM).

### 25.1 Determinismo das fases (A0 → A1 → A2)

| Sub-banda | Ordem | O que faz | Pode ler | Pode escrever |
|---|---|---|---|---|
| **A0** | `ABILITIES + 0` (−700) | Cada ASC publica o **snapshot do início do passo**: tags possuídas (cópia), estado vivo/habilitado, identidade, relação de time, pose do corpo (posição e facing lógico), e avança cooldowns/recargas (§25.7) | o próprio estado | só o próprio snapshot e os próprios cooldowns |
| **A1** | `ABILITIES + 30` (−670) | Pedidos, validação, reserva/commit, avanço das execuções, ações do passo, targeting, avanço e detecção dos projéteis diretos (§25.4) | o próprio estado; **de outros atores, só o snapshot A0**; consultas de física (o mundo não mudou desde o último passo de física) | o próprio estado; caixas de entrada de outros atores; a própria fila de mutações do mundo |
| **A2** | `ABILITIES + 60` (−640) | Cada ASC executa a própria fila de mutações do mundo (teleporte, spawn de prefab, suspensão, liberação ao pool), aplica a caixa de entrada na ordem total, avança efeitos (períodos, expiração), consolida tags e eventos | o próprio estado | o próprio estado e o Stats do próprio ator |

- **A1 é somente-leitura do mundo.** Ações que mudam o mundo ou o estado visível de outro ator (teleporte, spawn de
  prefab com colliders, `Freeze`/`Hold`, criação de áreas persistentes com colliders) são registradas na fila de
  mutações do próprio ator e executadas em A2. Projéteis e áreas não têm colliders próprios (só consultam): criá-los
  em A1 não afeta consultas de outros atores. Impulsos já são diferidos pelo próprio Character (banda `CHARACTER`).
- Leituras de outros atores em A1 (requisitos sobre tags do alvo, filtros de targeting, relação de time, vivo/morto,
  posição para alcance e linha de visão do alvo) usam **exclusivamente o snapshot A0**. Mudanças de tags do próprio
  ator em A1 (`ActivationOwnedTags`, cancelamentos) só valem para o próprio ator e aparecem no snapshot do passo
  seguinte para os outros.
- Dentro de um ator, os pedidos são processados em ordem total (prioridade ↓, instante ↑, token ↑); passivas e
  disparadas por evento, em ordem de concessão (vaga) e de evento (sequência).
- **Identidade:** `AbilityActorId` (64 bits) no ASC, gerado no Editor e serializado, combinado com índice de spawn em
  cópias (mesma ideia de `CombatantId`, sem depender do Combat; a integração Combat pode alinhar os valores).
- **Três componentes de fase** (ocultos, como no Combat): um por sub-banda; todos os A0 terminam antes de qualquer A1,
  e todos os A1 antes de qualquer A2 — sem singleton, sem gerenciador global.
- **Ordem total da caixa de entrada:** chave (canal, emissor, sequência do emissor no passo, índice da entrada).
  Canais em ordem fixa: `Abilities` (efeitos de A1), `Conditional` (gerados em A2 do passo anterior), `Combat`
  (`ICombatEffect` do Combat, chave = solicitante e ordem do Combat), `Script` (API do jogo, com sequência do
  chamador). O emissor é o `AbilityActorId` (ações do ator) ou o `ProjectileShotId` (projéteis e áreas, §25.4); a
  sequência é contada pelo próprio emissor enquanto ele é processado, portanto independe da ordem entre componentes.
- **Projéteis e áreas criados em A1 do passo *n* começam a mover e consultar no passo *n + 1*** (o driver só avança
  disparos com passo de criação < passo atual); assim não importa se o driver roda antes ou depois do criador.
- **Saturação determinística:** a caixa tem capacidade fixa por passo; cheia, ela mantém as **K menores chaves**
  (seleção top-K como o buffer de contatos do Combat) e descarta a maior, contando em `InboxOverflowCount` com o canal.
  Nunca "os primeiros N" por ordem de `FixedUpdate`. Mesma regra para eventos por ator e para o buffer de tags.
- **Testes obrigatórios:** vários atores com ordem de criação e de execução embaralhadas (12 permutações), tags
  bloqueadoras concedidas simultaneamente por atores diferentes, efeitos cruzados (A em B, B em A no mesmo passo),
  stacking com instigadores diferentes no mesmo passo, saturação da caixa com entradas em ordens diferentes —
  resultados idênticos.

### 25.2 Custos e `CommitPoint`

1. **Magnitude congelada:** os custos são calculados uma vez, na validação (nível, stats, `SetByCaller`), e gravados
   na reserva da execução; o commit debita exatamente esses valores.
2. **Consolidação:** custos por `ResourceDefinition` são somados (duplicados viram uma entrada) na verificação, na
   reserva e no commit; o commit chama `TryConsumeResources` uma vez com a lista consolidada (atômico).
3. **Verificação com reservas:** `disponível = atual − (reservas do ator − reserva da própria execução)`; a própria
   reserva nunca bloqueia o próprio commit.
4. **Ciclo da reserva:** `Reserved → Committed` ou `Reserved → Released`, cada transição uma única vez (estado
   explícito na execução). Cancelamento, interrupção e `Restart` liberam a reserva exatamente uma vez.
5. **`CommitFailed`:** nada é debitado (operação atômica), nenhuma carga é consumida, o cooldown não começa, a reserva
   é liberada, a execução termina sem executar ações posteriores e sem entregas. Ações anteriores ao commit só podem
   ser **não-entregadoras** (eventos de apresentação, tags próprias, movimento próprio); o validator acusa ação
   entregadora (efeito em outros, projétil, área, `PerformAttack`) antes do `CommitPoint`.
6. **Reembolso:** quantidade = política × debitado, aplicada com `ResourcePool.Increase`; o valor efetivamente
   devolvido é o retornado pelo Stats (limitado pelo máximo atual do recurso) e **nunca** excede o debitado; o
   excedente acima do máximo é perdido (documentado). Reembolso de cargas e cooldown segue a mesma política.
7. **`OnFirstDelivery` e Combat:** no v0.1.0, habilidades com entrega pelo Combat (`PerformAttack`, projéteis/áreas
   com `CombatDelivery`) **exigem commit anterior ao envio de candidatos** ao Combat (`OnActivate` ou `AtTime` antes
   da primeira ação de entrega); `OnFirstDelivery` nessas habilidades é erro de validação. `OnFirstDelivery` continua
   permitido em entregas diretas do Abilities: o commit ocorre em A1, quando a primeira entrega é determinada e
   **antes** de o efeito entrar na caixa do alvo; se falhar, a entrega é descartada e a execução termina como
   `CommitFailed`.

### 25.3 `IHitEffect` e latência

O contrato do Combat obriga `IHitEffect.OnHit` a pedir mudanças pelo `ICombatEffectSink`; o `ICombatEffect` é
promovido na fase 2 e aplicado na **fase 4 do passo seguinte** do Combat (−520). Nesse ponto o A2 do mesmo passo
(−640) já passou. Portanto, no v0.1.0:

| Passo | Banda | Acontece |
|---|---|---|
| *n* | Combat fase 4 (−520) | Golpe decidido; `ApplyGameplayEffectOnHit.OnHit` enfileira um `ICombatEffect` reutilizado no sink |
| *n + 1* | Combat fase 4 (−520) | `ICombatEffect.Apply` coloca o `EffectSpec` na caixa do ASC (canal `Combat`, chave do Combat: solicitante e ordem) |
| *n + 2* | A2 (−640) | O efeito é aplicado (modificadores, tags, eventos); vale para o Combat de *n + 2* |

**Latência documentada: o efeito de um golpe passa a valer em A2 de *n + 2*** (2 passos fixos; 0,04 s a 50 Hz). Os
testes verificam exatamente esse passo. Alternativa avaliada e **não adotada** no v0.1.0: `OnHit` escrever direto na
caixa do ASC (*n + 1*) — contraria o texto do contrato do `IHitEffect` ("toda mudança pelo sink"); só seria adotada
com ADR e emenda no Combat demonstrando reentrância e isolamento preservados.

Eventos do Combat (golpe recebido, interrupção, parry) chegam aos listeners da integração no fim da fase 4 de *n*;
a integração só **registra** o pedido no ASC. A **interrupção vale em A1 de *n + 1***: ações da habilidade marcadas
para *n + 1* não executam; ações de *n* já executaram (A1 de *n* precede o Combat de *n*). Gameplay Events de combate
seguem o mesmo instante (processados em A1 de *n + 1*).

### 25.4 Projéteis e feedback do Combat

- **Identidade por disparo:** cada disparo recebe `ProjectileShotId` = (instigador, índice de disparo do instigador,
  geração da vaga do pool). O objeto do pool hospeda um `ProjectileCombatSource : ICombatHitSource` cuja identidade
  (`CombatantId`) é derivada desse id; reutilizar a vaga incrementa a geração.
- **Envio:** na fase 1 do Combat (banda `COMBAT`), o source consulta (sweep do último passo) e enfileira candidatos
  com as `HitProperties` do projétil; contatos limitados por `MaxTargets` usam a **mesma seleção top-K** do Combat
  (distância, identidade); saturação da consulta contada.
- **Feedback sem efeito colateral:** `ReceiveHitFeedback` (chamado na fase 3 pelos alvos) apenas grava o resultado
  num buffer fixo do disparo (com a geração); nada muda durante a resolução. O buffer é consumido em A1 do passo
  seguinte, em ordem total (alvo, sequência), aplicando as políticas:

  | Resultado | Política padrão (configurável) |
  |---|---|
  | `Hit` | conta um acerto; com perfuração restante continua, senão termina |
  | `Blocked` | termina (`StopOnBlock`) ou continua (`PierceGuard`) |
  | `Parried` | termina (reflexão fora do M5) |
  | `Invulnerable`, `Dodged`, `Miss` | continua; o alvo não entra no registro de acertos do disparo (pode ser atingido em contato posterior, respeitando `MaxTargets`) |

  Feedback de vários alvos no mesmo passo é processado em ordem total; ao atingir a condição de término, os
  resultados restantes do passo ainda são registrados (já foram decididos pelo Combat), mas não reabrem o disparo.

- **Pool:** um disparo encerrado entra em `PendingRelease`; só volta ao pool depois que o buffer de feedback daquele
  passo foi consumido e não há candidatos pendentes (o Combat entrega todo feedback de um passo até a fase 3 dele).
  Feedback com geração antiga é ignorado e contado.
- **Instigador removido ou habilidade revogada:** o disparo guarda só dados capturados no spawn (id, time, `HitProperties`,
  efeitos de acerto); nunca referencia a execução. `OnInstigatorLost`: `Destroy` (padrão) ou `ContinueDetached` (sem
  stats do atacante: `ICombatHitSource.Stats = null`, regra documentada do Combat — poder de ataque 1). Com o
  instigador vivo, `Stats` devolve a fonte do instigador verificada a cada acesso (objeto Unity destruído → `null`).
  Referências a objetos Unity são verificadas ("fake null") antes de cada uso; revogar a habilidade não afeta
  disparos em voo (eles não guardam a concessão).
- **Entregas diretas do Abilities** (sem Combat): detecção em A1 com o mesmo top-K e o snapshot A0 dos alvos;
  efeitos vão para a caixa dos alvos (aplicados em A2 do mesmo passo).

### 25.5 `StatsComponent.Rebuild`

- O ASC guarda, por efeito ativo, a **coleção** onde seus modificadores estão. Ao receber `Rebuilt`, ele reaplica os
  modificadores de todos os efeitos ativos **não inibidos** na nova coleção (mesma fonte = o efeito; magnitude pelos
  stacks atuais), sem tocar a coleção antiga; stacks, tempos, períodos e tags não mudam; handles antigos de
  modificador são descartados.
- Listeners: inscrição em `OnEnable` (e na primeira resolução do `StatsComponent`), remoção em `OnDisable` e
  `OnDestroy`; ao reabilitar, o ASC compara a coleção atual com a registrada e ressincroniza se ela mudou enquanto
  estava desabilitado. Sem `StatsComponent`, efeitos com modificadores ficam ativos sem modificadores e são
  sincronizados quando a coleção existir (contado).
- **Testes:** efeitos ativos e inibidos atravessando `Rebuild`; remoção posterior restaura a coleção nova exatamente;
  desabilitar/reabilitar com `Rebuild` no meio; nenhum modificador duplicado. Nenhuma mudança no Stats.

### 25.6 `ConditionalEffects`

- **Validator:** grafo entre definições (efeito → efeitos condicionais que pode aplicar, transitivamente); qualquer
  ciclo (A → B → A, A → A, A → B → C → A) é **erro**, salvo se todas as definições do ciclo marcarem `AllowRecurrence`
  (aviso, exige limite).
- **Proteção dinâmica entre passos:** cada `EffectSpec` carrega uma **cadeia causal** de tamanho fixo (ids das
  definições que a geraram, máximo `MaxConditionalChain` = 8, sem alocação). Um efeito condicional herda a cadeia + a
  própria definição. É bloqueado, com diagnóstico (`ConditionalChainBlocked`, com a cadeia no Debugger), quando: a
  definição já está na cadeia (recorrência) e não tem `AllowRecurrence`; ou a cadeia atingiu o limite. Interações
  válidas (cadeias acíclicas até 8) não são afetadas; a proteção vale entre passos porque a cadeia viaja no spec.

### 25.7 Refinamentos

- **`CooldownGroupTag`:** cooldowns de grupo vivem no ASC por tag exata, ordenados por `StableId`. Início no commit
  (duração congelada); se já ativo, o restante passa a ser o **maior** entre o atual e o novo (política `Max`,
  configurável `Replace`). Avanço em A0 de cada passo; término quando restante ≤ tolerância, com evento
  `CooldownEnded` em A2 do mesmo passo. Habilidades do grupo são bloqueadas enquanto o grupo está ativo.
- **`Restart`:** em A1, a execução anterior é **completamente** encerrada antes da nova validação (janelas recebem
  `OnExit`, tags removidas, reserva liberada ou reembolso aplicado pela política, ações pendentes descartadas,
  mutações do mundo pendentes da execução anterior canceladas). Se a nova validação falhar, a anterior continua
  encerrada (documentado).
- **`OwnedTagCounter`:** contribuições por (tag, fonte); a fonte é um handle (dono + vaga + geração): efeito ativo,
  execução, concessão, integração ou `LooseTagHandle`. Remover por fonte remove só as contribuições dela; várias fontes
  da mesma tag somam; handle obsoleto ou de outro ator não remove nada. Consultas hierárquicas como no Core.
- **`AbilityTargetPoint`:** pose canônica relativa ao frame de facing (mesma regra do refinamento R20 do ADR-0016),
  com contratos **próprios do Abilities** (`IAbilityFacingSource`, `IAbilityFacingFrameSource`) implementados pela
  integração Character (`FacingController` e visual do presenter); sem dependência do Combat. Testes com facing
  inicial ±X/±Z e presenter girando.
- **Determinismo:** a garantia é de **equivalência lógica** — mesmas decisões, ativações, efeitos, stacks e tags dados
  os mesmos comandos e os mesmos resultados de consulta de física, independentemente da ordem entre atores e do Δt
  (dentro das tolerâncias). Não se promete determinismo bit a bit do PhysX entre máquinas ou versões.
- **Zero GC:** medido em **regime aquecido** (concessões feitas, pools aquecidos, cada definição já usada uma vez).
  Ficam fora, intencionalmente: conceder/revogar habilidades, primeira aplicação de uma definição nova, crescimento de
  pool além da capacidade, `Rebuild` do Stats, spawn de prefabs do jogo, abertura de ferramentas. Cada exceção é
  listada no CONTRACTS.


### 25.8 Validators acrescentados

| Validator | Severidade |
|---|---|
| Ação entregadora (efeito em outros, projétil, área, `PerformAttack`, `Spawn`) antes do `CommitPoint` | Erro |
| `CommitPoint = OnFirstDelivery` em habilidade com entrega pelo Combat (`PerformAttack`, `CombatDelivery`) | Erro |
| `CommitPoint = AtTime(τ)` com τ depois da primeira ação de entrega ou depois de `Duration` | Erro |
| Custo duplicado do mesmo `ResourceDefinition` | Aviso (consolidado em runtime) |
| Ciclo entre definições em `ConditionalEffects` (qualquer comprimento) | Erro; aviso se todas do ciclo têm `AllowRecurrence` |
| Cadeia condicional acíclica mais longa que `MaxConditionalChain` | Aviso (será bloqueada em runtime) |
| Projétil com `OnInstigatorLost = ContinueDetached` e entrega pelo Combat | Aviso (poder de ataque 1) |
| `AbilityTargetPoint` cuja pose depende de um transform animado sem frame de facing | Aviso (mesma regra das âncoras do Combat) |
| `CooldownGroupTag` com durações diferentes entre habilidades do grupo | Informativo (política `Max`) |

### 25.9 Testes acrescentados (v2)

- **Ordem embaralhada (PlayMode):** 4 atores, 12 permutações de criação e de `SetSiblingIndex`; efeitos cruzados no
  mesmo passo, tags bloqueadoras concedidas simultaneamente, stacking `AggregateBySource` com instigadores diferentes,
  saturação da caixa (capacidade 4, 7 entradas) — conteúdo da caixa, stacks, tags e contadores idênticos.
- **Snapshot A0:** requisito sobre tag do alvo concedida no A1 de outro ator no mesmo passo não é visto até o passo
  seguinte, em qualquer ordem.
- **Custos:** duplicados consolidados; própria reserva não bloqueia o commit; duas habilidades reservando o mesmo
  recurso; `Restart` e cancelamento liberam uma vez; `CommitFailed` (recurso gasto por fora) sem débito, carga,
  cooldown nem entregas; reembolso com máximo reduzido no meio da execução.
- **Latência Combat:** golpe no passo *n* → efeito ativo exatamente em A2 de *n + 2* (e não antes); interrupção pedida
  na fase 4 de *n* → ações de *n + 1* não executam.
- **Projéteis:** bloqueio, parry, perfuração com 3 alvos no mesmo passo (ordem total), `MaxTargets` com saturação,
  reuso de vaga do pool com feedback de geração antiga descartado, instigador destruído em voo (`Destroy` e
  `ContinueDetached`), habilidade revogada em voo.
- **`Rebuild`:** efeitos ativos e inibidos, `Rebuild` durante a execução, remoção posterior igual a coleção virgem,
  `Rebuild` com o ASC desabilitado.
- **Condicionais:** A → B → A bloqueado com diagnóstico entre passos; cadeia válida de 5 intacta; `AllowRecurrence`
  respeitando o limite.
- **Cooldown de grupo, `Restart`, `OwnedTagCounter`** (fontes múltiplas, handle obsoleto/estrangeiro) e
  **`AbilityTargetPoint`** (facing ±X/±Z, presenter girando).

---

## 26. Ajustes da auditoria preliminar (2026-10-13) — normativos

| Tema | Decisão |
|---|---|
| Janela `GrantArmor` | **Adiada** para depois da 0.1.0. A armadura continua disponível pelas janelas de armadura do `AttackDefinition` iniciado por `PerformAttack`. Qualquer API de armadura externa no Combat exige ADR e aprovação próprias. |
| Passivas por acertos de projéteis | **Obrigatório.** Cada resultado entregue a um disparo pelo `ReceiveHitFeedback` é guardado (sem efeito colateral, fase 3) com a identidade do disparo e o instigador; depois da fase 4 do Combat do mesmo passo (`COMBAT + 85`) os resultados são ordenados por (disparo, alvo, sequência da decisão) e entregues ao `CombatAbilityBridge` do instigador, cujos mapeamentos `Caused` enviam os Gameplay Events — mesmo caminho e mesmo instante do `HitConfirmed` corpo a corpo (A1 de *n + 1*). `Hit`, `Blocked`, `Parried` e os demais desfechos são separados pelas condições dos mapeamentos; instigador destruído ou desabilitado não recebe nada (contado). |
| Parry de projétil | O disparo termina, sem reflexão e **sem** parry-stun no lançador (política padrão). |
| Driver em `ABILITIES + 15`; `Blocked` consome `MaxHits` | Aprovados. |
| Feedback externo de áreas | Áreas com entrega externa registram os alvos de `Hit`, `Blocked` e `Parried` (re-acerto só após o intervalo) e contam `Hit`/`Blocked`; nunca terminam por bloqueio ou parry. |
| Magnitudes do instigador em efeitos de golpe do Combat | **Capturadas**, nunca lidas pelo alvo: golpe corpo a corpo captura do ASC do atacante quando o Combat decide o golpe (instante em que o próprio Combat lê os stats do atacante); projéteis e áreas usam o spec capturado no lançamento (§25.4: o disparo guarda só dados do spawn), inclusive com o lançador destruído. Sem ASC no atacante (ou entrega não lançada por habilidade) o fallback é usado e **contado** (`UncapturedCount`). |
| Magnitudes do instigador em efeitos condicionais | **Pendente de decisão do owner.** O spec condicional é criado na A2 do alvo; ler o instigador ali quebraria o determinismo (§25.1), então as magnitudes do instigador usam o fallback. Deixou de ser silencioso: o validator avisa em toda interação cujo efeito lê o instigador. Opções: (a) manter o fallback documentado; (b) capturar, ao criar o spec pai, as magnitudes dos efeitos condicionais diretos (um nível; spec maior); (c) proibir magnitudes do instigador em efeitos condicionais (erro no validator). |
