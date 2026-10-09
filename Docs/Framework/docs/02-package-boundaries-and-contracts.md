# 02 — Limites e contratos dos packages

> Esta é a **proposta de M0**. Os nomes de tipos são o contrato pretendido. Cada milestone pode refiná-los,
> e cada refinamento é registrado no `CONTRACTS.md` do package (e em ADR quando muda um limite).
>
> Legenda: **SO** = ScriptableObject (definição, imutável em runtime) · **MB** = MonoBehaviour ·
> **Pure** = classe C# pura · **SR** = polimórfico via `[SerializeReference]`.

## Padrões transversais

| Padrão | Descrição |
|---|---|
| **Definition / Instance** | Cada conceito configurável tem um `XDefinition` (SO) e uma instância de runtime (Pure) com o estado mutável. |
| **Component fino** | O MB cria e tica a instância Pure; expõe eventos e Inspector. |
| **Command Buffer** | Entrada de ações (Player ou AI) é escrita em um buffer por domínio; a execução lê o buffer. Ver [ADR-0004](adr/0004-command-buffers.md). |
| **Extension point** | Interface definida na camada inferior e implementada pela superior (ou pelo jogo) e configurada no Inspector via SR. |
| **Validation** | Definições implementam `IValidatable` (Core) e aparecem no Validator. |
| **Debugger** | Cada package tem visualização de runtime (Editor-only/Development Build). |

---

## core — `RamiresTechGames.Core` (L0)

**Responsabilidade:** somente tipos transversais com ≥ 2 consumidores reais.

| Contrato | Tipo | Desde | Descrição |
|---|---|---|---|
| `ExecutionOrder` | static consts | M0 | Bandas para `[DefaultExecutionOrder]`, garantindo ordem previsível entre packages. |
| `IValidatable` | interface | M0 | `void Validate(ValidationReport report)` para definições e componentes. |
| `ValidationReport`, `ValidationIssue`, `ValidationSeverity` | Pure | M0 | Coleta de problemas de configuração com contexto (`UnityEngine.Object`). |
| Validator (Editor) | EditorWindow + runner | M0 | `Tools/RamiresTech Games/Validator`: valida assets, prefabs e cenas abertas. |
| `Serialization.SelectImplementationAttribute` + seletor no Editor | atributo + drawer | M1 (ADR-0008) | Escolha de implementação de campos `[SerializeReference]` no Inspector; Validator acusa tipos ausentes. |
| `GameplayTag`, `GameplayTagContainer`, `GameplayTagQuery` | struct/Pure + SO de registro | **M4/M5** (sob demanda) | Tags hierárquicas usadas por Combat, Abilities, AI e Equipment. Entram no Core quando o 2º consumidor existir. |

**Não faz:** event bus global, service locator, singletons, pool genérico, extensões utilitárias "porque sim",
qualquer conceito de gameplay (vida, dano, time).

---

## stats — `RamiresTechGames.Stats` (L1) · M1

**Responsabilidade:** atributos numéricos, recursos, modificadores e stats derivadas.

> Contrato detalhado e vigente: especificação v2 ([design/m1-stats-design.md](design/m1-stats-design.md)) e
> `CONTRACTS.md` do package. A tabela abaixo é o resumo de M0.

| Contrato | Tipo | Descrição |
|---|---|---|
| `StatDefinition` | SO | Identidade de uma stat: nome, valor base padrão, limites, arredondamento. |
| `ResourceDefinition` | SO | Recurso consumível (vida, energia): stat de máximo associada, regeneração, atraso de regen. |
| `StatSetDefinition` | SO | Conjunto reutilizável: stats + valores base + recursos + fórmulas derivadas. |
| `DerivedStatFormula` | SR | Fórmula de stat derivada (ex.: soma ponderada, curva). Dependências declaradas → recálculo ordenado. |
| `StatModifier` | struct | Operação (`Flat`, `Additive`, `Multiplicative`), valor, prioridade, **fonte**, duração opcional. |
| `ModifierHandle` | struct | Retorno de `AddModifier`, usado para remoção. |
| `StatCollection` | Pure | `GetValue`, `GetBaseValue`, `SetBaseValue`, `AddModifier`, `RemoveModifier`, `RemoveModifiersFromSource`, `Tick(deltaTime)`. |
| `ResourcePool` | Pure | `Current`, `Max`, `TryConsume`, `Restore`, `Deplete`, regeneração. |
| `StatsComponent` | MB | Dono da `StatCollection` de um ator; tica durações e regen. |
| Eventos | `StatChanged(StatChangedArgs)`, `ResourceChanged(ResourceChangedArgs)`, `ResourceDepleted` | Notificação para UI/presentation/glue. |

