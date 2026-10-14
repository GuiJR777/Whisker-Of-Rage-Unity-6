# ADRs do framework

Decisões transversais. ADRs específicas de um package ficam em `Documentation~/adr/` do próprio package.

| # | Título | Status |
|---|---|---|
| [0001](0001-package-per-domain.md) | Um Unity Package por domínio, um repositório por package | Aceita (parcialmente substituída por 0007) |
| [0002](0002-optional-integration-assemblies.md) | Integração entre packages por integration assemblies opcionais | Aceita |
| [0003](0003-execution-order-bands.md) | Ordem de execução por bandas definidas no Core | Aceita |
| [0004](0004-command-buffers.md) | Command buffers por domínio: Player e AI compartilham execução | Aceita |
| [0005](0005-naming-namespaces-assemblies.md) | Nomes de packages, namespaces e assemblies | Aceita |
| [0006](0006-sandbox-file-references.md) | Sandbox com referências `file:`; jogos com submodules | Substituída por 0007 |
| [0007](0007-develop-inside-game-host.md) | Desenvolvimento do framework dentro do Whisker-Of-Rage-Unity-6 | Aceita |
| [0008](0008-serialize-reference-picker-in-core.md) | Seletor genérico de `[SerializeReference]` no Core | Aceita |
| [0009](0009-ai-optional-stats.md) | AI → Stats como dependência opcional | Aceita |
| [0010](0010-character-motor-physics-model.md) | Motor de personagem: Rigidbody dinâmico, controle por variação de velocidade e canais | Aceita |
| [0011](0011-character-command-buffer.md) | `CharacterCommandBuffer`: comandos, dono das bordas, relógio e capacidade | Aceita |
| [0012](0012-character-timing.md) | Decisões temporais do Character | Aceita |
| [0013](0013-combat-timing.md) | Tempo de combate: segundos de jogo, Δt fixo e janelas por sobreposição | Aceita |
| [0014](0014-two-phase-hit-resolution.md) | Resolução de acertos em duas fases (detecção → resolução) | Aceita |
| [0015](0015-combat-commands-and-ownership.md) | Comandos de combate, prioridade e requisição em duas fases | Aceita |
| [0016](0016-data-driven-hitboxes.md) | Hitboxes como dados ancorados, consultas sem alocação e anti-tunneling | Aceita |
| [0017](0017-damage-pipeline-and-outcomes.md) | Pipeline de dano e precedência dos resultados | Aceita |
| [0018](0018-gameplay-tags-in-core.md) | `GameplayTag` no Core | Aceita |
| [0019](0019-abilities-execution-and-timing.md) | Execução de habilidades: passo fixo, duas sub-bandas, caixa de entrada e hitstop | Aceita |
| [0020](0020-gameplay-effects-over-stats.md) | Gameplay Effects sobre o Stats: duração e stacking no Abilities, modificadores com fonte no Stats | Aceita |
| [0021](0021-ability-costs-commit-refund.md) | Custos de habilidade: verificação, reserva, commit e reembolso | Aceita |
| [0022](0022-ability-commands-and-ownership.md) | Comandos de habilidade e posse em duas fases | Aceita |
| [0023](0023-ability-composition.md) | Composição de habilidades por linha do tempo e ações SR (sem grafo) | Aceita |
| [0024](0024-projectiles-and-combat-delivery.md) | Projéteis determinísticos e entrega pelo Combat | Aceita |
| [0025](0025-owned-tags-in-abilities.md) | Tags possuídas com contagem no Abilities | Aceita |
| [0026](0026-ai-execution-and-timing.md) | Execução da IA: passo fixo na banda DECISION, fases e intervalos | Aceita |
| [0027](0027-behaviour-tree-execution-model.md) | Modelo de execução da Behaviour Tree: conjunto ativo, aborts e orçamento | Aceita |
| [0028](0028-blackboard.md) | Blackboard tipado por agente com escrita externa coalescida | Aceita |
| [0029](0029-ai-perception.md) | Percepção: identidades, registro, snapshot e estímulos com ordem total | Aceita |
| [0030](0030-agent-locomotion-and-navigation.md) | Contrato de locomoção do agente e navegação sem NavMesh | Aceita |
| [0031](0031-engagement-coordination.md) | Escopo de engajamento por tokens e vagas | Aceita |
| [0032](0032-ai-command-output-and-tracking.md) | Saída da IA por command buffers e rastreamento por token | Aceita |
| [0033](0033-equipment-contribution-ledger.md) | Livro de contribuições e reversibilidade exata do Equipment | Proposta (M7, design v1) |
| [0034](0034-equipment-transactions-and-timing.md) | Transações atômicas do Equipment, sem tick | Proposta (M7, design v1) |
| [0035](0035-equipment-abilities-through-grants.md) | Efeitos do Equipment sobre o Abilities somente por concessões e tags soltas | Proposta (M7, design v1) |
| [0036](0036-equipment-slots-occupancy-and-storage.md) | Slots, ocupação, unicidade e o limite com inventário | Proposta (M7, design v1) |
| [0037](0037-equipment-item-instances-and-loadout-state.md) | Instâncias de item, identidade e estado de loadout | Proposta (M7, design v1) |

Template: [`templates/package/Documentation~/adr/0000-template.md`](../../templates/package/Documentation~/adr/0000-template.md).
