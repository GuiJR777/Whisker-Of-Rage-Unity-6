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

Template: [`templates/package/Documentation~/adr/0000-template.md`](../../templates/package/Documentation~/adr/0000-template.md).
