# RamiresTech Games — Gameplay Framework (governança)

Repositório de **governança** do framework de gameplay Unity 6 da RamiresTech Games.
Não é um Unity package e não é consumido pelos jogos.

## O que há aqui

| Caminho | Conteúdo |
|---|---|
| [`docs/01-dependency-graph.md`](docs/01-dependency-graph.md) | Dependency Graph oficial |
| [`docs/02-package-boundaries-and-contracts.md`](docs/02-package-boundaries-and-contracts.md) | Limites e contratos de cada package |
| [`docs/03-repository-structure.md`](docs/03-repository-structure.md) | Estrutura padrão de repositório |
| [`docs/04-context-engineering.md`](docs/04-context-engineering.md) | Template de Context Engineering |
| [`docs/05-testing-strategy.md`](docs/05-testing-strategy.md) | Estratégia de testes |
| [`docs/06-versioning-and-publishing.md`](docs/06-versioning-and-publishing.md) | Publicação e versionamento |
| [`docs/07-roadmap.md`](docs/07-roadmap.md) | Roadmap, dependências, Quality Gates |
| [`docs/08-architectural-risks.md`](docs/08-architectural-risks.md) | Riscos arquiteturais e referência de mecânicas |
| [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md) | Convenções consolidadas e conflitos resolvidos |
| [`docs/adr/`](docs/adr/) | ADRs transversais |
| [`dependency-graph.json`](dependency-graph.json) | Grafo em formato de máquina |
| [`templates/package/`](templates/package/) | Package Template |
| [`tools/new_package.py`](tools/new_package.py) | Gera um package a partir do template |
| [`tools/check_dependencies.py`](tools/check_dependencies.py) | Valida grafo, `package.json` e asmdefs |
| [`COMPATIBILITY.md`](COMPATIBILITY.md) | Conjuntos de versões validados juntos |

## Packages

| Package | Camada | Milestone | Estado |
|---|---|---|---|
| `com.ramirestechgames.core` | L0 | M0 | 0.1.0 implementado, aguardando revisão |
| `com.ramirestechgames.stats` | L1 | M1 | — |
| `com.ramirestechgames.hfsm` | L1 | M2 | — |
| `com.ramirestechgames.character` | L2 | M3 | — |
| `com.ramirestechgames.combat` | L3 | M4 | — |
| `com.ramirestechgames.abilities` | L4 | M5 | — |
| `com.ramirestechgames.ai` | L5 | M6 | — |
| `com.ramirestechgames.equipment` | L5 | M7 | — |

## Workspace
```
Games/Framework/
  ramirestech-framework/   <- este repo
  com.ramirestechgames.*/  <- um repo por package
  RamiresTech-Sandbox/     <- projeto Unity host (file: references)
```

## Uso rápido
```bash
python tools/new_package.py stats --description "Generic attributes, resources and modifiers." --milestone M1
python tools/check_dependencies.py
```
Requer Python 3.10+.