**Fórmula de modificadores (proposta, confirmar em ADR no M1):**
`final = (base + ΣFlat) × (1 + ΣAdditive) × ΠMultiplicative`, depois clamp e arredondamento.

**Não faz:** saber o que é "vida", "dano" ou "chakra"; aplicar dano; equipar itens.
**Editor:** inspector de `StatSetDefinition`, Runtime Stat Inspector (valores, modificadores e fontes ao vivo), validators.

---

## hfsm — `RamiresTechGames.HierarchicalStateMachine` (L1) · M2

**Responsabilidade:** executar estados hierárquicos e transições. **Não decide estratégia.**

| Contrato | Tipo | Descrição |
|---|---|---|
| `StateMachineDefinition` | SO | Grafo: estados (SR), sub-estados, estado inicial por nível, transições. |
| `IState` / `StateBehaviour` | SR (sem estado entre atores) | `Enter`, `Tick`, `FixedTick`, `Exit` recebendo `StateContext`. |
| `ITransitionCondition` | SR | Guard: `bool Evaluate(StateContext context)`. |
| `TransitionDefinition` | data | Origem (estado ou *Any* do pai), destino, condições, prioridade, `CanInterrupt`. |
| `StateContext` | Pure | Contexto de runtime por ator: acesso a componentes (`TryGet<T>`), dados por estado, tempo no estado. |
| `StateMachineInstance` | Pure | Instância por ator; avalia transições por prioridade, do nível mais alto ao mais profundo. |
| `StateMachineRunner` | MB | Tica a instância em `STATE_MACHINE`; expõe estado ativo. |
| Eventos | `StateEntered`, `StateExited`, `TransitionTaken` | Debug e presentation. |

**Não faz:** IA, input, movimento, combate. Packages superiores fornecem *tipos de estado* via integração
(ex.: `Character.Integration.HierarchicalStateMachine.LocomotionState`).
**Editor:** visualização do grafo, debugger (estado ativo e histórico de transições), validators (estados órfãos, ciclos sem guard).

---

## character — `RamiresTechGames.Character` (L2) · M3

**Responsabilidade:** movimento físico 3D de personagens controlados por comandos.

| Contrato | Tipo | Descrição |
|---|---|---|
| `MovementProfileDefinition` | SO | Velocidades, aceleração/desaceleração, gravidade, **altura exata de pulo**, pulo variável, coyote time, jump buffer, air control, dash. |
| `CharacterCommands` | struct | `Move` (Vector2 plano), `JumpPressed`, `JumpHeld`, `DashPressed`, `FacingOverride`. |
| `CharacterCommandBuffer` | MB | Recebe comandos de qualquer fonte (Player, AI, cutscene) e os consome por tick. |
| `CharacterMotor` | MB + Pure core | Rigidbody 3D; aplica comandos, gravidade, forças externas. |
| `IExternalForceReceiver` | interface | `AddImpulse`, `AddForceOverTime`, `ClearExternalForces` — usado por knockback/launch. |
| `GroundSensor` | Pure + MB | Detecção de chão por casts com `NonAlloc`. |
| `FacingController` | Pure + MB | Orientação/facing independente de render. |
| Eventos | `Jumped`, `Landed`, `DashStarted`, `DashEnded`, `GroundedChanged` | Presentation/glue. |
| Integrações opcionais | `Integration/HierarchicalStateMachine` (estados de locomoção), `Integration/Stats` (velocidade por stat), `Integration/InputSystem` (`PlayerInputCharacterSource`). |

