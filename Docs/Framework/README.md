# RamiresTech Games — Gameplay Framework (governança)

**Governança** do framework de gameplay Unity 6 da RamiresTech Games, mantida em `Docs/Framework/` do projeto host
`Whisker-Of-Rage-Unity-6` ([ADR-0007](docs/adr/0007-develop-inside-game-host.md)). Não é um Unity package.

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
| [`tools/check_dependencies.py`](tools/check_dependencies.py) | Valida grafo, presença dos packages esperados, `package.json` e asmdefs |
| [`tools/verify_isolated_install.py`](tools/verify_isolated_install.py) | QG12: instala um package em projeto Unity vazio e descartável, compila e testa |
| [`tools/tests/`](tools/tests/) | Testes de regressão das ferramentas (`python -m unittest discover -s Docs/Framework/tools/tests`) |
| [`COMPATIBILITY.md`](COMPATIBILITY.md) | Conjuntos de versões validados juntos |

## Packages

| Package | Camada | Milestone | Estado |
|---|---|---|---|
| `com.ramirestechgames.core` | L0 | M0, M1, M2 | **v0.3.0** |
| `com.ramirestechgames.stats` | L1 | M1 | **v0.1.0** |
| `com.ramirestechgames.hfsm` | L1 | M2 | **v0.1.0** |
| `com.ramirestechgames.character` | L2 | M3 | 0.1.0 (sem tag, aguardando revisão final) |
| `com.ramirestechgames.combat` | L3 | M4 | — |
| `com.ramirestechgames.abilities` | L4 | M5 | — |
| `com.ramirestechgames.ai` | L5 | M6 | — |
| `com.ramirestechgames.equipment` | L5 | M7 | — |

## Layout no host
```
Whisker-Of-Rage-Unity-6/
  Docs/Framework/                 <- esta pasta
  Packages/com.ramirestechgames.*  <- um submodule por package (repo privado no GitHub)
  Assets/_FrameworkSandbox/       <- Integration Sandbox
```

## Uso rápido
```bash
python Docs/Framework/tools/new_package.py stats --description "Generic attributes, resources and modifiers." --milestone M1
python Docs/Framework/tools/check_dependencies.py
```
Requer Python 3.10+.