**Não faz:** SpriteRenderer obrigatório, animação, input direto, IA.
**Editor:** gizmos de chão/pulo, preview de arco de pulo no inspector do profile, debugger de velocidade/estado de chão.

---

## combat — `RamiresTechGames.Combat` (L3) · M4

**Responsabilidade:** ataques, combos, detecção de acerto, defesa e resolução de dano.
Começa por um **estudo de design de combate** (mecânicas inspiradas no BeatEmUpTemplate2D: combos, hit-confirm,
agarrões, reações). Nenhuma implementação existente é migrada.

| Contrato | Tipo | Descrição |
|---|---|---|
| `AttackDefinition` | SO | Fases (startup/active/recovery), hitboxes por janela ativa, dano, reação (knockback, launch, knockdown, stun), janelas de cancel, requisitos (chão/ar), máx. alvos, tags. |
| `ComboDefinition` | SO | Grafo de ataques; arestas com condições (ação de input, janela de tempo, chão/ar, hit confirmado). |
| `ComboRunner` | Pure | Input buffering, combo timeout, branching. |
| `CombatCommandBuffer` | MB | Ações de combate (ataque por id de ação, defender, agarrar) de Player ou AI. |
| `HitBox` / `HurtBox` | MB | Física 3D (overlap `NonAlloc`), layers configuráveis; HurtBox aponta para `CombatTarget`. |
| `CombatTarget` | MB | Estado defensivo: bloqueio, janela de parry, invulnerabilidade, esquiva. |
| `DamageResolver` | Pure | Pipeline configurável (`IDamageStep`, SR) que lê `StatCollection`s → `HitResult`. |
| `HitResult` | struct | `Outcome` ∈ {Hit, Miss, Blocked, Parried, Dodged, Invulnerable}, dano, reação aplicada. |
| `IKnockbackReceiver` | interface | Receptor de forças; impl. padrão `RigidbodyKnockbackReceiver`; Character via integração. |
| `IHitEffect` | interface (extension point) | Efeitos adicionais por acerto (Abilities implementa). |
| Grab/Throw | `GrabDefinition`, `GrabPoint` | Agarrar, segurar, arremessar alvos. |
| Eventos | `AttackStarted`, `AttackEnded`, `HitConfirmed`, `HitReceived`, `ComboAdvanced`, `ComboReset` | Presentation (Animator, VFX, hitstop, camera shake) via adapters. |

**Não faz:** ler Input System, tocar Animator, conhecer Abilities, conhecer stats específicas (as stats usadas no dano são configuradas no `DamageProfile`).
**Editor:** Combo Graph Editor, runtime combo debugger, gizmos de hitbox por fase, validators.

---

## abilities — `RamiresTechGames.Abilities` (L4) · M5

**Responsabilidade:** Gameplay Ability System em escala indie.

| Contrato | Tipo | Descrição |
|---|---|---|
| `AbilityDefinition` | SO | Ativa/passiva, tags (ability, required, blocked, cancel), custos, cooldown, targeting (SR), operações (SR), composição. |
| `AbilityInstance` | Pure | Ciclo de vida: `CanActivate` → `Activate` → `Tick` → `End`/`Cancel`. |
| `GameplayEffectDefinition` | SO | Instantâneo/duração/infinito, período, modificadores de Stats, tags concedidas, regras de stacking, condições de remoção. |
| `ActiveGameplayEffect` | Pure | Estado de um efeito aplicado (stacks, tempo restante, handles de modificadores). |
| `AbilitySystemComponent` | MB | Concede/revoga abilities, tags possuídas, efeitos ativos, cooldowns. |
| `AbilityCommandBuffer` | MB | Pedidos de ativação (Player/AI). |
| `ITargetingStrategy`, `IAbilityOperation`, `IActivationRequirement` | SR | Pontos de extensão. |
| Eventos | `AbilityActivated`, `AbilityEnded`, `AbilityCancelled`, `EffectApplied`, `EffectRemoved`, `TagsChanged` | |
| Integrações opcionais | `Integration/Combat` (operação "executar ataque", `IHitEffect` que aplica effect), `Integration/Character` (dash/impulso), `Integration/HierarchicalStateMachine` (estado de casting). |

**Não faz:** nomes de um jogo (ex.: Jutsu, Técnica, Chakra no WOR) — esses são *assets* no jogo.
**Editor:** Ability Debugger (abilities, cooldowns, tags, efeitos ativos), inspectors, validators.

---

## ai — `RamiresTechGames.AI` (L5) · M6

**Responsabilidade:** decidir intenções com Behaviour Tree.

| Contrato | Tipo | Descrição |
|---|---|---|
| `BehaviourTreeDefinition` | SO | Grafo de nós (SR). |
| `BehaviourNode` | SR | Ciclo `OnStart`/`OnTick`/`OnStop`, `NodeStatus` ∈ {Running, Success, Failure}. |
| Composites | Selector, Sequence, Parallel | Parallel apenas com política explícita de sucesso/falha. |
| Decorators | Inverter, Repeat, Cooldown, TimeLimit, Conditional (com abort/interrupção) | |
| `BlackboardDefinition` / `Blackboard` | SO / Pure | Chaves tipadas; instância por agente. |
| `ISensor` | SR | Visão, alcance, ruído → escrevem no Blackboard. |
| `ITargetSelector` | SR | Seleção de alvo por pontuação. |
| `BehaviourTreeRunner` | MB | Tica em `DECISION` com intervalo configurável. |
| Integrações opcionais | `Integration/Stats` (condições sobre stats/recursos, ADR-0009), `Integration/Character` (MoveTo, Strafe, Flee → `CharacterCommandBuffer`), `Integration/Combat` (Attack, Defend → `CombatCommandBuffer`), `Integration/Abilities` (UseAbility → `AbilityCommandBuffer`). |

**Não faz:** mover, atacar ou animar diretamente.
**Editor:** Behaviour Tree Graph Editor, Runtime Blackboard Debugger, status de nós ao vivo.

---

## equipment — `RamiresTechGames.Equipment` (L5) · M7

**Responsabilidade:** equipar itens em slots e aplicar seus efeitos.

| Contrato | Tipo | Descrição |
|---|---|---|
| `EquipmentSlotDefinition` | SO | Slot e restrições (categorias/tags aceitas). |
| `EquipmentDefinition` | SO | Slot alvo, modificadores de Stats, efeitos (SR `IEquipmentEffect`). |
| `EquipmentLoadoutDefinition` | SO | Conjunto inicial/predefinido. |
| `EquipmentComponent` | MB | `TryEquip`, `Unequip`, `GetEquipped`; modificadores aplicados com a instância como **fonte**, removidos ao desequipar. |
| Eventos | `Equipped`, `Unequipped`, `EquipFailed` | |
| Integração opcional | `Integration/Abilities` (`GrantAbilityEffect`, `ApplyPassiveEffect`). |

**Não faz:** inventário, loot, economia, UI de inventário.
**Editor:** inspector de loadout/slots, validators (slot incompatível, modificadores sem stat).

---

## Fluxo integrado (exemplo: inimigo ataca o jogador)

```
[COMMAND_SOURCES] PlayerInput → CharacterCommandBuffer / CombatCommandBuffer (jogador)
[DECISION]        BehaviourTreeRunner (inimigo) → nó Attack → CombatCommandBuffer (inimigo)
[STATE_MACHINE]   HFSM do inimigo: guard "HasAttackRequest" → AttackState (Combat.Integration.HFSM)
[COMBAT]          ComboRunner escolhe AttackDefinition → HitBox ativa → HurtBox do jogador
                  → DamageResolver(StatCollection atacante/defensor) → HitResult(Hit)
                  → ResourcePool(vida).TryConsume · IKnockbackReceiver(motor do jogador) · IHitEffect[]
[CHARACTER]       CharacterMotor integra comandos + forças externas (FixedUpdate)
[PRESENTATION]    Adapters escutam HitConfirmed/StateEntered → Animator, VFX, SFX, hitstop
```
